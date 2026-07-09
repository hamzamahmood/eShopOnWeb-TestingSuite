namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Shared request for previewing and committing a plan change (UC3). On commit, the four *InCents
/// fields carry the exact amounts the customer confirmed; the service re-prices and rejects the commit
/// if they have gone stale.
/// </summary>
public class PlanChangeRequest : BaseRequest
{
    /// <summary>Bound from the route.</summary>
    public int SubscriptionId { get; set; }

    /// <summary>Handle of the plan to move to (e.g. <c>basic-plan</c> or <c>eshop-pro</c>).</summary>
    public string TargetProductHandle { get; set; } = string.Empty;

    /// <summary>"Immediate" (prorated now) or "AtRenewal" (no proration).</summary>
    public string Timing { get; set; } = "Immediate";

    // The confirmed preview amounts (ignored by the preview endpoint; used for the staleness check on commit).
    public int ProratedAdjustmentInCents { get; set; }
    public int ChargeInCents { get; set; }
    public int PaymentDueInCents { get; set; }
    public int CreditAppliedInCents { get; set; }
}
