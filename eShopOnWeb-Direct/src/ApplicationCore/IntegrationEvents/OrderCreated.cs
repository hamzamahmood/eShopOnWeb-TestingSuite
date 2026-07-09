using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>
/// In-process notification published when an order is created. The subscription feature subscribes to
/// this to demo automatic pay-as-you-go usage: one order placed → one billable unit (UC2, plan §8).
/// Best-effort, in-process only (§2.5).
/// </summary>
public class OrderCreated : INotification
{
    public OrderCreated(int orderId, string buyerId)
    {
        OrderId = orderId;
        BuyerId = buyerId;
    }

    public int OrderId { get; }

    /// <summary>The order's buyer id — for an authenticated shopper this is their username/email
    /// (the same reference used as the Maxio customer reference).</summary>
    public string BuyerId { get; }
}
