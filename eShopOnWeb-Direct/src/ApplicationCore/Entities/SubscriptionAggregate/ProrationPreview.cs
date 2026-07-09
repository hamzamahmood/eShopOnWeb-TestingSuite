namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// A preview of the financial impact of a plan change (UC3), computed by the billing provider
/// before the change is committed. All amounts are in integer cents. For an "at renewal" change
/// there is no proration, so the preview amounts are zero and the new plan price takes effect
/// at the next period.
/// </summary>
public class ProrationPreview
{
    public int ProratedAdjustmentInCents { get; init; }
    public int ChargeInCents { get; init; }
    public int PaymentDueInCents { get; init; }
    public int CreditAppliedInCents { get; init; }

    /// <summary>The plan the subscription would move to.</summary>
    public string TargetProductHandle { get; init; } = string.Empty;

    /// <summary>Whether this preview is for an immediate (prorated) change or one deferred to renewal.</summary>
    public PlanChangeTiming Timing { get; init; }

    public decimal ProratedAdjustment => ProratedAdjustmentInCents / 100m;
    public decimal Charge => ChargeInCents / 100m;
    public decimal PaymentDue => PaymentDueInCents / 100m;
    public decimal CreditApplied => CreditAppliedInCents / 100m;
}
