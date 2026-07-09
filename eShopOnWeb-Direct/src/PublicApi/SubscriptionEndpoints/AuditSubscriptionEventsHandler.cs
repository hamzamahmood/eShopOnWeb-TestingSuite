using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// In-process handler that writes an audit line whenever a subscription changes (§2.5). Demonstrates
/// the best-effort MediatR eventing on the API host; discovered automatically by the MediatR assembly scan.
/// </summary>
public class AuditSubscriptionEventsHandler :
    INotificationHandler<SubscriptionActivated>,
    INotificationHandler<SubscriptionPlanChanged>,
    INotificationHandler<SubscriptionStateChanged>
{
    private readonly IAppLogger<AuditSubscriptionEventsHandler> _logger;

    public AuditSubscriptionEventsHandler(IAppLogger<AuditSubscriptionEventsHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionActivated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {0} activated for {1} on plan {2}.",
            notification.Subscription.Id, notification.UserReference, notification.Subscription.ProductHandle);
        return Task.CompletedTask;
    }

    public Task Handle(SubscriptionPlanChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {0} changed plan {1} -> {2} ({3}).",
            notification.SubscriptionId, notification.OldProductHandle, notification.NewProductHandle, notification.Timing);
        return Task.CompletedTask;
    }

    public Task Handle(SubscriptionStateChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {0} state {1} -> {2} (via {3}).",
            notification.SubscriptionId, notification.OldState, notification.NewState, notification.Action);
        return Task.CompletedTask;
    }
}
