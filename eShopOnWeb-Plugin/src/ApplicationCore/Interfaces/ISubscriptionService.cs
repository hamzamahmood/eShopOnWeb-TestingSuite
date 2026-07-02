using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The subscription module's use-case surface (mirrors <see cref="IOrderService"/>). Orchestrates
/// <see cref="IBillingClient"/>, enforces the "caller owns the subscription, or is an admin" rule that both
/// hosts (<c>Web</c> and <c>PublicApi</c>) rely on, and publishes the lifecycle <c>INotification</c>s.
/// </summary>
public interface ISubscriptionService
{
    Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken cancellationToken);

    /// <summary>UC1: ensures a provider customer exists for <paramref name="userId"/> and enrolls it in the plan.</summary>
    Task<SubscriptionDto> SubscribeAsync(string userId, string email, string? firstName, string? lastName, string productHandle, CancellationToken cancellationToken);

    /// <summary>UC1 (mine): lists only the authenticated user's own subscriptions.</summary>
    Task<IReadOnlyList<SubscriptionDto>> ListMySubscriptionsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>UC2: records usage against a specific subscription; caller must own it unless <paramref name="callerIsAdmin"/>.</summary>
    Task<UsageDto> RecordUsageAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, decimal quantity, string? memo, string requestId, CancellationToken cancellationToken);

    /// <summary>
    /// UC2 automatic hook: records usage against <paramref name="userId"/>'s own active subscription, if they
    /// have one. Never throws for "no subscription" — that is an expected, silent no-op for this hook.
    /// </summary>
    Task RecordUsageForUserIfSubscribedAsync(string userId, decimal quantity, string? memo, string requestId, CancellationToken cancellationToken);

    Task<UsageSummaryDto> GetUsageSummaryAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken);

    /// <summary>UC3: previews a plan change; caller must own the subscription unless <paramref name="callerIsAdmin"/>.</summary>
    Task<ProrationPreviewDto> PreviewPlanChangeAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken);

    /// <summary>UC3: commits a previously previewed plan change; throws <c>StalePreviewException</c> if it no longer matches.</summary>
    Task<SubscriptionDto> CommitPlanChangeAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, string previewToken, CancellationToken cancellationToken);

    Task<SubscriptionDto> PauseAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken);

    Task<SubscriptionDto> ResumeAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken);

    Task<SubscriptionDto> CancelAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancelTiming timing, string? reason, CancellationToken cancellationToken);

    Task<SubscriptionDto> ReactivateAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken);
}
