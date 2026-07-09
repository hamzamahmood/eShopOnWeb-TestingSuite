using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

// Use-case orchestration for subscriptions (mirrors OrderService). Validates,
// drives the billing client, and publishes best-effort in-process notifications.
public class SubscriptionService : ISubscriptionService
{
    private readonly IBillingClient _billingClient;
    private readonly IPublisher _publisher;
    private readonly IAppLogger<SubscriptionService> _logger;

    public SubscriptionService(IBillingClient billingClient,
        IPublisher publisher,
        IAppLogger<SubscriptionService> logger)
    {
        _billingClient = billingClient;
        _publisher = publisher;
        _logger = logger;
    }

    public Task<IReadOnlyList<SubscriptionPlan>> GetAvailablePlansAsync(CancellationToken cancellationToken = default)
        => _billingClient.ListPlansAsync(cancellationToken);

    // UC1 — Subscribe to a plan.
    public async Task<CustomerSubscription> SubscribeAsync(string userReference, string planHandle,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));
        Guard.Against.NullOrEmpty(planHandle, nameof(planHandle));

        // Fail with a configuration error (pointing back at UC0) rather than
        // enrolling against a guessed/unknown plan.
        var plan = await _billingClient.FindPlanAsync(planHandle, cancellationToken);
        if (plan is null)
        {
            throw new BillingConfigurationException(
                $"The configured plan '{planHandle}' does not resolve on the billing provider. " +
                "Verify the seeded product handles/ids (see plan UC0).");
        }

        var (firstName, lastName) = DeriveName(userReference);
        var customerId = await _billingClient.EnsureCustomerAsync(userReference, userReference, firstName, lastName,
            cancellationToken);

        // Idempotency: a double-click / repeated call must never create a second
        // enrollment. If an active subscription already exists, return it.
        var existing = await _billingClient.ListCustomerSubscriptionsAsync(customerId, cancellationToken);
        var active = existing.FirstOrDefault(s => s.IsActive);
        if (active is not null)
        {
            _logger.LogInformation(
                $"Subscribe skipped for '{userReference}': active subscription {active.Id} already exists.");
            return active;
        }

        var subscription = await _billingClient.SubscribeAsync(customerId, planHandle, cancellationToken);

        await PublishSafeAsync(new SubscriptionActivated(userReference, subscription), cancellationToken);

        return subscription;
    }

    public Task<IReadOnlyList<CustomerSubscription>> GetSubscriptionsForUserAsync(string userReference,
        CancellationToken cancellationToken = default)
        => GetSubscriptionsCoreAsync(userReference, cancellationToken);

    public async Task<CustomerSubscription?> GetActiveSubscriptionForUserAsync(string userReference,
        CancellationToken cancellationToken = default)
    {
        var subs = await GetSubscriptionsCoreAsync(userReference, cancellationToken);
        return subs.FirstOrDefault(s => s.IsActive) ?? subs.FirstOrDefault();
    }

    // UC2 — record usage for the signed-in customer's active subscription.
    public async Task<UsageSummary> RecordUsageForUserAsync(string userReference, int quantity, string? memo,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        var active = await GetActiveSubscriptionForUserAsync(userReference, cancellationToken);
        if (active is null || !active.IsActive)
        {
            throw new BillingProviderException(
                "No active subscription was found for this user; usage cannot be recorded.");
        }

        return await RecordUsageAsync(active.Id, quantity, memo, cancellationToken);
    }

    // UC2 — admin path: record usage against any subscription by id.
    public async Task<UsageSummary> RecordUsageAsync(int subscriptionId, int quantity, string? memo,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(subscriptionId, nameof(subscriptionId));
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        // Refuse to record usage unless the configured component is metered.
        await _billingClient.EnsureMeteredComponentAsync(cancellationToken);

        var recorded = await _billingClient.RecordUsageAsync(subscriptionId, quantity, memo, cancellationToken);

        // Read-back of the running total is best-effort: the usage already stands.
        int? periodToDate;
        try
        {
            periodToDate = await _billingClient.GetPeriodToDateUsageAsync(subscriptionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Usage recorded for subscription {subscriptionId} but period-to-date " +
                               $"read-back failed: {ex.Message}");
            periodToDate = null;
        }

        return new UsageSummary
        {
            SubscriptionId = subscriptionId,
            RecordedQuantity = recorded,
            PeriodToDateTotal = periodToDate,
            Memo = memo
        };
    }

    // UC2 hook — one order placed => one billable unit. Best-effort by contract:
    // a billing failure here must never surface to (or roll back) the order flow.
    public async Task RecordOrderUsageAsync(string userReference, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(userReference))
            {
                return;
            }

            var active = await GetActiveSubscriptionForUserAsync(userReference, cancellationToken);
            if (active is null || !active.IsActive)
            {
                return;
            }

            await _billingClient.EnsureMeteredComponentAsync(cancellationToken);
            await _billingClient.RecordUsageAsync(active.Id, 1, "eShopOnWeb order placed", cancellationToken);
            _logger.LogInformation(
                $"Recorded 1 usage unit against subscription {active.Id} for order placed by '{userReference}'.");
        }
        catch (Exception ex)
        {
            // Swallow: usage metering is additive and must not affect the order.
            _logger.LogWarning($"Automatic order-usage recording failed for '{userReference}': {ex.Message}");
        }
    }

    // UC3 — preview a plan change.
    public async Task<PlanChangePreview> PreviewPlanChangeForUserAsync(string userReference, string targetPlanHandle,
        bool applyNow, CancellationToken cancellationToken = default)
    {
        var (subscription, currentHandle) = await ResolveChangeableSubscriptionAsync(userReference, targetPlanHandle,
            cancellationToken);
        return await _billingClient.PreviewPlanChangeAsync(subscription.Id, currentHandle, targetPlanHandle, applyNow,
            cancellationToken);
    }

    // UC3 — commit a plan change; reject if the preview is stale.
    public async Task<CustomerSubscription> ChangePlanForUserAsync(string userReference, string targetPlanHandle,
        bool applyNow, long confirmedAmountDueInCents, CancellationToken cancellationToken = default)
    {
        var (subscription, currentHandle) = await ResolveChangeableSubscriptionAsync(userReference, targetPlanHandle,
            cancellationToken);

        // Re-price at commit time and reject if it no longer matches what the
        // customer confirmed — never silently apply a different amount (UC3).
        var fresh = await _billingClient.PreviewPlanChangeAsync(subscription.Id, currentHandle, targetPlanHandle,
            applyNow, cancellationToken);
        if (fresh.PaymentDueInCents != confirmedAmountDueInCents)
        {
            throw new BillingProviderException(
                "The plan-change preview is no longer valid (the prorated amount changed). " +
                "Please review the updated preview and confirm again.");
        }

        var updated = await _billingClient.ChangePlanAsync(subscription.Id, targetPlanHandle, applyNow,
            cancellationToken);

        await PublishSafeAsync(
            new SubscriptionPlanChanged(subscription.Id, currentHandle, targetPlanHandle, applyNow),
            cancellationToken);

        return updated;
    }

    // UC4 — lifecycle for the signed-in customer's subscription.
    public async Task<CustomerSubscription> ChangeLifecycleForUserAsync(string userReference,
        SubscriptionLifecycleAction action, bool endOfPeriod, string? reason,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));

        var subs = await GetSubscriptionsCoreAsync(userReference, cancellationToken);
        var target = SelectForAction(subs, action);
        if (target is null)
        {
            throw new BillingProviderException("No subscription was found for this user for the requested action.");
        }

        return await ApplyLifecycleAsync(target, action, endOfPeriod, reason, cancellationToken);
    }

    // UC4 — admin path by subscription id.
    public async Task<CustomerSubscription> ChangeLifecycleAsync(int subscriptionId,
        SubscriptionLifecycleAction action, bool endOfPeriod, string? reason,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(subscriptionId, nameof(subscriptionId));

        var subscription = await _billingClient.GetSubscriptionAsync(subscriptionId, cancellationToken);
        if (subscription is null)
        {
            throw new BillingProviderException($"Subscription {subscriptionId} was not found.");
        }

        return await ApplyLifecycleAsync(subscription, action, endOfPeriod, reason, cancellationToken);
    }

    private async Task<CustomerSubscription> ApplyLifecycleAsync(CustomerSubscription subscription,
        SubscriptionLifecycleAction action, bool endOfPeriod, string? reason, CancellationToken cancellationToken)
    {
        EnsureTransitionLegal(subscription, action);

        var oldState = subscription.State;
        var updated = action switch
        {
            SubscriptionLifecycleAction.Pause => await _billingClient.PauseAsync(subscription.Id, cancellationToken),
            SubscriptionLifecycleAction.Resume => await _billingClient.ResumeAsync(subscription.Id, cancellationToken),
            SubscriptionLifecycleAction.Cancel => await _billingClient.CancelAsync(subscription.Id, endOfPeriod, reason,
                cancellationToken),
            SubscriptionLifecycleAction.Reactivate => await _billingClient.ReactivateAsync(subscription.Id,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown lifecycle action.")
        };

        await PublishSafeAsync(
            new SubscriptionStateChanged(subscription.Id, action, oldState, updated.State),
            cancellationToken);

        return updated;
    }

    private async Task<IReadOnlyList<CustomerSubscription>> GetSubscriptionsCoreAsync(string userReference,
        CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));

        var customerId = await _billingClient.FindCustomerIdAsync(userReference, cancellationToken);
        if (customerId is null)
        {
            return Array.Empty<CustomerSubscription>();
        }

        return await _billingClient.ListCustomerSubscriptionsAsync(customerId.Value, cancellationToken);
    }

    private async Task<(CustomerSubscription subscription, string currentHandle)> ResolveChangeableSubscriptionAsync(
        string userReference, string targetPlanHandle, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));
        Guard.Against.NullOrEmpty(targetPlanHandle, nameof(targetPlanHandle));

        var subscription = await GetActiveSubscriptionForUserAsync(userReference, cancellationToken);
        if (subscription is null)
        {
            throw new BillingProviderException("No subscription was found for this user.");
        }

        if (!subscription.CanChangePlan)
        {
            throw new BillingProviderException(
                $"A plan change is not allowed while the subscription is '{subscription.State}'. " +
                "Reactivate the subscription first.");
        }

        var currentHandle = subscription.PlanHandle ?? string.Empty;
        if (string.Equals(currentHandle, targetPlanHandle, StringComparison.OrdinalIgnoreCase))
        {
            throw new BillingProviderException("The subscription is already on the requested plan.");
        }

        // Validate the target resolves before doing anything else (config error -> UC0).
        var targetPlan = await _billingClient.FindPlanAsync(targetPlanHandle, cancellationToken);
        if (targetPlan is null)
        {
            throw new BillingConfigurationException(
                $"The target plan '{targetPlanHandle}' does not resolve on the billing provider (see plan UC0).");
        }

        return (subscription, currentHandle);
    }

    private static void EnsureTransitionLegal(CustomerSubscription subscription, SubscriptionLifecycleAction action)
    {
        var legal = action switch
        {
            SubscriptionLifecycleAction.Pause => subscription.CanPause,
            SubscriptionLifecycleAction.Resume => subscription.CanResume,
            SubscriptionLifecycleAction.Cancel => subscription.CanCancel,
            SubscriptionLifecycleAction.Reactivate => subscription.CanReactivate,
            _ => false
        };

        if (!legal)
        {
            throw new BillingProviderException(
                $"Cannot {action.ToString().ToLowerInvariant()} a subscription in state '{subscription.State}'.");
        }
    }

    private static CustomerSubscription? SelectForAction(IReadOnlyList<CustomerSubscription> subs,
        SubscriptionLifecycleAction action)
    {
        return action switch
        {
            SubscriptionLifecycleAction.Reactivate => subs.FirstOrDefault(s => s.IsCanceled) ?? subs.FirstOrDefault(),
            SubscriptionLifecycleAction.Resume => subs.FirstOrDefault(s => s.IsPaused) ?? subs.FirstOrDefault(),
            _ => subs.FirstOrDefault(s => s.IsActive) ?? subs.FirstOrDefault(s => s.IsPaused) ?? subs.FirstOrDefault()
        };
    }

    private async Task PublishSafeAsync(INotification notification, CancellationToken cancellationToken)
    {
        // Best-effort in-process eventing (plan §2.5): a handler failure never
        // rolls back the successful provider call.
        try
        {
            await _publisher.Publish(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Publishing {notification.GetType().Name} failed: {ex.Message}");
        }
    }

    private static (string firstName, string lastName) DeriveName(string userReference)
    {
        var local = userReference.Contains('@')
            ? userReference[..userReference.IndexOf('@')]
            : userReference;
        var firstName = string.IsNullOrWhiteSpace(local) ? "eShopOnWeb" : local;
        return (firstName, "Customer");
    }
}
