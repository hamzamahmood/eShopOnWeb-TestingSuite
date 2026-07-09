namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>Provider-agnostic classification of a billable component.</summary>
public enum BillingComponentKind
{
    Unknown = 0,
    Metered,
    QuantityBased,
    OnOff,
    PrepaidUsage,
    EventBased
}

/// <summary>
/// A billable component defined on a product family (e.g. the pay-as-you-go metered add-on).
/// Provider-agnostic; used to validate that the configured usage component really is metered
/// before any usage is recorded (see UC2 preconditions).
/// </summary>
public class BillingComponent
{
    public int Id { get; init; }
    public string Handle { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public BillingComponentKind Kind { get; init; }

    public bool IsMetered => Kind == BillingComponentKind.Metered;
}
