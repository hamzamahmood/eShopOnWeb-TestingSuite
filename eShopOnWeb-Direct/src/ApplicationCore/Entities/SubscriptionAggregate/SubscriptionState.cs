namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// Provider-agnostic lifecycle state of a subscription. The billing provider is the
/// system of record; its native states are normalized onto this enum by the billing client
/// so nothing outside Infrastructure depends on the provider's own vocabulary.
/// </summary>
public enum SubscriptionState
{
    Unknown = 0,
    Pending,
    Trialing,
    Active,
    PastDue,
    Suspended,
    Canceled,
    Expired,
    Unpaid,
    TrialEnded,
    OnHold,
    Paused,
    AwaitingSignup
}
