using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Web.Features.Subscriptions;

/// <summary>
/// In-process reactions to subscription lifecycle notifications (§2.5): audit via <see cref="IAppLogger{T}"/>
/// and a confirmation email via the existing <see cref="IEmailSender"/>. Discovered by the Web MediatR
/// assembly scan; delivery is best-effort (the publisher swallows handler failures).
/// </summary>
public class SubscriptionNotificationsHandler :
    INotificationHandler<SubscriptionActivated>,
    INotificationHandler<SubscriptionPlanChanged>,
    INotificationHandler<SubscriptionStateChanged>
{
    private readonly IEmailSender _emailSender;
    private readonly IAppLogger<SubscriptionNotificationsHandler> _logger;

    public SubscriptionNotificationsHandler(IEmailSender emailSender,
        IAppLogger<SubscriptionNotificationsHandler> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task Handle(SubscriptionActivated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {0} activated for {1} on plan {2}.",
            notification.Subscription.Id, notification.UserReference, notification.Subscription.ProductHandle);

        await _emailSender.SendEmailAsync(notification.UserReference, "Your subscription is active",
            $"You are now subscribed to {notification.Subscription.ProductName}.");
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
