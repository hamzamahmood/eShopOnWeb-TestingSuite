using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

// Published in-process (best-effort) after a subscription is successfully
// activated with the billing provider (UC1). Mirrors eShopOnWeb's existing
// MediatR usage; there is no durable broker or outbox (plan §2.5).
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
