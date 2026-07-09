using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Web.Features.Subscriptions;

/// <summary>
/// UC2 automatic trigger (plan §8): when an order is placed, record one pay-as-you-go usage unit against
/// the buyer's active subscription. Fully best-effort — a buyer with no subscription is a no-op, and any
/// failure is logged rather than allowed to fail the checkout that raised the event.
/// </summary>
public class RecordUsageOnOrderCreatedHandler : INotificationHandler<OrderCreated>
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAppLogger<RecordUsageOnOrderCreatedHandler> _logger;

    public RecordUsageOnOrderCreatedHandler(ISubscriptionService subscriptionService,
        IAppLogger<RecordUsageOnOrderCreatedHandler> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public async Task Handle(OrderCreated notification, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptions = await _subscriptionService.GetMySubscriptionsAsync(notification.BuyerId, cancellationToken);
            var active = subscriptions.FirstOrDefault(s => s.IsActive);
            if (active is null)
            {
                return; // buyer isn't a subscriber (or checked out anonymously) — nothing to meter.
            }

            await _subscriptionService.RecordUsageAsync(active.Id, 1,
                $"eShopOnWeb order {notification.OrderId}", notification.BuyerId, cancellationToken);

            _logger.LogInformation("Recorded 1 usage unit on subscription {0} for order {1}.",
                active.Id, notification.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not record usage for order {0} (best-effort, checkout still succeeded): {1}",
                notification.OrderId, ex.Message);
        }
    }
}
