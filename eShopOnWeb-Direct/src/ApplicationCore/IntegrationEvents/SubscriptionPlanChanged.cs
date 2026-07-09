using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// In-process notification published after a subscription's plan is changed (UC3 step 5).
/// Best-effort delivery (§2.5).
/// </summary>
public class SubscriptionPlanChanged : INotification
{
    public SubscriptionPlanChanged(int subscriptionId, string oldProductHandle, string newProductHandle, PlanChangeTiming timing, CustomerSubscription subscription)
    {
        SubscriptionId = subscriptionId;
        OldProductHandle = oldProductHandle;
        NewProductHandle = newProductHandle;
        Timing = timing;
        Subscription = subscription;
    }

    public int SubscriptionId { get; }
    public string OldProductHandle { get; }
    public string NewProductHandle { get; }
    public PlanChangeTiming Timing { get; }
    public CustomerSubscription Subscription { get; }
}
