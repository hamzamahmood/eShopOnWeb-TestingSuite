using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IBillingClient _billingClient;
    private readonly IPublisher _publisher;
    private readonly IIdempotencyCache _idempotencyCache;
    private readonly IAppLogger<SubscriptionService> _logger;

    public SubscriptionService(
        IBillingClient billingClient,
        IPublisher publisher,
        IIdempotencyCache idempotencyCache,
        IAppLogger<SubscriptionService> logger)
    {
        _billingClient = billingClient;
        _publisher = publisher;
        _idempotencyCache = idempotencyCache;
        _logger = logger;
    }

    public Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken cancellationToken) =>
        _billingClient.ListPlansAsync(cancellationToken);

    public async Task<SubscriptionDto> SubscribeAsync(string userId, string email, string? firstName, string? lastName, string productHandle, CancellationToken cancellationToken)
    {
        var providerCustomerId = await _billingClient.FindOrCreateCustomerAsync(userId, email, firstName, lastName, cancellationToken);
        var subscription = await _billingClient.CreateSubscriptionAsync(providerCustomerId, productHandle, cancellationToken);

        await _publisher.Publish(new SubscriptionActivated(userId, subscription.SubscriptionId, subscription.ProductHandle), cancellationToken);
        _logger.LogInformation("User {UserId} subscribed to plan {ProductHandle} (subscription {SubscriptionId})", userId, productHandle, subscription.SubscriptionId);

        return subscription;
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListMySubscriptionsAsync(string userId, CancellationToken cancellationToken)
    {
        var providerCustomerId = await _billingClient.FindCustomerIdAsync(userId, cancellationToken);
        if (providerCustomerId is null)
        {
            return System.Array.Empty<SubscriptionDto>();
        }

        return await _billingClient.ListCustomerSubscriptionsAsync(providerCustomerId, cancellationToken);
    }

    public async Task<UsageDto> RecordUsageAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, decimal quantity, string? memo, string requestId, CancellationToken cancellationToken)
    {
        await _billingClient.VerifyMeteredComponentAsync(cancellationToken);
        await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);

        if (!_idempotencyCache.TryClaim($"usage:{subscriptionId}:{requestId}"))
        {
            _logger.LogInformation("Duplicate usage request {RequestId} for subscription {SubscriptionId} ignored", requestId, subscriptionId);
            return new UsageDto("duplicate", 0m, "Duplicate request; no additional usage recorded.", null);
        }

        var usage = await _billingClient.RecordUsageAsync(subscriptionId, quantity, memo, cancellationToken);
        _logger.LogInformation("Recorded {Quantity} usage unit(s) on subscription {SubscriptionId}", quantity, subscriptionId);
        return usage;
    }

    public async Task RecordUsageForUserIfSubscribedAsync(string userId, decimal quantity, string? memo, string requestId, CancellationToken cancellationToken)
    {
        var providerCustomerId = await _billingClient.FindCustomerIdAsync(userId, cancellationToken);
        if (providerCustomerId is null)
        {
            _logger.LogInformation("User {UserId} has no billing-provider customer record; skipping automatic usage hook", userId);
            return;
        }

        var subscriptions = await _billingClient.ListCustomerSubscriptionsAsync(providerCustomerId, cancellationToken);
        var active = subscriptions.FirstOrDefault(s => s.State == SubscriptionState.Active);
        if (active is null)
        {
            _logger.LogInformation("User {UserId} has no active subscription; skipping automatic usage hook", userId);
            return;
        }

        await RecordUsageAsync(active.SubscriptionId, userId, callerIsAdmin: false, quantity, memo, requestId, cancellationToken);
    }

    public async Task<UsageSummaryDto> GetUsageSummaryAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken)
    {
        await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);
        return await _billingClient.GetUsageSummaryAsync(subscriptionId, cancellationToken);
    }

    public async Task<ProrationPreviewDto> PreviewPlanChangeAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken)
    {
        await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);

        var quote = await _billingClient.PreviewPlanChangeAsync(subscriptionId, targetProductHandle, timing, cancellationToken);
        var preview = new ProrationPreviewDto(quote.SubscriptionId, quote.FromProductHandle, quote.ToProductHandle, quote.Timing, quote.ProratedAmount, quote.EffectiveAt, string.Empty);
        var token = _idempotencyCache.StorePreview(preview);
        return preview with { PreviewToken = token };
    }

    public async Task<SubscriptionDto> CommitPlanChangeAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, string previewToken, CancellationToken cancellationToken)
    {
        await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);

        var cachedPreview = _idempotencyCache.TakePreview(previewToken);
        if (cachedPreview is null || cachedPreview.SubscriptionId != subscriptionId)
        {
            throw new StalePreviewException();
        }

        var freshQuote = await _billingClient.PreviewPlanChangeAsync(subscriptionId, cachedPreview.ToProductHandle, cachedPreview.Timing, cancellationToken);
        if (freshQuote.ProratedAmount != cachedPreview.ProratedAmount)
        {
            throw new StalePreviewException();
        }

        var result = await _billingClient.CommitPlanChangeAsync(subscriptionId, cachedPreview.ToProductHandle, cachedPreview.Timing, cancellationToken);

        await _publisher.Publish(new SubscriptionPlanChanged(result.CustomerReference, subscriptionId, cachedPreview.FromProductHandle, cachedPreview.ToProductHandle, cachedPreview.ProratedAmount), cancellationToken);
        _logger.LogInformation("Subscription {SubscriptionId} changed plan {OldPlan} -> {NewPlan}", subscriptionId, cachedPreview.FromProductHandle, cachedPreview.ToProductHandle);

        return result;
    }

    public async Task<SubscriptionDto> PauseAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken)
    {
        var current = await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);
        if (current.State != SubscriptionState.Active)
        {
            throw new IllegalSubscriptionTransitionException(subscriptionId, "pause", current.State);
        }

        var result = await _billingClient.PauseAsync(subscriptionId, cancellationToken);
        await PublishStateChangedAsync(result, current.State, cancellationToken);
        return result;
    }

    public async Task<SubscriptionDto> ResumeAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken)
    {
        var current = await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);
        if (current.State != SubscriptionState.OnHold)
        {
            throw new IllegalSubscriptionTransitionException(subscriptionId, "resume", current.State);
        }

        var result = await _billingClient.ResumeAsync(subscriptionId, cancellationToken);
        await PublishStateChangedAsync(result, current.State, cancellationToken);
        return result;
    }

    public async Task<SubscriptionDto> CancelAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancelTiming timing, string? reason, CancellationToken cancellationToken)
    {
        var current = await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);
        if (current.State is SubscriptionState.Canceled or SubscriptionState.Expired)
        {
            throw new IllegalSubscriptionTransitionException(subscriptionId, "cancel", current.State);
        }

        var result = await _billingClient.CancelAsync(subscriptionId, timing, reason, cancellationToken);
        await PublishStateChangedAsync(result, current.State, cancellationToken);
        return result;
    }

    public async Task<SubscriptionDto> ReactivateAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken)
    {
        var current = await GetOwnedSubscriptionAsync(subscriptionId, callerUserId, callerIsAdmin, cancellationToken);
        if (current.State != SubscriptionState.Canceled)
        {
            throw new IllegalSubscriptionTransitionException(subscriptionId, "reactivate", current.State);
        }

        var result = await _billingClient.ReactivateAsync(subscriptionId, cancellationToken);
        await PublishStateChangedAsync(result, current.State, cancellationToken);
        return result;
    }

    private async Task<SubscriptionDto> GetOwnedSubscriptionAsync(string subscriptionId, string callerUserId, bool callerIsAdmin, CancellationToken cancellationToken)
    {
        var subscription = await _billingClient.ReadSubscriptionAsync(subscriptionId, cancellationToken);
        if (!callerIsAdmin && subscription.CustomerReference != callerUserId)
        {
            // Deliberately the same exception as "does not exist" - a non-admin must not learn that a
            // subscription id belonging to someone else exists.
            throw new SubscriptionNotFoundException(subscriptionId);
        }

        return subscription;
    }

    private Task PublishStateChangedAsync(SubscriptionDto result, SubscriptionState oldState, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription {SubscriptionId} transitioned {OldState} -> {NewState}", result.SubscriptionId, oldState, result.State);
        return _publisher.Publish(new SubscriptionStateChanged(result.CustomerReference, result.SubscriptionId, oldState, result.State), cancellationToken);
    }
}
