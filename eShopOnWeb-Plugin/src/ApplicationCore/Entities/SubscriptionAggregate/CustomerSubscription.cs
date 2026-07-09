using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

// Provider-agnostic view of a single subscription belonging to an eShopOnWeb
// customer. Read from the billing provider (the system of record) and never
// persisted locally — the state helpers below encode the legal lifecycle
// transitions the service enforces before calling the provider (UC4).
public record CustomerSubscription
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public string? CustomerReference { get; init; }

    public string? PlanHandle { get; init; }
    public string? PlanName { get; init; }
    public long? PriceInCents { get; init; }

    // The raw provider state (e.g. "active", "on_hold", "canceled").
    public string State { get; init; } = string.Empty;

    public DateTimeOffset? CurrentPeriodEndsAt { get; init; }
    public bool CancelAtEndOfPeriod { get; init; }
    public DateTimeOffset? CanceledAt { get; init; }
    public DateTimeOffset? DelayedCancelAt { get; init; }

    // When a delayed plan change is scheduled, the handle it will move to at renewal.
    public string? NextPlanHandle { get; init; }

    public decimal? Price => PriceInCents.HasValue ? PriceInCents.Value / 100m : (decimal?)null;

    public bool IsActive =>
        State.Equals("active", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("trialing", StringComparison.OrdinalIgnoreCase);

    public bool IsPaused =>
        State.Equals("on_hold", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("paused", StringComparison.OrdinalIgnoreCase);

    public bool IsCanceled =>
        State.Equals("canceled", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("expired", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("trial_ended", StringComparison.OrdinalIgnoreCase);

    public bool CanPause => IsActive;
    public bool CanResume => IsPaused;
    public bool CanCancel => IsActive || IsPaused;
    public bool CanReactivate => IsCanceled;
    public bool CanChangePlan => IsActive;
}
