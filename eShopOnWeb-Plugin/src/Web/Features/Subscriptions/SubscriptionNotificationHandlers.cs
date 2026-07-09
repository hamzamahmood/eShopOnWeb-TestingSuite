using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

namespace Microsoft.eShopWeb.Web.Features.Subscriptions;

// In-process reactions to subscription lifecycle notifications (plan §2.5):
// an audit log via IAppLogger and a best-effort confirmation email via the
// existing IEmailSender. Handlers are intentionally side-effect-only; failures
// here never roll back the successful provider call.

public class SubscriptionActivatedHandler : INotificationHandler<SubscriptionActivated>
{
    private readonly IAppLogger<SubscriptionActivatedHandler> _logger;
    private readonly IEmailSender _emailSender;

    public SubscriptionActivatedHandler(IAppLogger<SubscriptionActivatedHandler> logger, IEmailSender emailSender)
    {
        _logger = logger;
        _emailSender = emailSender;
    }

    public async Task Handle(SubscriptionActivated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            $"Subscription {notification.Subscription.Id} activated on plan " +
            $"'{notification.Subscription.PlanName ?? notification.Subscription.PlanHandle}' for " +
            $"'{notification.UserReference}'.");

        await _emailSender.SendEmailAsync(notification.UserReference, "Your subscription is active",
            $"You are now subscribed to {notification.Subscription.PlanName ?? notification.Subscription.PlanHandle}.");
    }
}

public class SubscriptionPlanChangedHandler : INotificationHandler<SubscriptionPlanChanged>
{
    private readonly IAppLogger<SubscriptionPlanChangedHandler> _logger;

    public SubscriptionPlanChangedHandler(IAppLogger<SubscriptionPlanChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionPlanChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            $"Subscription {notification.SubscriptionId} plan changed from '{notification.FromPlanHandle}' to " +
            $"'{notification.ToPlanHandle}' ({(notification.AppliedNow ? "now" : "at renewal")}).");
        return Task.CompletedTask;
    }
}

public class SubscriptionStateChangedHandler : INotificationHandler<SubscriptionStateChanged>
{
    private readonly IAppLogger<SubscriptionStateChangedHandler> _logger;

    public SubscriptionStateChangedHandler(IAppLogger<SubscriptionStateChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionStateChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            $"Subscription {notification.SubscriptionId} state changed from '{notification.OldState}' to " +
            $"'{notification.NewState}' via {notification.Action}.");
        return Task.CompletedTask;
    }
}
