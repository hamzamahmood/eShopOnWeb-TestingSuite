using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// In-process notification published after a customer is successfully enrolled in a plan (UC1 step 6).
/// Delivery is best-effort (§2.5): registered handlers run, but a handler failure never rolls back
/// the enrollment.
/// </summary>
public class SubscriptionActivated : INotification
{
    public SubscriptionActivated(string userReference, CustomerSubscription subscription)
    {
        UserReference = userReference;
        Subscription = subscription;
    }

    public string UserReference { get; }
    public CustomerSubscription Subscription { get; }
}
