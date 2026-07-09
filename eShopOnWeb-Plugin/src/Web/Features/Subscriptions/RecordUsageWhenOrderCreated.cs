using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

namespace Microsoft.eShopWeb.Web.Features.Subscriptions;

// UC2 automatic trigger (plan §8): one order placed => one billable usage unit
// recorded against the buyer's active subscription. The service call is
// best-effort by contract and never throws, so this handler cannot affect the
// order lifecycle.
public class RecordUsageWhenOrderCreated : INotificationHandler<OrderCreated>
{
    private readonly ISubscriptionService _subscriptionService;

    public RecordUsageWhenOrderCreated(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public Task Handle(OrderCreated notification, CancellationToken cancellationToken)
        => _subscriptionService.RecordOrderUsageAsync(notification.BuyerId, cancellationToken);
}
