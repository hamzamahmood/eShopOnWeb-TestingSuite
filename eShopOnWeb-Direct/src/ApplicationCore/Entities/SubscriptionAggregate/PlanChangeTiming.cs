namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>When a plan change (UC3) takes effect.</summary>
public enum PlanChangeTiming
{
    /// <summary>Apply now, with proration for the remainder of the current period.</summary>
    Immediate = 0,

    /// <summary>Defer to the next renewal; the new plan price applies from the next period, no proration.</summary>
    AtRenewal = 1
}
