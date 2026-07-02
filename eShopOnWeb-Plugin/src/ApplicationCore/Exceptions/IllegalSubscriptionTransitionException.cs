using System;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The requested lifecycle action is not legal from the subscription's current state
/// (e.g. resuming a subscription that is not on hold).
/// </summary>
public class IllegalSubscriptionTransitionException : Exception
{
    public IllegalSubscriptionTransitionException(string subscriptionId, string action, SubscriptionState currentState)
        : base($"Cannot {action} subscription {subscriptionId} while it is {currentState}")
    {
    }
}
