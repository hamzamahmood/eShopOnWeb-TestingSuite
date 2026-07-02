using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.Handlers;

public class LogSubscriptionStateChangedHandler : INotificationHandler<SubscriptionStateChanged>
{
    private readonly IAppLogger<LogSubscriptionStateChangedHandler> _logger;

    public LogSubscriptionStateChangedHandler(IAppLogger<LogSubscriptionStateChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionStateChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {SubscriptionId} for user {UserId} transitioned {OldState} -> {NewState}",
            notification.SubscriptionId, notification.CustomerReference, notification.OldState, notification.NewState);
        return Task.CompletedTask;
    }
}
