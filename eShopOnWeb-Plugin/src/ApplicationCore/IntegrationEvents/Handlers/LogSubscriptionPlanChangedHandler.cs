using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.Handlers;

public class LogSubscriptionPlanChangedHandler : INotificationHandler<SubscriptionPlanChanged>
{
    private readonly IAppLogger<LogSubscriptionPlanChangedHandler> _logger;

    public LogSubscriptionPlanChangedHandler(IAppLogger<LogSubscriptionPlanChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionPlanChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {SubscriptionId} for user {UserId} changed plan {OldPlan} -> {NewPlan} (prorated {ProratedAmount})",
            notification.SubscriptionId, notification.CustomerReference, notification.OldProductHandle, notification.NewProductHandle, notification.ProratedAmount);
        return Task.CompletedTask;
    }
}
