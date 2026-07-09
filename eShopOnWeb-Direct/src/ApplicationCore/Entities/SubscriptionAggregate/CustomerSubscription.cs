using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// A customer's subscription as reported by the billing provider. The provider is the
/// system of record (the mapping between the eShopOnWeb user and this subscription is kept
/// stateless — the user's email/username is the provider-side customer reference, see plan §8),
/// so this type is a normalized read model rather than a persisted EF entity.
/// </summary>
public class CustomerSubscription
{
    public int Id { get; init; }
    public SubscriptionState State { get; init; }

    public int CustomerId { get; init; }
    public string? CustomerReference { get; init; }

    public string ProductHandle { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int ProductPriceInCents { get; init; }
    public int Interval { get; init; }
    public string IntervalUnit { get; init; } = string.Empty;

    public DateTimeOffset? CurrentPeriodEndsAt { get; init; }
    public DateTimeOffset? NextAssessmentAt { get; init; }
    public bool CancelAtEndOfPeriod { get; init; }
    public DateTimeOffset? CanceledAt { get; init; }
    public DateTimeOffset? DelayedCancelAt { get; init; }
    public DateTimeOffset? AutomaticallyResumeAt { get; init; }

    /// <summary>Convenience view of <see cref="ProductPriceInCents"/> as a decimal amount.</summary>
    public decimal ProductPrice => ProductPriceInCents / 100m;

    public bool IsActive => State == SubscriptionState.Active || State == SubscriptionState.Trialing;
}
