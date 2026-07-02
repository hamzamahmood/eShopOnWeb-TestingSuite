using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The single seam through which this application talks to the billing provider (Maxio Advanced Billing).
/// No other type in this codebase may reference the billing provider's SDK. Every member here is expressed
/// in provider-agnostic primitives/DTOs only, so <c>ApplicationCore</c> carries no compile-time dependency on
/// the provider. The implementation lives in <c>Infrastructure</c>.
/// </summary>
public interface IBillingClient
{
    /// <summary>Lists the recurring plans available for subscription.</summary>
    Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the provider-side customer id for <paramref name="customerReference"/> (the stable eShopOnWeb
    /// user id), creating the customer if one does not already exist. Idempotent: repeat calls for the same
    /// reference never create a duplicate provider customer (AC-03).
    /// </summary>
    Task<string> FindOrCreateCustomerAsync(string customerReference, string email, string? firstName, string? lastName, CancellationToken cancellationToken);

    /// <summary>Read-only lookup of the provider customer id for a reference; <c>null</c> if none exists yet. Never creates one.</summary>
    Task<string?> FindCustomerIdAsync(string customerReference, CancellationToken cancellationToken);

    /// <summary>Enrolls the given provider customer id in the plan identified by <paramref name="productHandle"/>.</summary>
    Task<SubscriptionDto> CreateSubscriptionAsync(string providerCustomerId, string productHandle, CancellationToken cancellationToken);

    /// <summary>Reads back a subscription by its provider id.</summary>
    Task<SubscriptionDto> ReadSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Lists every subscription belonging to the given provider customer id.</summary>
    Task<IReadOnlyList<SubscriptionDto>> ListCustomerSubscriptionsAsync(string providerCustomerId, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the configured metered-component handle exists, is of metered kind, and belongs to the
    /// configured product family. Throws <c>MeteredComponentMisconfiguredException</c> otherwise. Must be
    /// called (and pass) before any usage is ever sent to the provider (UC2 precondition).
    /// </summary>
    Task VerifyMeteredComponentAsync(CancellationToken cancellationToken);

    /// <summary>Records a quantity of usage against the configured metered component on a subscription.</summary>
    Task<UsageDto> RecordUsageAsync(string subscriptionId, decimal quantity, string? memo, CancellationToken cancellationToken);

    /// <summary>Reads the period-to-date usage total for the configured metered component on a subscription.</summary>
    Task<UsageSummaryDto> GetUsageSummaryAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Quotes the cost of moving a subscription to a different plan, with the given timing.</summary>
    Task<PlanChangeQuoteDto> PreviewPlanChangeAsync(string subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken);

    /// <summary>Commits a previously previewed plan change.</summary>
    Task<SubscriptionDto> CommitPlanChangeAsync(string subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken);

    Task<SubscriptionDto> PauseAsync(string subscriptionId, CancellationToken cancellationToken);

    Task<SubscriptionDto> ResumeAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>Cancels a subscription, either immediately or scheduled for the end of the current period.</summary>
    Task<SubscriptionDto> CancelAsync(string subscriptionId, CancelTiming timing, string? reason, CancellationToken cancellationToken);

    Task<SubscriptionDto> ReactivateAsync(string subscriptionId, CancellationToken cancellationToken);
}
