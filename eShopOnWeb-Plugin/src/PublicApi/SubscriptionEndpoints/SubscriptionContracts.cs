using System;
using System.Globalization;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

// DTOs, request bodies and mappings shared by the subscription endpoints. Kept
// together to mirror the compact CatalogEndpoints DTOs and avoid a proliferation
// of one-line files.

public class SubscriptionPlanDto
{
    public int Id { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PriceInCents { get; set; }
    public decimal Price { get; set; }
    public int Interval { get; set; }
    public string IntervalUnit { get; set; } = "month";
}

public class CustomerSubscriptionDto
{
    public int Id { get; set; }
    public string? PlanHandle { get; set; }
    public string? PlanName { get; set; }
    public decimal? Price { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public bool CancelAtEndOfPeriod { get; set; }
    public DateTimeOffset? DelayedCancelAt { get; set; }
    public string? NextPlanHandle { get; set; }
}

public class UsageResultDto
{
    public int SubscriptionId { get; set; }
    public int RecordedQuantity { get; set; }
    public int? PeriodToDateTotal { get; set; }
    public bool PeriodTotalAvailable { get; set; }
    public string? Memo { get; set; }
}

public class PlanChangePreviewDto
{
    public string FromPlanHandle { get; set; } = string.Empty;
    public string ToPlanHandle { get; set; } = string.Empty;
    public bool ApplyNow { get; set; }
    public long ProratedAdjustmentInCents { get; set; }
    public long ChargeInCents { get; set; }
    public long CreditAppliedInCents { get; set; }
    public long PaymentDueInCents { get; set; }
    public decimal AmountDue { get; set; }
    public DateTimeOffset? EffectiveDate { get; set; }
}

public record SubscribeRequest(string PlanHandle);

public record RecordUsageRequest(int SubscriptionId, int Quantity, string? Memo);

public record PlanChangePreviewRequest(string TargetPlanHandle, bool ApplyNow);

public record PlanChangeRequest(string TargetPlanHandle, bool ApplyNow, long ConfirmedAmountDueInCents);

public record LifecycleRequest(string Action, bool EndOfPeriod, string? Reason);

public record AdminLifecycleRequest(int SubscriptionId, string Action, bool EndOfPeriod, string? Reason);

public static class SubscriptionMappings
{
    public static SubscriptionPlanDto ToDto(this SubscriptionPlan plan) => new()
    {
        Id = plan.Id,
        Handle = plan.Handle,
        Name = plan.Name,
        PriceInCents = plan.PriceInCents,
        Price = plan.Price,
        Interval = plan.Interval,
        IntervalUnit = plan.IntervalUnit
    };

    public static CustomerSubscriptionDto ToDto(this CustomerSubscription subscription) => new()
    {
        Id = subscription.Id,
        PlanHandle = subscription.PlanHandle,
        PlanName = subscription.PlanName,
        Price = subscription.Price,
        State = subscription.State,
        CurrentPeriodEndsAt = subscription.CurrentPeriodEndsAt,
        CancelAtEndOfPeriod = subscription.CancelAtEndOfPeriod,
        DelayedCancelAt = subscription.DelayedCancelAt,
        NextPlanHandle = subscription.NextPlanHandle
    };

    public static UsageResultDto ToDto(this UsageSummary usage) => new()
    {
        SubscriptionId = usage.SubscriptionId,
        RecordedQuantity = usage.RecordedQuantity,
        PeriodToDateTotal = usage.PeriodToDateTotal,
        PeriodTotalAvailable = usage.PeriodTotalAvailable,
        Memo = usage.Memo
    };

    public static PlanChangePreviewDto ToDto(this PlanChangePreview preview) => new()
    {
        FromPlanHandle = preview.FromPlanHandle,
        ToPlanHandle = preview.ToPlanHandle,
        ApplyNow = preview.ApplyNow,
        ProratedAdjustmentInCents = preview.ProratedAdjustmentInCents,
        ChargeInCents = preview.ChargeInCents,
        CreditAppliedInCents = preview.CreditAppliedInCents,
        PaymentDueInCents = preview.PaymentDueInCents,
        AmountDue = preview.AmountDue,
        EffectiveDate = preview.EffectiveDate
    };

    // Parses the lifecycle action name (case-insensitive) used by the endpoints.
    public static bool TryParseAction(string? value, out SubscriptionLifecycleAction action) =>
        Enum.TryParse(value, ignoreCase: true, out action) &&
        Enum.IsDefined(typeof(SubscriptionLifecycleAction), action);
}
