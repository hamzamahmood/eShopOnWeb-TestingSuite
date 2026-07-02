using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.ErrorResponse;
using MaxioAdvancedBilling.Core.Exceptions;
using MaxioAdvancedBilling.Errors;
using MaxioAdvancedBilling.Models;
using MaxioAdvancedBilling.Models.Enums;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using DomainSubscriptionState = Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions.SubscriptionState;
using MaxioSubscription = MaxioAdvancedBilling.Models.Subscription;
using MaxioSubscriptionState = MaxioAdvancedBilling.Models.Enums.SubscriptionState;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// The single class in this codebase that talks to Maxio Advanced Billing. Implements the provider-agnostic
/// <see cref="IBillingClient"/> via the generated <see cref="MaxioAdvancedBillingClient"/> SDK. Every SDK
/// call is wrapped to translate <c>SdkException&lt;TError&gt;</c> into the typed <c>ApplicationCore</c>
/// exceptions - callers above this class never see a Maxio SDK type.
/// </summary>
public class MaxioBillingClient : IBillingClient
{
    private readonly MaxioAdvancedBillingClient _client;
    private readonly MaxioSettings _settings;
    private readonly IAppLogger<MaxioBillingClient> _logger;

    public MaxioBillingClient(MaxioAdvancedBillingClient client, IOptions<MaxioSettings> settings, IAppLogger<MaxioBillingClient> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken cancellationToken)
    {
        try
        {
            var products = await _client.ProductFamilies.ListProductsForProductFamily(
                _settings.ProductFamilyId.ToString(CultureInfo.InvariantCulture),
                dateField: null, filter: null, startDate: null, endDate: null, startDatetime: null, endDatetime: null,
                includeArchived: false, include: null, ct: cancellationToken);

            return products
                .Select(p => p.Product)
                .Where(p => p is not null && p.ArchivedAt is null)
                .Select(p => MapPlan(p!))
                .ToList();
        }
        catch (SdkException<ListProductsForProductFamilyError> ex)
        {
            throw WrapError(ex.Error, "list plans");
        }
    }

    public async Task<string?> FindCustomerIdAsync(string customerReference, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Customers.ReadCustomerByReference(customerReference, cancellationToken);
            return response.Customer.Id!.Value.ToString(CultureInfo.InvariantCulture);
        }
        catch (SdkException<RawError> ex) when (ex.Error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (SdkException<RawError> ex)
        {
            throw WrapRawError(ex.Error, "find customer");
        }
    }

    public async Task<string> FindOrCreateCustomerAsync(string customerReference, string email, string? firstName, string? lastName, CancellationToken cancellationToken)
    {
        var existingId = await FindCustomerIdAsync(customerReference, cancellationToken);
        if (existingId is not null)
        {
            return existingId;
        }

        try
        {
            var created = await _client.Customers.CreateCustomer(new CreateCustomerRequest
            {
                Customer = new CreateCustomer
                {
                    FirstName = string.IsNullOrWhiteSpace(firstName) ? "eShopOnWeb" : firstName,
                    LastName = string.IsNullOrWhiteSpace(lastName) ? "Customer" : lastName,
                    Email = email,
                    Reference = customerReference
                }
            }, cancellationToken);
            return created.Customer.Id!.Value.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is SdkException<CreateCustomerError> or JsonException)
        {
            // A concurrent request may have created this reference between our lookup above and this
            // call. Recover by re-reading rather than assuming failure means no customer exists.
            // (Also guards against CreateCustomerError's typed validation body - CustomerErrorResponse1 -
            // not matching Maxio's actual documented errors:[] shape for this operation; see final report.)
            var recovered = await FindCustomerIdAsync(customerReference, cancellationToken);
            if (recovered is not null)
            {
                return recovered;
            }

            _logger.LogWarning("Maxio create customer failed for reference {CustomerReference}: {ErrorMessage}", customerReference, ex.Message);
            throw new BillingProviderException("The billing provider could not create a customer record for this user.", ex);
        }
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(string providerCustomerId, string productHandle, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Subscriptions.CreateSubscription(new CreateSubscriptionRequest
            {
                Subscription = new CreateSubscription
                {
                    ProductHandle = productHandle,
                    CustomerId = ParseId(providerCustomerId),
                    // The sandbox site uses Relationship Invoicing with default_payment_collection_method
                    // "automatic" (confirmed via GET /site.json), which requires a card on file regardless
                    // of the product's own require_credit_card=false setting. Both plans this integration
                    // targets are deliberately payment-method-optional (integration plan §1.3/UC1), so
                    // every subscription created here explicitly requests remittance (invoice-based, no
                    // card required) collection rather than the site's automatic default.
                    PaymentCollectionMethod = CollectionMethod.Remittance
                }
            }, cancellationToken);
            return MapSubscription(response.Subscription!);
        }
        catch (SdkException<CreateSubscriptionError> ex)
        {
            throw ToSubscribeFailure(ex.Error, "create subscription");
        }
    }

    public async Task<SubscriptionDto> ReadSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Subscriptions.ReadSubscription(ParseId(subscriptionId), include: null, ct: cancellationToken);
            if (response.Subscription is null)
            {
                throw new SubscriptionNotFoundException(subscriptionId);
            }

            return MapSubscription(response.Subscription);
        }
        catch (SdkException<RawError> ex) when (ex.Error.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SubscriptionNotFoundException(subscriptionId);
        }
        catch (SdkException<RawError> ex)
        {
            throw WrapRawError(ex.Error, "read subscription");
        }
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListCustomerSubscriptionsAsync(string providerCustomerId, CancellationToken cancellationToken)
    {
        try
        {
            var responses = await _client.Customers.ListCustomerSubscriptions(ParseId(providerCustomerId), cancellationToken);
            return responses
                .Where(r => r.Subscription is not null)
                .Select(r => MapSubscription(r.Subscription!))
                .ToList();
        }
        catch (SdkException<RawError> ex)
        {
            throw WrapRawError(ex.Error, "list customer subscriptions");
        }
    }

    public async Task VerifyMeteredComponentAsync(CancellationToken cancellationToken)
    {
        Component? component;
        try
        {
            var response = await _client.Components.FindComponent(_settings.MeteredComponentHandle, cancellationToken);
            component = response.Component;
        }
        catch (SdkException<RawError> ex) when (ex.Error.StatusCode == HttpStatusCode.NotFound)
        {
            throw new MeteredComponentMisconfiguredException(_settings.MeteredComponentHandle, "no component with this handle exists on the site");
        }
        catch (SdkException<RawError> ex)
        {
            throw WrapRawError(ex.Error, "verify metered component");
        }

        if (component is null)
        {
            throw new MeteredComponentMisconfiguredException(_settings.MeteredComponentHandle, "no component with this handle exists on the site");
        }

        if (component.Kind != ComponentKind.MeteredComponent)
        {
            throw new MeteredComponentMisconfiguredException(_settings.MeteredComponentHandle, $"kind is '{component.Kind}', not metered");
        }

        if (component.ProductFamilyId != _settings.ProductFamilyId)
        {
            throw new MeteredComponentMisconfiguredException(_settings.MeteredComponentHandle, "component does not belong to the configured product family");
        }
    }

    public async Task<UsageDto> RecordUsageAsync(string subscriptionId, decimal quantity, string? memo, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SubscriptionComponents.CreateUsage(
                ParseId(subscriptionId),
                (double)_settings.MeteredComponentId,
                new CreateUsageRequest
                {
                    Usage = new CreateUsage { Quantity = quantity, Memo = memo }
                },
                cancellationToken);

            var usage = response.Usage;
            return new UsageDto(
                usage.Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                ReadQuantity(usage.Quantity),
                usage.Memo,
                usage.CreatedAt);
        }
        catch (SdkException<CreateUsageError> ex)
        {
            throw WrapError(ex.Error, "record usage");
        }
    }

    public async Task<UsageSummaryDto> GetUsageSummaryAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            var id = ParseId(subscriptionId);
            var componentTask = _client.SubscriptionComponents.ReadSubscriptionComponent(id, (double)_settings.MeteredComponentId, cancellationToken);
            var subscriptionTask = _client.Subscriptions.ReadSubscription(id, include: null, ct: cancellationToken);
            await Task.WhenAll(componentTask, subscriptionTask);

            var component = (await componentTask).Component;
            var subscription = (await subscriptionTask).Subscription;

            return new UsageSummaryDto(
                _settings.MeteredComponentHandle,
                (decimal)(component?.UnitBalance ?? 0),
                subscription?.CurrentPeriodStartedAt,
                subscription?.CurrentPeriodEndsAt);
        }
        catch (SdkException<ReadSubscriptionComponentError> ex)
        {
            throw WrapError(ex.Error, "get usage summary");
        }
        catch (SdkException<RawError> ex)
        {
            throw WrapRawError(ex.Error, "get usage summary");
        }
    }

    public async Task<PlanChangeQuoteDto> PreviewPlanChangeAsync(string subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken)
    {
        var current = await ReadSubscriptionAsync(subscriptionId, cancellationToken);

        if (timing == PlanChangeTiming.AtRenewal)
        {
            // UpdateSubscription (used to commit this timing) applies the new product at the normal
            // start of the next period with no proration - there is nothing to quote.
            return new PlanChangeQuoteDto(subscriptionId, current.ProductHandle, targetProductHandle, timing, 0m, current.NextAssessmentAt ?? DateTimeOffset.UtcNow);
        }

        try
        {
            var response = await _client.SubscriptionProducts.PreviewSubscriptionProductMigration(ParseId(subscriptionId), new SubscriptionMigrationPreviewRequest
            {
                Migration = new SubscriptionMigrationPreviewOptions
                {
                    ProductHandle = targetProductHandle,
                    PreservePeriod = true
                }
            }, cancellationToken);

            // payment_due_in_cents is the actual net amount charged right now (>0 on an upgrade; 0 on a
            // downgrade, since Maxio holds any excess as account credit rather than charging anything).
            // prorated_adjustment_in_cents alone is only the old-plan credit component and, taken by
            // itself, is negative even for upgrades - confirmed against the live sandbox (a basic->pro
            // upgrade returned a negative prorated_adjustment despite genuinely owing money at
            // payment_due). When nothing is due, credit_applied_in_cents (already negative) reports the
            // credit the customer is receiving instead.
            var migration = response.Migration;
            var netCents = migration.PaymentDueInCents is > 0 ? migration.PaymentDueInCents.Value : migration.CreditAppliedInCents ?? 0;
            var amount = netCents / 100m;
            return new PlanChangeQuoteDto(subscriptionId, current.ProductHandle, targetProductHandle, timing, amount, DateTimeOffset.UtcNow);
        }
        catch (SdkException<PreviewSubscriptionProductMigrationError> ex)
        {
            throw WrapError(ex.Error, "preview plan change");
        }
    }

    public async Task<SubscriptionDto> CommitPlanChangeAsync(string subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken)
    {
        var id = ParseId(subscriptionId);

        if (timing == PlanChangeTiming.AtRenewal)
        {
            try
            {
                var response = await _client.Subscriptions.UpdateSubscription(id, new UpdateSubscriptionRequest
                {
                    Subscription = new UpdateSubscription { ProductHandle = targetProductHandle }
                }, cancellationToken);
                return MapSubscription(response.Subscription!);
            }
            catch (SdkException<UpdateSubscriptionError> ex)
            {
                throw ToSubscribeFailure(ex.Error, "commit plan change");
            }
        }

        try
        {
            var response = await _client.SubscriptionProducts.MigrateSubscriptionProduct(id, new SubscriptionProductMigrationRequest
            {
                Migration = new SubscriptionProductMigration { ProductHandle = targetProductHandle, PreservePeriod = true }
            }, cancellationToken);
            return MapSubscription(response.Subscription!);
        }
        catch (SdkException<MigrateSubscriptionProductError> ex)
        {
            throw ToSubscribeFailure(ex.Error, "commit plan change");
        }
    }

    public async Task<SubscriptionDto> PauseAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SubscriptionStatus.PauseSubscription(ParseId(subscriptionId), body: null, ct: cancellationToken);
            return MapSubscription(response.Subscription!);
        }
        catch (SdkException<PauseSubscriptionError> ex)
        {
            throw WrapError(ex.Error, "pause subscription");
        }
    }

    public async Task<SubscriptionDto> ResumeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SubscriptionStatus.ResumeSubscription(ParseId(subscriptionId), calendarBillingResumptionCharge: null, ct: cancellationToken);
            return MapSubscription(response.Subscription!);
        }
        catch (SdkException<ResumeSubscriptionError> ex)
        {
            throw WrapError(ex.Error, "resume subscription");
        }
    }

    public async Task<SubscriptionDto> CancelAsync(string subscriptionId, CancelTiming timing, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SubscriptionStatus.CancelSubscription(ParseId(subscriptionId), new CancellationRequest
            {
                Subscription = new CancellationOptions
                {
                    CancellationMessage = reason,
                    CancelAtEndOfPeriod = timing == CancelTiming.EndOfPeriod
                }
            }, cancellationToken);
            return MapSubscription(response.Subscription!);
        }
        catch (SdkException<CancelSubscriptionApiError> ex)
        {
            throw WrapError(ex.Error, "cancel subscription");
        }
    }

    public async Task<SubscriptionDto> ReactivateAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.SubscriptionStatus.ReactivateSubscription(ParseId(subscriptionId), body: null, ct: cancellationToken);
            return MapSubscription(response.Subscription!);
        }
        catch (SdkException<ReactivateSubscriptionError> ex)
        {
            throw WrapError(ex.Error, "reactivate subscription");
        }
    }

    private static bool ContainsPaymentKeyword(string message) =>
        message.Contains("card", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("3-d secure", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("3d secure", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("3ds", StringComparison.OrdinalIgnoreCase);

    private Exception ToSubscribeFailure(CreateSubscriptionError error, string operation)
    {
        if (error.TryGetErrorListResponse1(out var validation))
        {
            if (validation.Errors.Any(ContainsPaymentKeyword))
            {
                return new PaymentVerificationRequiredException(validation.Errors);
            }

            _logger.LogWarning("Maxio {Operation} rejected the request: {Errors}", operation, string.Join("; ", validation.Errors));
            return new BillingProviderException("The billing provider rejected this request: " + string.Join(" ", validation.Errors));
        }

        return WrapError(error, operation);
    }

    private Exception ToSubscribeFailure(MigrateSubscriptionProductError error, string operation)
    {
        if (error.TryGetErrorListResponse1(out var validation))
        {
            if (validation.Errors.Any(ContainsPaymentKeyword))
            {
                return new PaymentVerificationRequiredException(validation.Errors);
            }

            _logger.LogWarning("Maxio {Operation} rejected the request: {Errors}", operation, string.Join("; ", validation.Errors));
            return new BillingProviderException("The billing provider rejected this request: " + string.Join(" ", validation.Errors));
        }

        return WrapError(error, operation);
    }

    private Exception ToSubscribeFailure(UpdateSubscriptionError error, string operation)
    {
        if (error.TryGetErrorListResponse1(out var validation))
        {
            if (validation.Errors.Any(ContainsPaymentKeyword))
            {
                return new PaymentVerificationRequiredException(validation.Errors);
            }

            _logger.LogWarning("Maxio {Operation} rejected the request: {Errors}", operation, string.Join("; ", validation.Errors));
            return new BillingProviderException("The billing provider rejected this request: " + string.Join(" ", validation.Errors));
        }

        return WrapError(error, operation);
    }

    private BillingProviderException WrapError(MaxioAdvancedBilling.Core.ErrorResponse.ApiError error, string operation)
    {
        if (error.TryGetRawError(out var raw))
        {
            return WrapRawError(raw, operation);
        }

        _logger.LogWarning("Maxio {Operation} returned an unhandled validation error", operation);
        return new BillingProviderException($"The billing provider could not complete '{operation}'.");
    }

    private BillingProviderException WrapRawError(RawError raw, string operation)
    {
        _logger.LogWarning("Maxio {Operation} failed: HTTP {StatusCode} {Body}", operation, (int)raw.StatusCode, SafeReadBody(raw));
        return new BillingProviderException($"The billing provider could not complete '{operation}'.");
    }

    private static string SafeReadBody(RawError raw)
    {
        try
        {
            return raw.ReadAsString();
        }
        catch
        {
            return "<unreadable body>";
        }
    }

    private static double ParseId(string id) => double.Parse(id, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static decimal ReadQuantity(MaxioAdvancedBilling.Models.AnyOf.Quantity1? quantity)
    {
        if (quantity is null)
        {
            return 0m;
        }

        if (quantity.TryGetDouble(out var d))
        {
            return (decimal)d;
        }

        if (quantity.TryGetString(out var s) && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static PlanDto MapPlan(Product product) => new(
        product.Handle ?? string.Empty,
        product.Name ?? string.Empty,
        (product.PriceInCents ?? 0) / 100m,
        (int)(product.Interval ?? 1),
        product.IntervalUnit?.Value ?? "month",
        product.RequireCreditCard ?? false);

    private static SubscriptionDto MapSubscription(MaxioSubscription subscription) => new(
        subscription.Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        subscription.Customer?.Reference ?? string.Empty,
        subscription.Product?.Handle ?? string.Empty,
        subscription.Product?.Name ?? string.Empty,
        (subscription.ProductPriceInCents ?? 0) / 100m,
        MapState(subscription.State),
        (subscription.ProductPriceInCents ?? 0) / 100m,
        subscription.NextAssessmentAt);

    private static DomainSubscriptionState MapState(MaxioSubscriptionState? state)
    {
        if (state is null)
        {
            return DomainSubscriptionState.Other;
        }

        return state.Value switch
        {
            "active" => DomainSubscriptionState.Active,
            "trialing" => DomainSubscriptionState.Trialing,
            "on_hold" => DomainSubscriptionState.OnHold,
            "past_due" => DomainSubscriptionState.PastDue,
            "canceled" => DomainSubscriptionState.Canceled,
            "unpaid" => DomainSubscriptionState.Unpaid,
            "expired" => DomainSubscriptionState.Expired,
            _ => DomainSubscriptionState.Other
        };
    }
}
