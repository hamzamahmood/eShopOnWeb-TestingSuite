using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.Handlers;

public class LogSubscriptionActivatedHandler : INotificationHandler<SubscriptionActivated>
{
    private readonly IAppLogger<LogSubscriptionActivatedHandler> _logger;

    public LogSubscriptionActivatedHandler(IAppLogger<LogSubscriptionActivatedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionActivated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {SubscriptionId} activated for user {UserId} on plan {ProductHandle}",
            notification.SubscriptionId, notification.CustomerReference, notification.ProductHandle);
        return Task.CompletedTask;
    }
}
