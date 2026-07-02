using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// Published in-process after an order is created. Consumed by <c>RecordUsageOnOrderPlacedHandler</c> to
/// record one pay-as-you-go usage unit (UC2) on the buyer's subscription, if they have one. Unrelated to
/// the Basket→Order checkout flow itself, which must never fail because of this notification.
/// </summary>
public class OrderPlaced : INotification
{
    public string BuyerId { get; }
    public int OrderId { get; }

    public OrderPlaced(string buyerId, int orderId)
    {
        BuyerId = buyerId;
        OrderId = orderId;
    }
}
