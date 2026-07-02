using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// A single usage record accepted by the billing provider (UC2).
/// </summary>
public record UsageDto(
    string UsageId,
    decimal Quantity,
    string? Memo,
    DateTimeOffset? RecordedAt);

/// <summary>
/// Period-to-date usage total for a metered component on a subscription (UC2).
/// </summary>
public record UsageSummaryDto(
    string ComponentHandle,
    decimal PeriodToDateQuantity,
    DateTimeOffset? PeriodStartsAt,
    DateTimeOffset? PeriodEndsAt);
