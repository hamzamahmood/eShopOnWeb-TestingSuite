using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// Published in-process (best-effort, no durable outbox — see integration plan §2.5) after a customer
/// successfully enrolls in a plan (UC1).
/// </summary>
public class SubscriptionActivated : INotification
{
    public string CustomerReference { get; }
    public string SubscriptionId { get; }
    public string ProductHandle { get; }

    public SubscriptionActivated(string customerReference, string subscriptionId, string productHandle)
    {
        CustomerReference = customerReference;
        SubscriptionId = subscriptionId;
        ProductHandle = productHandle;
    }
}
