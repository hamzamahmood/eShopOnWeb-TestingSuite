using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// Published in-process after a lifecycle transition (pause/resume/cancel/reactivate — UC4) commits.
/// </summary>
public class SubscriptionStateChanged : INotification
{
    public string CustomerReference { get; }
    public string SubscriptionId { get; }
    public SubscriptionState OldState { get; }
    public SubscriptionState NewState { get; }

    public SubscriptionStateChanged(string customerReference, string subscriptionId, SubscriptionState oldState, SubscriptionState newState)
    {
        CustomerReference = customerReference;
        SubscriptionId = subscriptionId;
        OldState = oldState;
        NewState = newState;
    }
}
