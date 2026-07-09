using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// In-process notification published after a subscription lifecycle transition (UC4 step 3),
/// carrying the old → new state. Best-effort delivery (§2.5).
/// </summary>
public class SubscriptionStateChanged : INotification
{
    public SubscriptionStateChanged(int subscriptionId, SubscriptionLifecycleAction action, SubscriptionState oldState, SubscriptionState newState, CustomerSubscription subscription)
    {
        SubscriptionId = subscriptionId;
        Action = action;
        OldState = oldState;
        NewState = newState;
        Subscription = subscription;
    }

    public int SubscriptionId { get; }
    public SubscriptionLifecycleAction Action { get; }
    public SubscriptionState OldState { get; }
    public SubscriptionState NewState { get; }
    public CustomerSubscription Subscription { get; }
}
