namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

// Provider-agnostic view of a recurring plan a customer can subscribe to.
// Populated from the billing provider on demand and never persisted — the
// integration keeps a stateless mapping (see plan §8), so the billing provider
// remains the single system of record for subscriptions.
public record SubscriptionPlan
{
    public int Id { get; init; }
    public string Handle { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    // The recurring price, expressed in integer cents (the provider's native unit).
    public long PriceInCents { get; init; }

    // The recurring billing interval, e.g. 1 "month".
    public int Interval { get; init; }
    public string IntervalUnit { get; init; } = "month";

    public decimal Price => PriceInCents / 100m;
}
