using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The single, provider-agnostic seam to the billing provider. Exactly one Infrastructure class
/// implements this (talking to the provider over HTTP); nothing else in the application touches
/// the provider directly. The concrete implementation owns all provider configuration — including
/// which product family / metered component the integration is bound to and the outbound base URL
/// (see plan §2.3) — so ApplicationCore stays free of provider details. Every method throws
/// <see cref="Exceptions.BillingProviderException"/> when the provider rejects a request or is unreachable.
/// </summary>
public interface IBillingClient
{
    /// <summary>Lists the recurring plans available for customers to subscribe to (UC1 step 1).</summary>
    Task<IReadOnlyCollection<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a provider-side customer exists for the given stable reference (the eShopOnWeb
    /// user's email/username), creating one if necessary. Idempotent on the reference. Returns
    /// the provider-side customer id.
    /// </summary>
    Task<int> EnsureCustomerAsync(string reference, string email, string? firstName, string? lastName, CancellationToken cancellationToken = default);

    /// <summary>Returns the provider-side customer id for a reference, or null if none exists.</summary>
    Task<int?> FindCustomerIdByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Lists a customer's subscriptions.</summary>
    Task<IReadOnlyCollection<CustomerSubscription>> GetSubscriptionsForCustomerAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Reads a single subscription (authoritative provider state).</summary>
    Task<CustomerSubscription> GetSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Enrolls an existing customer in a plan (UC1 step 4).</summary>
    Task<CustomerSubscription> CreateSubscriptionAsync(int customerId, string productHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the configured metered ("pay-as-you-go") component and returns it, so the caller can
    /// verify it really is of metered kind before recording usage (UC2 precondition). Throws if the
    /// configured handle does not resolve.
    /// </summary>
    Task<BillingComponent> GetMeteredComponentAsync(CancellationToken cancellationToken = default);

    /// <summary>Records usage against the subscription's configured metered component (UC2 step 2).</summary>
    Task RecordUsageAsync(int subscriptionId, int quantity, string? memo, CancellationToken cancellationToken = default);

    /// <summary>Reads the running period-to-date billable unit total for the configured metered component (UC2 step 3).</summary>
    Task<int> GetUsageTotalAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Previews the prorated cost of a plan change before committing it (UC3 step 2).</summary>
    Task<ProrationPreview> PreviewPlanChangeAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken = default);

    /// <summary>Commits a plan change with the chosen timing (UC3 step 4).</summary>
    Task<CustomerSubscription> ChangePlanAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken = default);

    /// <summary>Applies a lifecycle transition (UC4).</summary>
    Task<CustomerSubscription> ApplyLifecycleActionAsync(int subscriptionId, SubscriptionLifecycleAction action, string? reason, CancellationToken cancellationToken = default);
}
