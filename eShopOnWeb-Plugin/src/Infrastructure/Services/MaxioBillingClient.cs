using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.ErrorResponse;
using MaxioAdvancedBilling.Core.Exceptions;
using MaxioAdvancedBilling.Errors;
using MaxioAdvancedBilling.Models;
using MaxioAdvancedBilling.Models.AnyOf;
using MaxioAdvancedBilling.Models.Enums;
using MaxioAdvancedBilling.Servers;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services;

// The single integration point with Maxio Advanced Billing. Implements the
// provider-agnostic IBillingClient behind a typed HttpClient (supplied by
// IHttpClientFactory) and the Maxio SDK. This class is the ONLY place that
// touches the provider, and the only place that resolves the outbound base URL
// (plan §2.2 / §2.3) — so retargeting prod / dev / mock is a config change here
// and never leaks outward.
public class MaxioBillingClient : IBillingClient
{
    private readonly MaxioAdvancedBillingClient _client;
    private readonly MaxioSettings _settings;
    private readonly IAppLogger<MaxioBillingClient> _logger;

    // Process-wide one-time validation of the metered component (plan UC2).
    private static readonly SemaphoreSlim MeteredGate = new(1, 1);
    private static volatile bool _meteredValidated;

    public MaxioBillingClient(System.Net.Http.HttpClient httpClient,
        IOptions<MaxioSettings> options,
        IAppLogger<MaxioBillingClient> logger)
    {
        _settings = options.Value;
        _logger = logger;

        // Resolve the outbound target once, here: an explicit Maxio:BaseUrl wins
        // verbatim, otherwise the host is derived from the subdomain (+ region).
        var resolvedBaseUrl = _settings.ResolveBaseUrl();
        httpClient.BaseAddress = new Uri(resolvedBaseUrl);

        var clientOptions = new MaxioAdvancedBillingClientOptions
        {
            Environment = _settings.IsEuRegion ? ServerEnvironment.Eu : ServerEnvironment.Us,
            BasicAuth = new BasicAuthCredentials
            {
                // Maxio HTTP Basic: username = API key, password = literal "x".
                Username = _settings.ApiKey ?? string.Empty,
                Password = "x"
            }
        };

        // Feed the resolved (already fully-substituted) URL into the SDK's server
        // template. Because it carries no {site} token it is used verbatim — this
        // is what makes an explicit Maxio:BaseUrl override win over the default.
        if (_settings.IsEuRegion)
        {
            clientOptions.Server.Production.Eu.BaseUrl = resolvedBaseUrl;
        }
        else
        {
            clientOptions.Server.Production.Us.BaseUrl = resolvedBaseUrl;
        }

        _client = new MaxioAdvancedBillingClient(httpClient, clientOptions);
    }

    // ----- UC1: plans & enrollment -------------------------------------------

    public Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default)
        => ExecAsync("list plans", async () =>
        {
            var products = await _client.ProductFamilies.ListProductsForProductFamily(
                FamilyIdParam(),
                dateField: null, filter: null, startDate: null, endDate: null,
                startDatetime: null, endDatetime: null, includeArchived: false, include: null,
                page: 1, perPage: 200, ct: cancellationToken);

            IReadOnlyList<SubscriptionPlan> plans = products
                .Select(p => MapPlan(p.Product))
                .Where(p => p.Id != 0)
                .ToList();
            return plans;
        });

    public async Task<SubscriptionPlan?> FindPlanAsync(string planHandle,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.Products.ReadProductByHandle(planHandle, cancellationToken);
            return MapPlan(response.Product);
        }
        catch (SdkException<RawError> ex) when ((int)ex.Error.StatusCode == 404)
        {
            return null;
        }
        catch (SdkException<RawError> ex)
        {
            throw Fail("find plan", ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("find plan", ex);
        }
    }

    public async Task<int> EnsureCustomerAsync(string reference, string email, string firstName, string lastName,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var existing = await FindCustomerIdAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        try
        {
            var created = await _client.Customers.CreateCustomer(new CreateCustomerRequest
            {
                Customer = new CreateCustomer
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    Reference = reference
                }
            }, cancellationToken);

            return created.Customer.Id
                   ?? throw new BillingProviderException("Maxio create customer returned no customer id.");
        }
        catch (SdkException<CreateCustomerError> ex)
        {
            // A concurrent create can race on the unique reference; re-read once.
            var raced = await FindCustomerIdAsync(reference, cancellationToken);
            if (raced is not null)
            {
                return raced.Value;
            }

            if (ex.Error.TryGetRawError(out var raw))
            {
                throw Fail("create customer", raw);
            }

            throw Fail("create customer", "the billing provider rejected the customer details.");
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("create customer", ex);
        }
    }

    public async Task<int?> FindCustomerIdAsync(string reference, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.Customers.ReadCustomerByReference(reference, cancellationToken);
            return response.Customer.Id;
        }
        catch (SdkException<RawError> ex) when ((int)ex.Error.StatusCode == 404)
        {
            return null;
        }
        catch (SdkException<RawError> ex)
        {
            throw Fail("look up customer", ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("look up customer", ex);
        }
    }

    public async Task<CustomerSubscription> SubscribeAsync(int customerId, string planHandle,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.Subscriptions.CreateSubscription(new CreateSubscriptionRequest
            {
                Subscription = new CreateSubscription
                {
                    CustomerId = customerId,
                    ProductHandle = planHandle,
                    // Bill via invoice/remittance rather than automatic card capture, so the
                    // demo enrolls without a payment method or 3-DS (plan UC0/UC1). The charge
                    // accrues to the subscription's invoice.
                    PaymentCollectionMethod = CollectionMethod.Remittance
                }
            }, cancellationToken);

            return MapSubscription(response.Subscription);
        }
        catch (SdkException<CreateSubscriptionError> ex)
        {
            throw Fail("create subscription",
                ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("create subscription", ex);
        }
    }

    public Task<IReadOnlyList<CustomerSubscription>> ListCustomerSubscriptionsAsync(int customerId,
        CancellationToken cancellationToken = default)
        => ExecAsync("list customer subscriptions", async () =>
        {
            var subscriptions = await _client.Customers.ListCustomerSubscriptions(customerId, cancellationToken);
            IReadOnlyList<CustomerSubscription> mapped = subscriptions
                .Select(s => MapSubscription(s.Subscription))
                .ToList();
            return mapped;
        });

    public async Task<CustomerSubscription?> GetSubscriptionAsync(int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.Subscriptions.ReadSubscription(subscriptionId, include: null,
                ct: cancellationToken);
            return MapSubscription(response.Subscription);
        }
        catch (SdkException<RawError> ex) when ((int)ex.Error.StatusCode == 404)
        {
            return null;
        }
        catch (SdkException<RawError> ex)
        {
            throw Fail("read subscription", ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("read subscription", ex);
        }
    }

    // ----- UC2: pay-as-you-go usage ------------------------------------------

    public async Task EnsureMeteredComponentAsync(CancellationToken cancellationToken = default)
    {
        if (_meteredValidated)
        {
            return;
        }

        EnsureConfigured();
        await MeteredGate.WaitAsync(cancellationToken);
        try
        {
            if (_meteredValidated)
            {
                return;
            }

            Component component;
            try
            {
                var response = await _client.Components.FindComponent(_settings.MeteredComponentHandle,
                    cancellationToken);
                component = response.Component;
            }
            catch (SdkException<RawError> ex) when ((int)ex.Error.StatusCode == 404)
            {
                throw new BillingConfigurationException(
                    $"The configured metered component '{_settings.MeteredComponentHandle}' does not resolve on " +
                    "the billing provider. Re-check the seed (plan UC0).");
            }
            catch (SdkException<RawError> ex)
            {
                throw Fail("validate metered component", ex.Error);
            }

            var kind = component.Kind?.Value;
            if (!string.Equals(kind, ComponentKind.MeteredComponent.Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new BillingConfigurationException(
                    $"The configured component '{_settings.MeteredComponentHandle}' is of kind '{kind ?? "unknown"}' " +
                    "but must be metered. Archive it and recreate it as metered (plan UC0).");
            }

            if (_settings.ProductFamilyId > 0 && component.ProductFamilyId is int familyId &&
                familyId != _settings.ProductFamilyId)
            {
                throw new BillingConfigurationException(
                    $"The metered component '{_settings.MeteredComponentHandle}' belongs to product family " +
                    $"{familyId}, not the configured family {_settings.ProductFamilyId}. Recreate it on the " +
                    "correct family (plan UC0).");
            }

            _meteredValidated = true;
        }
        finally
        {
            MeteredGate.Release();
        }
    }

    public async Task<int> RecordUsageAsync(int subscriptionId, int quantity, string? memo,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.SubscriptionComponents.CreateUsage(
                SubscriptionIdOrReference.Int(subscriptionId),
                MeteredComponentId(),
                new CreateUsageRequest
                {
                    Usage = new CreateUsage
                    {
                        Quantity = quantity,
                        Memo = memo
                    }
                }, cancellationToken);

            if (response.Usage.Quantity is { } recorded && recorded.TryGetInt(out var value))
            {
                return value;
            }

            return quantity;
        }
        catch (SdkException<CreateUsageError> ex)
        {
            throw Fail("record usage",
                ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("record usage", ex);
        }
    }

    public Task<int?> GetPeriodToDateUsageAsync(int subscriptionId, CancellationToken cancellationToken = default)
        => ExecAsync<int?>("read usage total", async () =>
        {
            // Constrain the running total to the current billing period.
            var subscription = await _client.Subscriptions.ReadSubscription(subscriptionId, include: null,
                ct: cancellationToken);
            var since = subscription.Subscription?.CurrentPeriodStartedAt;

            var usages = await _client.SubscriptionComponents.ListUsages(
                SubscriptionIdOrReference.Int(subscriptionId),
                MeteredComponentId(),
                sinceId: null, maxId: null, sinceDate: since, untilDate: null,
                page: 1, perPage: 200, ct: cancellationToken);

            var total = 0;
            foreach (var usage in usages)
            {
                total += ReadQuantity(usage.Usage.Quantity);
            }

            return total;
        });

    // ----- UC3: plan change --------------------------------------------------

    public async Task<PlanChangePreview> PreviewPlanChangeAsync(int subscriptionId, string fromPlanHandle,
        string toPlanHandle, bool applyNow, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (applyNow)
        {
            try
            {
                var response = await _client.SubscriptionProducts.PreviewSubscriptionProductMigration(subscriptionId,
                    new SubscriptionMigrationPreviewRequest
                    {
                        Migration = new SubscriptionMigrationPreviewOptions
                        {
                            ProductHandle = toPlanHandle,
                            PreservePeriod = true
                        }
                    }, cancellationToken);

                var m = response.Migration;
                return new PlanChangePreview
                {
                    FromPlanHandle = fromPlanHandle,
                    ToPlanHandle = toPlanHandle,
                    ApplyNow = true,
                    ProratedAdjustmentInCents = m.ProratedAdjustmentInCents ?? 0,
                    ChargeInCents = m.ChargeInCents ?? 0,
                    CreditAppliedInCents = m.CreditAppliedInCents ?? 0,
                    PaymentDueInCents = m.PaymentDueInCents ?? 0,
                    EffectiveDate = null
                };
            }
            catch (SdkException<PreviewSubscriptionProductMigrationError> ex)
            {
                throw Fail("preview plan change",
                    ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
            }
            catch (Exception ex) when (Wrappable(ex))
            {
                throw Fail("preview plan change", ex);
            }
        }

        // "At renewal": no proration. The amount effective from the next period is
        // simply the target plan price; nothing is due now.
        var targetPlan = await FindPlanAsync(toPlanHandle, cancellationToken);
        var subscription = await GetSubscriptionAsync(subscriptionId, cancellationToken);
        return new PlanChangePreview
        {
            FromPlanHandle = fromPlanHandle,
            ToPlanHandle = toPlanHandle,
            ApplyNow = false,
            ChargeInCents = targetPlan?.PriceInCents ?? 0,
            PaymentDueInCents = 0,
            EffectiveDate = subscription?.CurrentPeriodEndsAt
        };
    }

    public async Task<CustomerSubscription> ChangePlanAsync(int subscriptionId, string toPlanHandle, bool applyNow,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (applyNow)
        {
            try
            {
                var response = await _client.SubscriptionProducts.MigrateSubscriptionProduct(subscriptionId,
                    new SubscriptionProductMigrationRequest
                    {
                        Migration = new SubscriptionProductMigration
                        {
                            ProductHandle = toPlanHandle,
                            PreservePeriod = true
                        }
                    }, cancellationToken);

                return MapSubscription(response.Subscription);
            }
            catch (SdkException<MigrateSubscriptionProductError> ex)
            {
                throw Fail("change plan",
                    ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
            }
            catch (Exception ex) when (Wrappable(ex))
            {
                throw Fail("change plan", ex);
            }
        }

        // Delayed product change: takes effect at the next renewal.
        try
        {
            var response = await _client.Subscriptions.UpdateSubscription(subscriptionId,
                new UpdateSubscriptionRequest
                {
                    Subscription = new UpdateSubscription
                    {
                        ProductHandle = toPlanHandle,
                        ProductChangeDelayed = true
                    }
                }, cancellationToken);

            return MapSubscription(response.Subscription);
        }
        catch (SdkException<UpdateSubscriptionError> ex)
        {
            if (ex.Error.TryGetRawError(out var raw))
            {
                throw Fail("schedule plan change", raw);
            }

            throw Fail("schedule plan change", "the billing provider rejected the scheduled plan change.");
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("schedule plan change", ex);
        }
    }

    // ----- UC4: lifecycle ----------------------------------------------------

    public async Task<CustomerSubscription> PauseAsync(int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.SubscriptionStatus.PauseSubscription(subscriptionId, body: null,
                ct: cancellationToken);
            return MapSubscription(response.Subscription);
        }
        catch (SdkException<PauseSubscriptionError> ex)
        {
            throw Fail("pause subscription",
                ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("pause subscription", ex);
        }
    }

    public async Task<CustomerSubscription> ResumeAsync(int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.SubscriptionStatus.ResumeSubscription(subscriptionId,
                calendarBillingResumptionCharge: null, ct: cancellationToken);
            return MapSubscription(response.Subscription);
        }
        catch (SdkException<ResumeSubscriptionError> ex)
        {
            throw Fail("resume subscription",
                ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("resume subscription", ex);
        }
    }

    public async Task<CustomerSubscription> CancelAsync(int subscriptionId, bool endOfPeriod, string? reason,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (endOfPeriod)
        {
            try
            {
                await _client.SubscriptionStatus.InitiateDelayedCancellation(subscriptionId,
                    new CancellationRequest
                    {
                        Subscription = new CancellationOptions
                        {
                            CancellationMessage = reason,
                            CancelAtEndOfPeriod = true
                        }
                    }, cancellationToken);
            }
            catch (SdkException<InitiateDelayedCancellationError> ex)
            {
                throw Fail("schedule cancellation",
                    ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
            }
            catch (Exception ex) when (Wrappable(ex))
            {
                throw Fail("schedule cancellation", ex);
            }

            // Delayed cancellation returns its own payload; re-read the subscription
            // so callers observe the authoritative (pending-cancellation) state.
            var refreshed = await GetSubscriptionAsync(subscriptionId, cancellationToken);
            return refreshed ?? throw new BillingProviderException(
                $"Subscription {subscriptionId} could not be re-read after scheduling cancellation.");
        }

        try
        {
            var response = await _client.SubscriptionStatus.CancelSubscription(subscriptionId,
                new CancellationRequest
                {
                    Subscription = new CancellationOptions
                    {
                        CancellationMessage = reason,
                        CancelAtEndOfPeriod = false
                    }
                }, cancellationToken);
            return MapSubscription(response.Subscription);
        }
        catch (SdkException<CancelSubscriptionApiError> ex)
        {
            if (ex.Error.TryGetRawError(out var raw))
            {
                throw Fail("cancel subscription", raw);
            }

            throw Fail("cancel subscription", "the billing provider rejected the cancellation.");
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("cancel subscription", ex);
        }
    }

    public async Task<CustomerSubscription> ReactivateAsync(int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var response = await _client.SubscriptionStatus.ReactivateSubscription(subscriptionId, body: null,
                ct: cancellationToken);
            return MapSubscription(response.Subscription);
        }
        catch (SdkException<ReactivateSubscriptionError> ex)
        {
            throw Fail("reactivate subscription",
                ex.Error.TryGetErrorListResponse1(out var errors) ? errors.Errors : null, ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail("reactivate subscription", ex);
        }
    }

    // ----- mapping helpers ---------------------------------------------------

    private static SubscriptionPlan MapPlan(Product product) => new()
    {
        Id = product.Id ?? 0,
        Handle = product.Handle ?? string.Empty,
        Name = product.Name ?? string.Empty,
        PriceInCents = product.PriceInCents ?? 0,
        Interval = product.Interval ?? 1,
        IntervalUnit = product.IntervalUnit?.Value ?? "month"
    };

    private static CustomerSubscription MapSubscription(Subscription? subscription)
    {
        if (subscription is null)
        {
            throw new BillingProviderException("The billing provider returned an empty subscription payload.");
        }

        return new CustomerSubscription
        {
            Id = subscription.Id ?? 0,
            CustomerId = subscription.Customer?.Id ?? 0,
            CustomerReference = subscription.Customer?.Reference,
            PlanHandle = subscription.Product?.Handle,
            PlanName = subscription.Product?.Name,
            PriceInCents = subscription.ProductPriceInCents,
            State = subscription.State?.Value ?? string.Empty,
            CurrentPeriodEndsAt = subscription.CurrentPeriodEndsAt,
            CancelAtEndOfPeriod = subscription.CancelAtEndOfPeriod ?? false,
            CanceledAt = subscription.CanceledAt,
            DelayedCancelAt = subscription.DelayedCancelAt,
            NextPlanHandle = subscription.NextProductHandle
        };
    }

    private static int ReadQuantity(Quantity1? quantity)
    {
        if (quantity is null)
        {
            return 0;
        }

        if (quantity.TryGetInt(out var intValue))
        {
            return intValue;
        }

        if (quantity.TryGetString(out var stringValue) &&
            int.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private ComponentIdModel MeteredComponentId() =>
        _settings.MeteredComponentId > 0
            ? ComponentIdModel.Int(_settings.MeteredComponentId)
            : ComponentIdModel.String($"handle:{_settings.MeteredComponentHandle}");

    private string FamilyIdParam() =>
        _settings.ProductFamilyId > 0
            ? _settings.ProductFamilyId.ToString(CultureInfo.InvariantCulture)
            : $"handle:{_settings.ProductFamilyHandle}";

    // ----- error handling ----------------------------------------------------

    // Fails fast (no network) when the API key is absent, so an unconfigured
    // environment surfaces a clear configuration error instead of attempting an
    // outbound call — and the best-effort order-usage hook becomes a no-op.
    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new BillingConfigurationException(
                "Maxio is not configured: set Maxio:ApiKey via .NET user-secrets (see plan §5).");
        }
    }

    // Wraps read/list operations that only ever surface SdkException<RawError>.
    private async Task<T> ExecAsync<T>(string action, Func<Task<T>> call)
    {
        EnsureConfigured();
        try
        {
            return await call();
        }
        catch (SdkException<RawError> ex)
        {
            throw Fail(action, ex.Error);
        }
        catch (Exception ex) when (Wrappable(ex))
        {
            throw Fail(action, ex);
        }
    }

    private BillingProviderException Fail(string action, RawError raw)
    {
        var body = Summarize(raw.ReadAsString());
        var message = string.IsNullOrEmpty(body)
            ? $"Maxio {action} failed with HTTP {(int)raw.StatusCode} ({raw.StatusCode})."
            : $"Maxio {action} failed with HTTP {(int)raw.StatusCode}: {body}";
        _logger.LogWarning(message);
        return new BillingProviderException(message);
    }

    private BillingProviderException Fail(string action, IReadOnlyList<string>? messages, ApiError fallback)
    {
        if (messages is { Count: > 0 })
        {
            var message = $"Maxio {action} failed: {string.Join("; ", messages)}";
            _logger.LogWarning(message);
            return new BillingProviderException(message);
        }

        if (fallback.TryGetRawError(out var raw))
        {
            return Fail(action, raw);
        }

        return Fail(action, "the billing provider rejected the request.");
    }

    private BillingProviderException Fail(string action, string detail)
    {
        var message = $"Maxio {action} failed: {detail}";
        _logger.LogWarning(message);
        return new BillingProviderException(message);
    }

    private BillingProviderException Fail(string action, Exception ex)
    {
        var message = $"Maxio {action} failed: {ex.Message}";
        _logger.LogWarning(message);
        return new BillingProviderException(message, ex);
    }

    // Domain exceptions we raise deliberately, and cancellation, must pass through.
    private static bool Wrappable(Exception ex) =>
        ex is not (OperationCanceledException or BillingProviderException or BillingConfigurationException);

    private static string Summarize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var collapsed = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return collapsed.Length > 500 ? collapsed[..500] + "…" : collapsed;
    }
}
