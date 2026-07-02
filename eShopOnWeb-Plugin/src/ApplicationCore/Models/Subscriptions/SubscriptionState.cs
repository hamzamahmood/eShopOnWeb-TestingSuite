namespace Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

/// <summary>
/// Provider-agnostic subscription lifecycle state. <see cref="Other"/> is the forward-compatible
/// catch-all for any billing-provider state this integration does not otherwise reason about.
/// </summary>
public enum SubscriptionState
{
    Active,
    Trialing,
    OnHold,
    PastDue,
    Canceled,
    Unpaid,
    Expired,
    Other
}
