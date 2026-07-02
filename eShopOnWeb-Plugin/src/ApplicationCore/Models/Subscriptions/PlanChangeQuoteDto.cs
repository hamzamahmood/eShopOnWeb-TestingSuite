using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// The billing provider's quote for a plan change, before any preview-token/staleness handling is layered
/// on by <c>ISubscriptionService</c> (see <see cref="ProrationPreviewDto"/>).
/// </summary>
public record PlanChangeQuoteDto(
    string SubscriptionId,
    string FromProductHandle,
    string ToProductHandle,
    PlanChangeTiming Timing,
    decimal ProratedAmount,
    DateTimeOffset EffectiveAt);
