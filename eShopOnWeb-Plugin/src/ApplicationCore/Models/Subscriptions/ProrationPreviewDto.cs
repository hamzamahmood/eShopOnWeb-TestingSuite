using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// Preview of a plan change (UC3): the amount that would be charged/credited and when it takes effect.
/// <see cref="PreviewToken"/> must be presented to <c>ISubscriptionService.CommitPlanChangeAsync</c> so the
/// commit can be rejected as stale if it no longer matches what was previewed (AC-07b).
/// </summary>
public record ProrationPreviewDto(
    string SubscriptionId,
    string FromProductHandle,
    string ToProductHandle,
    PlanChangeTiming Timing,
    decimal ProratedAmount,
    DateTimeOffset EffectiveAt,
    string PreviewToken);
