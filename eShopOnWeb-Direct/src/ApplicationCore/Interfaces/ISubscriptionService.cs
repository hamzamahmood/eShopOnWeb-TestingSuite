using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Use-case surface for the subscription feature (mirrors <see cref="IOrderService"/>). Orchestrates
/// the provider-agnostic <see cref="IBillingClient"/> and publishes in-process MediatR notifications
/// on meaningful state changes. The billing provider is the system of record; the mapping between the
/// eShopOnWeb user and the provider customer is kept stateless via the user's email/username reference.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>UC1 step 1 — the plans a customer can subscribe to.</summary>
    Task<IReadOnlyCollection<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// UC1 — ensures a provider-side customer exists for this eShopOnWeb user (idempotent on the
    /// reference) and enrolls them in the chosen plan. If the customer already has an active
    /// subscription on that plan it is returned rather than creating a second enrollment.
    /// Publishes <c>SubscriptionActivated</c> on a new enrollment.
    /// </summary>
    Task<CustomerSubscription> SubscribeAsync(string userReference, string email, string productHandle, CancellationToken cancellationToken = default);

    /// <summary>Lists the current user's subscriptions (empty if the user has no provider customer yet).</summary>
    Task<IReadOnlyCollection<CustomerSubscription>> GetMySubscriptionsAsync(string userReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC2 — records usage against the subscription's metered component and returns the running
    /// period-to-date total. When <paramref name="ownerReference"/> is supplied the subscription
    /// must belong to that customer (customer reporting their own usage); pass null for admin
    /// reporting against any subscription.
    /// </summary>
    Task<UsageResult> RecordUsageAsync(int subscriptionId, int quantity, string? memo, string? ownerReference, CancellationToken cancellationToken = default);

    /// <summary>UC3 step 2 — previews the cost of moving a subscription to a different plan.</summary>
    Task<ProrationPreview> PreviewPlanChangeAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC3 step 4 — commits a plan change with the chosen timing. The <paramref name="confirmedPreview"/>
    /// (the exact amounts shown to and confirmed by the customer) is re-validated against a fresh preview;
    /// if it has gone stale the commit is rejected. Publishes <c>SubscriptionPlanChanged</c> on success.
    /// </summary>
    Task<CustomerSubscription> ChangePlanAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, ProrationPreview confirmedPreview, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC4 — applies a lifecycle transition (pause/resume/cancel/cancel-at-period-end/reactivate).
    /// Publishes <c>SubscriptionStateChanged</c> carrying old → new state on success.
    /// </summary>
    Task<CustomerSubscription> ChangeLifecycleAsync(int subscriptionId, SubscriptionLifecycleAction action, string? reason, CancellationToken cancellationToken = default);
}
