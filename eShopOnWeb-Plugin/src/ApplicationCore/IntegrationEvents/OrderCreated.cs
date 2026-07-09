using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

// Raised in-process when an eShopOnWeb order is created. Used by the pay-as-you-go
// hook (UC2, plan §8): a handler records one billable usage unit against the
// buyer's active subscription. BuyerId is the buyer reference (email / username),
// which is the same stable reference the subscription customer is keyed on.
public class OrderCreated : INotification
{
    public OrderCreated(string buyerId)
    {
        BuyerId = buyerId;
    }

    public string BuyerId { get; }
}
