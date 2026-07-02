namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// When a plan change (UC3) should take effect.
/// </summary>
public enum PlanChangeTiming
{
    /// <summary>Apply now; the delta is prorated and charged/credited immediately.</summary>
    Immediate,

    /// <summary>Apply at the start of the next billing period; no proration.</summary>
    AtRenewal
}
