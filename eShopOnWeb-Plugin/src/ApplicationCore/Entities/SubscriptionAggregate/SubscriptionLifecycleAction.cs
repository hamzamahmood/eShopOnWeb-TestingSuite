namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

// The four lifecycle transitions exposed by UC4's single management surface.
public enum SubscriptionLifecycleAction
{
    Pause,
    Resume,
    Cancel,
    Reactivate
}
