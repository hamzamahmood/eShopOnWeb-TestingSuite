using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.Handlers;

/// <summary>
/// UC2's automatic hook: one order placed records one <c>api-call</c> usage unit on the buyer's active
/// subscription, if they have one. This handler is the failure-isolation boundary the integration plan (§5)
/// requires — a billing-provider outage, a misconfigured component, or the buyer simply not having a
/// subscription must never fail the order that already succeeded, so every failure is caught and logged here
/// rather than allowed to propagate back into <c>OrderService.CreateOrderAsync</c>.
/// </summary>
public class RecordUsageOnOrderPlacedHandler : INotificationHandler<OrderPlaced>
{
    private const decimal OneApiCallUnit = 1m;

    private readonly ISubscriptionService _subscriptionService;
    private readonly IAppLogger<RecordUsageOnOrderPlacedHandler> _logger;

    public RecordUsageOnOrderPlacedHandler(ISubscriptionService subscriptionService, IAppLogger<RecordUsageOnOrderPlacedHandler> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public async Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        try
        {
            var requestId = $"order:{notification.OrderId}";
            await _subscriptionService.RecordUsageForUserIfSubscribedAsync(
                notification.BuyerId, OneApiCallUnit, $"Order #{notification.OrderId} placed", requestId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Intentional broad catch: this is the documented failure-isolation boundary between order
            // placement and the billing provider (integration plan §5) - it must never surface.
            _logger.LogWarning("Failed to record automatic usage for order {OrderId}: {ErrorMessage}", notification.OrderId, ex.Message);
        }
    }
}
