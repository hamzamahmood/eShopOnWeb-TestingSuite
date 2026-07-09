namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// A recurring plan a customer can subscribe to (a "product" in the billing provider).
/// Provider-agnostic; populated by the billing client from the provider's product catalog.
/// </summary>
public class SubscriptionPlan
{
    public int Id { get; init; }
    public string Handle { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>Recurring price in integer cents (the provider's native money representation).</summary>
    public int PriceInCents { get; init; }

    /// <summary>Numeric part of the billing interval (e.g. 1 in "every 1 month").</summary>
    public int Interval { get; init; }

    /// <summary>Unit part of the billing interval (e.g. "month" or "day").</summary>
    public string IntervalUnit { get; init; } = string.Empty;

    public string ProductFamilyHandle { get; init; } = string.Empty;

    /// <summary>Convenience view of <see cref="PriceInCents"/> as a decimal amount.</summary>
    public decimal Price => PriceInCents / 100m;
}
