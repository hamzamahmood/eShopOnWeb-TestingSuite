using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

// The previewed cost of a plan change (UC3), shown to and confirmed by the
// customer before any charge. For an immediate ("apply now") change the amounts
// are the prorated figures computed by the provider; for an "at renewal" change
// there is no proration and the amount due is the new plan price effective at
// the next period boundary.
public record PlanChangePreview
{
    public string FromPlanHandle { get; init; } = string.Empty;
    public string ToPlanHandle { get; init; } = string.Empty;
    public bool ApplyNow { get; init; }

    public long ProratedAdjustmentInCents { get; init; }
    public long ChargeInCents { get; init; }
    public long CreditAppliedInCents { get; init; }
    public long PaymentDueInCents { get; init; }

    public DateTimeOffset? EffectiveDate { get; init; }

    public decimal AmountDue => PaymentDueInCents / 100m;
    public decimal Charge => ChargeInCents / 100m;
    public decimal Credit => CreditAppliedInCents / 100m;
}
