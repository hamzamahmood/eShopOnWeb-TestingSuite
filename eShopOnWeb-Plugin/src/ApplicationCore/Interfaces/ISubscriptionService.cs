using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// The subscription use-case surface (mirrors IOrderService). Orchestrates the
// billing client, enforces the domain rules each use case requires, and
// publishes the corresponding in-process MediatR notifications. Customer flows
// are keyed on the stable user reference (email / User.Identity.Name, plan §8);
// admin flows on the PublicApi operate directly on a subscription id.
public interface ISubscriptionService
{
    // UC1
    Task<IReadOnlyList<SubscriptionPlan>> GetAvailablePlansAsync(CancellationToken cancellationToken = default);
    Task<CustomerSubscription> SubscribeAsync(string userReference, string planHandle,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerSubscription>> GetSubscriptionsForUserAsync(string userReference,
        CancellationToken cancellationToken = default);
    Task<CustomerSubscription?> GetActiveSubscriptionForUserAsync(string userReference,
        CancellationToken cancellationToken = default);

    // UC2
    Task<UsageSummary> RecordUsageForUserAsync(string userReference, int quantity, string? memo,
        CancellationToken cancellationToken = default);
    Task<UsageSummary> RecordUsageAsync(int subscriptionId, int quantity, string? memo,
        CancellationToken cancellationToken = default);
    // Best-effort "one order placed -> one billable unit" hook. Never throws;
    // a billing failure must not roll back eShopOnWeb's order.
    Task RecordOrderUsageAsync(string userReference, CancellationToken cancellationToken = default);

    // UC3
    Task<PlanChangePreview> PreviewPlanChangeForUserAsync(string userReference, string targetPlanHandle,
        bool applyNow, CancellationToken cancellationToken = default);
    Task<CustomerSubscription> ChangePlanForUserAsync(string userReference, string targetPlanHandle, bool applyNow,
        long confirmedAmountDueInCents, CancellationToken cancellationToken = default);

    // UC4
    Task<CustomerSubscription> ChangeLifecycleForUserAsync(string userReference, SubscriptionLifecycleAction action,
        bool endOfPeriod, string? reason, CancellationToken cancellationToken = default);
    Task<CustomerSubscription> ChangeLifecycleAsync(int subscriptionId, SubscriptionLifecycleAction action,
        bool endOfPeriod, string? reason, CancellationToken cancellationToken = default);
}
