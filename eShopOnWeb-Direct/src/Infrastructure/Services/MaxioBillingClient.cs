using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// The single integration point with Maxio Advanced Billing. Implements the provider-agnostic
/// <see cref="IBillingClient"/> by talking to Maxio over plain HTTP (no SDK), normalizing the
/// results, and throwing <see cref="BillingProviderException"/> on failure. The outbound base URL
/// and HTTP Basic credentials are configured on the injected typed <see cref="HttpClient"/> from
/// <see cref="MaxioSettings"/> in the composition root (plan §2.3 / §4.3), so retargeting
/// prod / dev / mock never leaks beyond configuration.
/// </summary>
public class MaxioBillingClient : IBillingClient
{
    private readonly HttpClient _http;
    private readonly MaxioSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public MaxioBillingClient(HttpClient httpClient, IOptions<MaxioSettings> settings)
    {
        _http = httpClient;
        _settings = settings.Value;
    }

    // ---- Plans (UC1 step 1) -------------------------------------------------

    public async Task<IReadOnlyCollection<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        var wrappers = await SendAsync<List<ProductWrapper>>(
            HttpMethod.Get, $"product_families/{_settings.ProductFamilyId}/products.json", null, cancellationToken)
            ?? new List<ProductWrapper>();

        return wrappers
            .Select(w => w.Product)
            .Where(p => p is not null && p!.ArchivedAt is null)
            .Select(p => MapPlan(p!))
            .ToList();
    }

    // ---- Customers (UC1 step 3) --------------------------------------------

    public async Task<int?> FindCustomerIdByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var (status, wrapper) = await TrySendAsync<CustomerWrapper>(
            HttpMethod.Get, $"customers/lookup.json?reference={Uri.EscapeDataString(reference)}", null, cancellationToken);

        if (status == HttpStatusCode.NotFound)
        {
            return null;
        }

        return wrapper?.Customer?.Id;
    }

    public async Task<int> EnsureCustomerAsync(string reference, string email, string? firstName, string? lastName, CancellationToken cancellationToken = default)
    {
        var existing = await FindCustomerIdByReferenceAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        // Maxio requires first_name, last_name and email. Derive sensible defaults from the identity
        // we have (the eShopOnWeb user only carries an email/username).
        var first = string.IsNullOrWhiteSpace(firstName) ? DeriveFirstName(email, reference) : firstName!;
        var last = string.IsNullOrWhiteSpace(lastName) ? "eShopOnWeb Customer" : lastName!;

        var body = new { customer = new { first_name = first, last_name = last, email, reference } };
        var wrapper = await SendAsync<CustomerWrapper>(HttpMethod.Post, "customers.json", body, cancellationToken);

        if (wrapper?.Customer is null)
        {
            throw new BillingProviderException("Maxio did not return a customer after creation.");
        }

        return wrapper.Customer.Id;
    }

    public async Task<IReadOnlyCollection<CustomerSubscription>> GetSubscriptionsForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var wrappers = await SendAsync<List<SubscriptionWrapper>>(
            HttpMethod.Get, $"customers/{customerId}/subscriptions.json", null, cancellationToken)
            ?? new List<SubscriptionWrapper>();

        return wrappers
            .Select(w => w.Subscription)
            .Where(s => s is not null)
            .Select(s => MapSubscription(s!))
            .ToList();
    }

    public async Task<CustomerSubscription> GetSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        var wrapper = await SendAsync<SubscriptionWrapper>(
            HttpMethod.Get, $"subscriptions/{subscriptionId}.json", null, cancellationToken);
        return RequireSubscription(wrapper);
    }

    // ---- Subscribe (UC1 step 4) --------------------------------------------

    public async Task<CustomerSubscription> CreateSubscriptionAsync(int customerId, string productHandle, CancellationToken cancellationToken = default)
    {
        // The demo plans require no payment method (plan UC0). Use remittance (invoice) collection so the
        // subscription is created without capturing a card — otherwise Maxio would try to auto-charge the
        // first period and fail with "no payment method on file".
        var body = new
        {
            subscription = new
            {
                customer_id = customerId,
                product_handle = productHandle,
                payment_collection_method = "remittance"
            }
        };
        var wrapper = await SendAsync<SubscriptionWrapper>(HttpMethod.Post, "subscriptions.json", body, cancellationToken);
        return RequireSubscription(wrapper);
    }

    // ---- Usage (UC2) --------------------------------------------------------

    public async Task<BillingComponent> GetMeteredComponentAsync(CancellationToken cancellationToken = default)
    {
        var wrapper = await SendAsync<ComponentWrapper>(
            HttpMethod.Get, $"components/lookup.json?handle={Uri.EscapeDataString(_settings.MeteredComponentHandle)}", null, cancellationToken);

        if (wrapper?.Component is null)
        {
            throw new BillingProviderException(
                $"The configured metered component handle '{_settings.MeteredComponentHandle}' did not resolve on Maxio. " +
                "Check the seed (plan UC0) and the 'Maxio:MeteredComponentHandle' configuration.");
        }

        return new BillingComponent
        {
            Id = wrapper.Component.Id,
            Handle = wrapper.Component.Handle ?? _settings.MeteredComponentHandle,
            Name = wrapper.Component.Name ?? string.Empty,
            Kind = MapComponentKind(wrapper.Component.Kind)
        };
    }

    public async Task RecordUsageAsync(int subscriptionId, int quantity, string? memo, CancellationToken cancellationToken = default)
    {
        var body = new { usage = new { quantity, memo } };
        await SendAsync<UsageRecordResponse>(
            HttpMethod.Post,
            $"subscriptions/{subscriptionId}/components/{_settings.MeteredComponentId}/usages.json",
            body, cancellationToken);
    }

    public async Task<int> GetUsageTotalAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        var wrapper = await SendAsync<SubscriptionComponentWrapper>(
            HttpMethod.Get,
            $"subscriptions/{subscriptionId}/components/{_settings.MeteredComponentId}.json",
            null, cancellationToken);

        if (wrapper?.Component is null)
        {
            throw new BillingProviderException(
                $"Maxio did not return the metered component for subscription {subscriptionId}.");
        }

        return wrapper.Component.UnitBalance;
    }

    // ---- Plan change (UC3) --------------------------------------------------

    public async Task<ProrationPreview> PreviewPlanChangeAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken = default)
    {
        // An "at renewal" change is a delayed product change with no proration: the new plan price
        // simply takes effect next period. Maxio's migration preview only prices immediate proration,
        // so there is nothing to charge now — report a zero preview.
        if (timing == PlanChangeTiming.AtRenewal)
        {
            return new ProrationPreview { TargetProductHandle = targetProductHandle, Timing = timing };
        }

        var body = new { migration = new { product_handle = targetProductHandle, preserve_period = true } };
        var wrapper = await SendAsync<MigrationPreviewWrapper>(
            HttpMethod.Post, $"subscriptions/{subscriptionId}/migrations/preview.json", body, cancellationToken);

        if (wrapper?.Migration is null)
        {
            throw new BillingProviderException($"Maxio did not return a migration preview for subscription {subscriptionId}.");
        }

        var m = wrapper.Migration;
        return new ProrationPreview
        {
            TargetProductHandle = targetProductHandle,
            Timing = timing,
            ProratedAdjustmentInCents = m.ProratedAdjustmentInCents,
            ChargeInCents = m.ChargeInCents,
            PaymentDueInCents = m.PaymentDueInCents,
            CreditAppliedInCents = m.CreditAppliedInCents
        };
    }

    public async Task<CustomerSubscription> ChangePlanAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken = default)
    {
        if (timing == PlanChangeTiming.AtRenewal)
        {
            // Delayed product change at next renewal, no proration.
            var updateBody = new { subscription = new { product_handle = targetProductHandle, product_change_delayed = true } };
            var updated = await SendAsync<SubscriptionWrapper>(
                HttpMethod.Put, $"subscriptions/{subscriptionId}.json", updateBody, cancellationToken);
            return RequireSubscription(updated);
        }

        // Immediate prorated change: preserve the billing period and issue a prorated charge.
        var body = new { migration = new { product_handle = targetProductHandle, preserve_period = true } };
        var wrapper = await SendAsync<SubscriptionWrapper>(
            HttpMethod.Post, $"subscriptions/{subscriptionId}/migrations.json", body, cancellationToken);
        return RequireSubscription(wrapper);
    }

    // ---- Lifecycle (UC4) ----------------------------------------------------

    public async Task<CustomerSubscription> ApplyLifecycleActionAsync(int subscriptionId, SubscriptionLifecycleAction action, string? reason, CancellationToken cancellationToken = default)
    {
        switch (action)
        {
            case SubscriptionLifecycleAction.Pause:
            {
                var wrapper = await SendAsync<SubscriptionWrapper>(
                    HttpMethod.Post, $"subscriptions/{subscriptionId}/hold.json", new { }, cancellationToken);
                return RequireSubscription(wrapper);
            }
            case SubscriptionLifecycleAction.Resume:
            {
                var wrapper = await SendAsync<SubscriptionWrapper>(
                    HttpMethod.Post, $"subscriptions/{subscriptionId}/resume.json", null, cancellationToken);
                return RequireSubscription(wrapper);
            }
            case SubscriptionLifecycleAction.Cancel:
            {
                var body = new { subscription = new { cancellation_message = reason } };
                var wrapper = await SendAsync<SubscriptionWrapper>(
                    HttpMethod.Delete, $"subscriptions/{subscriptionId}.json", body, cancellationToken);
                return RequireSubscription(wrapper);
            }
            case SubscriptionLifecycleAction.CancelAtEndOfPeriod:
            {
                // delayed_cancel returns only a { "message": ... } acknowledgement, so re-read the
                // authoritative subscription state to return it.
                var body = new { subscription = new { cancellation_message = reason } };
                await SendAsync<DelayedCancellationResponse>(
                    HttpMethod.Post, $"subscriptions/{subscriptionId}/delayed_cancel.json", body, cancellationToken);
                return await GetSubscriptionAsync(subscriptionId, cancellationToken);
            }
            case SubscriptionLifecycleAction.Reactivate:
            {
                var wrapper = await SendAsync<SubscriptionWrapper>(
                    HttpMethod.Put, $"subscriptions/{subscriptionId}/reactivate.json", new { }, cancellationToken);
                return RequireSubscription(wrapper);
            }
            default:
                throw new BillingProviderException($"Unsupported lifecycle action '{action}'.");
        }
    }

    // ---- HTTP plumbing ------------------------------------------------------

    private async Task<T?> SendAsync<T>(HttpMethod method, string relativeUri, object? body, CancellationToken cancellationToken)
    {
        var (_, result) = await TrySendAsync<T>(method, relativeUri, body, cancellationToken);
        return result;
    }

    /// <summary>
    /// Sends a request and deserializes the JSON body. On a non-success status it throws
    /// <see cref="BillingProviderException"/>, EXCEPT it returns the status (with a null body) so
    /// callers that treat 404 as "not found" (e.g. customer lookup) can handle it without an exception.
    /// </summary>
    private async Task<(HttpStatusCode Status, T? Body)> TrySendAsync<T>(HttpMethod method, string relativeUri, object? body, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
        {
            // The typed client's BaseAddress is only set when Maxio is configured (see the composition
            // roots). Failing here with a clear, typed error keeps unrelated flows (e.g. checkout, which
            // best-effort records usage) from breaking when the integration isn't configured.
            throw new BillingProviderException(
                "Maxio is not configured. Set 'Maxio:ApiKey' and either 'Maxio:BaseUrl' or 'Maxio:Subdomain' (see plan §5).");
        }

        using var request = new HttpRequestMessage(method, relativeUri);
        if (body is not null)
        {
            // Pass the runtime type explicitly: serializing an anonymous object declared as `object`
            // via the generic overload would use typeof(object) and emit an empty "{}".
            request.Content = JsonContent.Create(body, body.GetType(), mediaType: null, options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new BillingProviderException($"Could not reach the billing provider: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BillingProviderException("The billing provider request timed out.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (HttpStatusCode.NotFound, default);
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadErrorMessageAsync(response, cancellationToken);
                throw new BillingProviderException(
                    $"Billing provider returned {(int)response.StatusCode} ({response.ReasonPhrase}): {message}",
                    (int)response.StatusCode);
            }

            if (response.Content.Headers.ContentLength == 0)
            {
                return (response.StatusCode, default);
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                return (response.StatusCode, value);
            }
            catch (JsonException ex)
            {
                throw new BillingProviderException($"Could not parse the billing provider response: {ex.Message}", ex);
            }
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string raw;
        try
        {
            raw = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return response.ReasonPhrase ?? "unknown error";
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return response.ReasonPhrase ?? "unknown error";
        }

        // Maxio error bodies are usually { "errors": [ ... ] } or { "errors": { field: msg } }, but can
        // also be a bare JSON string. Extract something human-readable, falling back to the raw body.
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString() ?? raw;
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var errors))
            {
                return errors.ValueKind switch
                {
                    JsonValueKind.Array => string.Join("; ", errors.EnumerateArray().Select(e => e.ToString())),
                    JsonValueKind.Object => string.Join("; ", errors.EnumerateObject().Select(p => $"{p.Name}: {p.Value}")),
                    _ => errors.ToString()
                };
            }
        }
        catch (JsonException)
        {
            // not JSON — fall through
        }

        return raw;
    }

    // ---- Mapping ------------------------------------------------------------

    private static CustomerSubscription RequireSubscription(SubscriptionWrapper? wrapper)
    {
        if (wrapper?.Subscription is null)
        {
            throw new BillingProviderException("Maxio did not return a subscription.");
        }

        return MapSubscription(wrapper.Subscription);
    }

    private static SubscriptionPlan MapPlan(MaxioProduct p) => new()
    {
        Id = p.Id,
        Handle = p.Handle ?? string.Empty,
        Name = p.Name ?? string.Empty,
        Description = p.Description,
        PriceInCents = p.PriceInCents,
        Interval = p.Interval,
        IntervalUnit = p.IntervalUnit ?? string.Empty,
        ProductFamilyHandle = p.ProductFamily?.Handle ?? string.Empty
    };

    private static CustomerSubscription MapSubscription(MaxioSubscription s) => new()
    {
        Id = s.Id,
        State = MapState(s.State),
        CustomerId = s.Customer?.Id ?? 0,
        CustomerReference = s.Customer?.Reference,
        ProductHandle = s.Product?.Handle ?? string.Empty,
        ProductName = s.Product?.Name ?? string.Empty,
        ProductPriceInCents = s.ProductPriceInCents,
        Interval = s.Product?.Interval ?? 0,
        IntervalUnit = s.Product?.IntervalUnit ?? string.Empty,
        CurrentPeriodEndsAt = s.CurrentPeriodEndsAt,
        NextAssessmentAt = s.NextAssessmentAt,
        CancelAtEndOfPeriod = s.CancelAtEndOfPeriod ?? false,
        CanceledAt = s.CanceledAt,
        DelayedCancelAt = s.DelayedCancelAt,
        AutomaticallyResumeAt = s.AutomaticallyResumeAt
    };

    private static SubscriptionState MapState(string? state) => state switch
    {
        "active" => SubscriptionState.Active,
        "trialing" => SubscriptionState.Trialing,
        "on_hold" => SubscriptionState.OnHold,
        "paused" => SubscriptionState.Paused,
        "canceled" => SubscriptionState.Canceled,
        "past_due" => SubscriptionState.PastDue,
        "suspended" => SubscriptionState.Suspended,
        "expired" => SubscriptionState.Expired,
        "unpaid" => SubscriptionState.Unpaid,
        "trial_ended" => SubscriptionState.TrialEnded,
        "pending" => SubscriptionState.Pending,
        "awaiting_signup" => SubscriptionState.AwaitingSignup,
        _ => SubscriptionState.Unknown
    };

    private static BillingComponentKind MapComponentKind(string? kind) => kind switch
    {
        "metered_component" => BillingComponentKind.Metered,
        "quantity_based_component" => BillingComponentKind.QuantityBased,
        "on_off_component" => BillingComponentKind.OnOff,
        "prepaid_usage_component" => BillingComponentKind.PrepaidUsage,
        "event_based_component" => BillingComponentKind.EventBased,
        _ => BillingComponentKind.Unknown
    };

    private static string DeriveFirstName(string email, string reference)
    {
        var source = !string.IsNullOrWhiteSpace(email) ? email : reference;
        var at = source.IndexOf('@');
        return at > 0 ? source[..at] : source;
    }

    // Minimal shapes for responses whose bodies we don't otherwise need to map.
    private sealed class UsageRecordResponse { }
    private sealed class DelayedCancellationResponse { }
}
