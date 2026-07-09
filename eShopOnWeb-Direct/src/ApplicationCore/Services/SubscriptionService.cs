using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the subscription use cases (mirrors <see cref="OrderService"/>): it validates input,
/// drives the provider-agnostic <see cref="IBillingClient"/>, and publishes in-process MediatR
/// notifications on meaningful state changes. Publication is best-effort (§2.5): a handler failure is
/// logged and never rolls back a successful provider call.
/// </summary>
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

    public Task<IReadOnlyCollection<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default)
        => _billingClient.ListPlansAsync(cancellationToken);

    public async Task<CustomerSubscription> SubscribeAsync(string userReference, string email, string productHandle, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));
        Guard.Against.NullOrEmpty(email, nameof(email));
        Guard.Against.NullOrEmpty(productHandle, nameof(productHandle));

        // Ensure the provider-side customer exists (idempotent on the user reference).
        var customerId = await _billingClient.EnsureCustomerAsync(userReference, email, null, null, cancellationToken);

        // Duplicate-subscribe protection: if this customer is already actively enrolled in the
        // chosen plan, return that enrollment rather than creating a second one.
        var existing = await _billingClient.GetSubscriptionsForCustomerAsync(customerId, cancellationToken);
        var alreadyActive = existing.FirstOrDefault(s => s.IsActive &&
            string.Equals(s.ProductHandle, productHandle, StringComparison.OrdinalIgnoreCase));
        if (alreadyActive is not null)
        {
            return alreadyActive;
        }

        var subscription = await _billingClient.CreateSubscriptionAsync(customerId, productHandle, cancellationToken);

        await PublishBestEffortAsync(new SubscriptionActivated(userReference, subscription), cancellationToken);

        return subscription;
    }

    public async Task<IReadOnlyCollection<CustomerSubscription>> GetMySubscriptionsAsync(string userReference, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(userReference, nameof(userReference));

        var customerId = await _billingClient.FindCustomerIdByReferenceAsync(userReference, cancellationToken);
        if (customerId is null)
        {
            return Array.Empty<CustomerSubscription>();
        }

        return await _billingClient.GetSubscriptionsForCustomerAsync(customerId.Value, cancellationToken);
    }

    public async Task<UsageResult> RecordUsageAsync(int subscriptionId, int quantity, string? memo, string? ownerReference, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(subscriptionId, nameof(subscriptionId));
        // Reject invalid quantity before any provider call (UC2 failure scenario).
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        // Startup / first-call validation: the configured usage component must resolve and be metered,
        // otherwise we refuse to record usage (UC2 preconditions & failure scenarios).
        var component = await _billingClient.GetMeteredComponentAsync(cancellationToken);
        if (!component.IsMetered)
        {
            throw new BillingProviderException(
                $"The configured usage component '{component.Handle}' is not a metered component (kind: {component.Kind}). " +
                "Correct the seed so it is a metered-kind component (see plan UC0) before recording usage.");
        }

        // The subscription must exist and be active, and (for customer self-service) must belong to the caller.
        var subscription = await _billingClient.GetSubscriptionAsync(subscriptionId, cancellationToken);
        if (!subscription.IsActive)
        {
            throw new BillingProviderException(
                $"Subscription {subscriptionId} is not active (state: {subscription.State}); usage cannot be recorded.");
        }

        if (ownerReference is not null)
        {
            var ownerId = await _billingClient.FindCustomerIdByReferenceAsync(ownerReference, cancellationToken);
            if (ownerId is null || ownerId.Value != subscription.CustomerId)
            {
                throw new BillingProviderException($"Subscription {subscriptionId} does not belong to the current user.");
            }
        }

        await _billingClient.RecordUsageAsync(subscriptionId, quantity, memo, cancellationToken);

        // Read back the running period-to-date total. If this fails the usage still stands, so we
        // report success with the total marked unavailable rather than failing the whole operation.
        int? total = null;
        try
        {
            total = await _billingClient.GetUsageTotalAsync(subscriptionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Recorded usage on subscription {0} but failed to read back the period-to-date total: {1}",
                subscriptionId, ex.Message);
        }

        return new UsageResult { RecordedQuantity = quantity, Memo = memo, PeriodToDateTotal = total };
    }

    public async Task<ProrationPreview> PreviewPlanChangeAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(subscriptionId, nameof(subscriptionId));
        Guard.Against.NullOrEmpty(targetProductHandle, nameof(targetProductHandle));

        var subscription = await _billingClient.GetSubscriptionAsync(subscriptionId, cancellationToken);
        GuardPlanChangeAllowed(subscription, targetProductHandle);

        return await _billingClient.PreviewPlanChangeAsync(subscriptionId, targetProductHandle, timing, cancellationToken);
    }

    public async Task<CustomerSubscription> ChangePlanAsync(int subscriptionId, string targetProductHandle, PlanChangeTiming timing, ProrationPreview confirmedPreview, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(subscriptionId, nameof(subscriptionId));
        Guard.Against.NullOrEmpty(targetProductHandle, nameof(targetProductHandle));
        Guard.Against.Null(confirmedPreview, nameof(confirmedPreview));

        var subscription = await _billingClient.GetSubscriptionAsync(subscriptionId, cancellationToken);
        GuardPlanChangeAllowed(subscription, targetProductHandle);
        var oldHandle = subscription.ProductHandle;

        // Reject a stale preview: re-price and require the confirmed amounts to still match, so we never
        // silently apply a different amount than the one shown to the customer (UC3, §6 Phase 4).
        var fresh = await _billingClient.PreviewPlanChangeAsync(subscriptionId, targetProductHandle, timing, cancellationToken);
        if (!PreviewsMatch(confirmedPreview, fresh))
        {
            throw new BillingProviderException(
                "The previewed cost is no longer current. Please review a fresh preview before confirming the plan change.");
        }

        var updated = await _billingClient.ChangePlanAsync(subscriptionId, targetProductHandle, timing, cancellationToken);

        await PublishBestEffortAsync(
            new SubscriptionPlanChanged(subscriptionId, oldHandle, targetProductHandle, timing, updated), cancellationToken);

        return updated;
    }

    public async Task<CustomerSubscription> ChangeLifecycleAsync(int subscriptionId, SubscriptionLifecycleAction action, string? reason, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(subscriptionId, nameof(subscriptionId));

        var subscription = await _billingClient.GetSubscriptionAsync(subscriptionId, cancellationToken);
        var oldState = subscription.State;
        GuardLifecycleAllowed(subscription, action);

        var updated = await _billingClient.ApplyLifecycleActionAsync(subscriptionId, action, reason, cancellationToken);

        await PublishBestEffortAsync(
            new SubscriptionStateChanged(subscriptionId, action, oldState, updated.State, updated), cancellationToken);

        return updated;
    }

    private static bool PreviewsMatch(ProrationPreview a, ProrationPreview b) =>
        a.ChargeInCents == b.ChargeInCents &&
        a.CreditAppliedInCents == b.CreditAppliedInCents &&
        a.PaymentDueInCents == b.PaymentDueInCents &&
        a.ProratedAdjustmentInCents == b.ProratedAdjustmentInCents;

    private static void GuardPlanChangeAllowed(CustomerSubscription subscription, string targetProductHandle)
    {
        if (string.Equals(subscription.ProductHandle, targetProductHandle, StringComparison.OrdinalIgnoreCase))
        {
            throw new BillingProviderException(
                $"The subscription is already on plan '{targetProductHandle}'; nothing to change.");
        }

        // A plan change is only meaningful for a live subscription; a cancelled one must be reactivated first.
        if (subscription.State is SubscriptionState.Canceled or SubscriptionState.Expired)
        {
            throw new BillingProviderException(
                $"Subscription {subscription.Id} is {subscription.State} and cannot change plans. Reactivate it first (UC4).");
        }
    }

    private static void GuardLifecycleAllowed(CustomerSubscription subscription, SubscriptionLifecycleAction action)
    {
        var state = subscription.State;
        var legal = action switch
        {
            SubscriptionLifecycleAction.Pause => state is SubscriptionState.Active or SubscriptionState.Trialing,
            SubscriptionLifecycleAction.Resume => state is SubscriptionState.OnHold or SubscriptionState.Paused,
            SubscriptionLifecycleAction.Cancel => state is not (SubscriptionState.Canceled or SubscriptionState.Expired),
            SubscriptionLifecycleAction.CancelAtEndOfPeriod => state is SubscriptionState.Active or SubscriptionState.Trialing or SubscriptionState.PastDue,
            SubscriptionLifecycleAction.Reactivate => state is SubscriptionState.Canceled or SubscriptionState.Unpaid or SubscriptionState.TrialEnded,
            _ => false
        };

        if (!legal)
        {
            throw new BillingProviderException(
                $"Cannot {action} a subscription that is {state}. Refresh the subscription and choose a transition legal from its current state.");
        }
    }

    private async Task PublishBestEffortAsync(INotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.Publish(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort, in-process only (§2.5): the provider call already succeeded, so a handler
            // failure must not roll it back — log and move on.
            _logger.LogWarning("In-process publication of {0} failed (the state change still stands): {1}",
                notification.GetType().Name, ex.Message);
        }
    }
}
