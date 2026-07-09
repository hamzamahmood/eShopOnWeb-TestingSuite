using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class SubscriptionPlanDto
{
    public int Id { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriceInCents { get; set; }
    public decimal Price { get; set; }
    public int Interval { get; set; }
    public string IntervalUnit { get; set; } = string.Empty;
}

public class SubscriptionDto
{
    public int Id { get; set; }
    public string State { get; set; } = string.Empty;
    public string ProductHandle { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int ProductPriceInCents { get; set; }
    public decimal ProductPrice { get; set; }
    public int Interval { get; set; }
    public string IntervalUnit { get; set; } = string.Empty;
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public bool CancelAtEndOfPeriod { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public DateTimeOffset? DelayedCancelAt { get; set; }
    public DateTimeOffset? AutomaticallyResumeAt { get; set; }
}

public class ProrationPreviewDto
{
    public string TargetProductHandle { get; set; } = string.Empty;
    public string Timing { get; set; } = string.Empty;
    public int ProratedAdjustmentInCents { get; set; }
    public int ChargeInCents { get; set; }
    public int PaymentDueInCents { get; set; }
    public int CreditAppliedInCents { get; set; }
    public decimal ProratedAdjustment { get; set; }
    public decimal Charge { get; set; }
    public decimal PaymentDue { get; set; }
    public decimal CreditApplied { get; set; }
}

public class UsageResultDto
{
    public int RecordedQuantity { get; set; }
    public string? Memo { get; set; }
    public int? PeriodToDateTotal { get; set; }
}

/// <summary>Maps the provider-agnostic domain models onto the API DTOs.</summary>
public static class SubscriptionMap
{
    public static SubscriptionPlanDto ToDto(this SubscriptionPlan p) => new()
    {
        Id = p.Id,
        Handle = p.Handle,
        Name = p.Name,
        Description = p.Description,
        PriceInCents = p.PriceInCents,
        Price = p.Price,
        Interval = p.Interval,
        IntervalUnit = p.IntervalUnit
    };

    public static SubscriptionDto ToDto(this CustomerSubscription s) => new()
    {
        Id = s.Id,
        State = s.State.ToString(),
        ProductHandle = s.ProductHandle,
        ProductName = s.ProductName,
        ProductPriceInCents = s.ProductPriceInCents,
        ProductPrice = s.ProductPrice,
        Interval = s.Interval,
        IntervalUnit = s.IntervalUnit,
        CurrentPeriodEndsAt = s.CurrentPeriodEndsAt,
        CancelAtEndOfPeriod = s.CancelAtEndOfPeriod,
        CanceledAt = s.CanceledAt,
        DelayedCancelAt = s.DelayedCancelAt,
        AutomaticallyResumeAt = s.AutomaticallyResumeAt
    };

    public static ProrationPreviewDto ToDto(this ProrationPreview p) => new()
    {
        TargetProductHandle = p.TargetProductHandle,
        Timing = p.Timing.ToString(),
        ProratedAdjustmentInCents = p.ProratedAdjustmentInCents,
        ChargeInCents = p.ChargeInCents,
        PaymentDueInCents = p.PaymentDueInCents,
        CreditAppliedInCents = p.CreditAppliedInCents,
        ProratedAdjustment = p.ProratedAdjustment,
        Charge = p.Charge,
        PaymentDue = p.PaymentDue,
        CreditApplied = p.CreditApplied
    };

    public static UsageResultDto ToDto(this UsageResult u) => new()
    {
        RecordedQuantity = u.RecordedQuantity,
        Memo = u.Memo,
        PeriodToDateTotal = u.PeriodToDateTotal
    };
}
