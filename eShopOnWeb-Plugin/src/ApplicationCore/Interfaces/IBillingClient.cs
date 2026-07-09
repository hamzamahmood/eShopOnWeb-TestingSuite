using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// The provider-agnostic seam to the billing engine. This is the single
// abstraction the domain depends on; the one concrete implementation
// (MaxioBillingClient in Infrastructure) is the only place the provider is
// touched. Nothing above this interface knows the provider exists.
public interface IBillingClient
{
    // UC1 — plans & enrollment
    Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> FindPlanAsync(string planHandle, CancellationToken cancellationToken = default);

    // Idempotent on the user reference: creates the provider-side customer only
    // if one does not already exist for that reference, and returns its id.
    Task<int> EnsureCustomerAsync(string reference, string email, string firstName, string lastName,
        CancellationToken cancellationToken = default);
    Task<int?> FindCustomerIdAsync(string reference, CancellationToken cancellationToken = default);

    Task<CustomerSubscription> SubscribeAsync(int customerId, string planHandle,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerSubscription>> ListCustomerSubscriptionsAsync(int customerId,
        CancellationToken cancellationToken = default);
    Task<CustomerSubscription?> GetSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default);

    // UC2 — pay-as-you-go usage
    // Verifies the configured metered component resolves to a metered-kind
    // component on the family; throws BillingConfigurationException otherwise.
    Task EnsureMeteredComponentAsync(CancellationToken cancellationToken = default);
    Task<int> RecordUsageAsync(int subscriptionId, int quantity, string? memo,
        CancellationToken cancellationToken = default);
    Task<int?> GetPeriodToDateUsageAsync(int subscriptionId, CancellationToken cancellationToken = default);

    // UC3 — plan change
    Task<PlanChangePreview> PreviewPlanChangeAsync(int subscriptionId, string fromPlanHandle, string toPlanHandle,
        bool applyNow, CancellationToken cancellationToken = default);
    Task<CustomerSubscription> ChangePlanAsync(int subscriptionId, string toPlanHandle, bool applyNow,
        CancellationToken cancellationToken = default);

    // UC4 — lifecycle
    Task<CustomerSubscription> PauseAsync(int subscriptionId, CancellationToken cancellationToken = default);
    Task<CustomerSubscription> ResumeAsync(int subscriptionId, CancellationToken cancellationToken = default);
    Task<CustomerSubscription> CancelAsync(int subscriptionId, bool endOfPeriod, string? reason,
        CancellationToken cancellationToken = default);
    Task<CustomerSubscription> ReactivateAsync(int subscriptionId, CancellationToken cancellationToken = default);
}
