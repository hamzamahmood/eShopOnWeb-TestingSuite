using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

// Published in-process (best-effort) after a lifecycle transition (UC4),
// carrying the old and new state.
public class SubscriptionStateChanged : INotification
{
    public SubscriptionStateChanged(int subscriptionId, SubscriptionLifecycleAction action, string oldState, string newState)
    {
        SubscriptionId = subscriptionId;
        Action = action;
        OldState = oldState;
        NewState = newState;
    }

    public int SubscriptionId { get; }
    public SubscriptionLifecycleAction Action { get; }
    public string OldState { get; }
    public string NewState { get; }
}
