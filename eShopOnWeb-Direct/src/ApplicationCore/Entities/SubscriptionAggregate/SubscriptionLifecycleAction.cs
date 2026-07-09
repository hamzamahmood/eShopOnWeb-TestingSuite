namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>The lifecycle transitions a subscription supports (UC4).</summary>
public enum SubscriptionLifecycleAction
{
    Pause = 0,
    Resume,
    Cancel,
    CancelAtEndOfPeriod,
    Reactivate
}
