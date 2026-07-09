using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.Services;

// Internal wire-format DTOs for the Maxio Advanced Billing HTTP API. They are deliberately kept
// internal to Infrastructure so the provider's shapes never leak into ApplicationCore; the billing
// client maps them onto the provider-agnostic domain models. Maxio wraps single resources under a
// snake_case key (e.g. { "customer": {...} }) and returns lists as bare arrays of those wrappers.

internal sealed class CustomerWrapper
{
    [JsonPropertyName("customer")] public MaxioCustomer? Customer { get; set; }
}

internal sealed class MaxioCustomer
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("reference")] public string? Reference { get; set; }
}

internal sealed class SubscriptionWrapper
{
    [JsonPropertyName("subscription")] public MaxioSubscription? Subscription { get; set; }
}

internal sealed class MaxioSubscription
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("product_price_in_cents")] public int ProductPriceInCents { get; set; }
    [JsonPropertyName("current_period_ends_at")] public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    [JsonPropertyName("next_assessment_at")] public DateTimeOffset? NextAssessmentAt { get; set; }
    [JsonPropertyName("cancel_at_end_of_period")] public bool? CancelAtEndOfPeriod { get; set; }
    [JsonPropertyName("canceled_at")] public DateTimeOffset? CanceledAt { get; set; }
    [JsonPropertyName("delayed_cancel_at")] public DateTimeOffset? DelayedCancelAt { get; set; }
    [JsonPropertyName("automatically_resume_at")] public DateTimeOffset? AutomaticallyResumeAt { get; set; }
    [JsonPropertyName("customer")] public MaxioCustomer? Customer { get; set; }
    [JsonPropertyName("product")] public MaxioProduct? Product { get; set; }
}

internal sealed class ProductWrapper
{
    [JsonPropertyName("product")] public MaxioProduct? Product { get; set; }
}

internal sealed class MaxioProduct
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("handle")] public string? Handle { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("price_in_cents")] public int PriceInCents { get; set; }
    [JsonPropertyName("interval")] public int Interval { get; set; }
    [JsonPropertyName("interval_unit")] public string? IntervalUnit { get; set; }
    [JsonPropertyName("archived_at")] public DateTimeOffset? ArchivedAt { get; set; }
    [JsonPropertyName("product_family")] public MaxioProductFamily? ProductFamily { get; set; }
}

internal sealed class MaxioProductFamily
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("handle")] public string? Handle { get; set; }
}

internal sealed class ComponentWrapper
{
    [JsonPropertyName("component")] public MaxioComponent? Component { get; set; }
}

internal sealed class MaxioComponent
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("handle")] public string? Handle { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }
}

// GET /subscriptions/{id}/components/{component_id}.json is also wrapped under "component".
internal sealed class SubscriptionComponentWrapper
{
    [JsonPropertyName("component")] public MaxioSubscriptionComponent? Component { get; set; }
}

internal sealed class MaxioSubscriptionComponent
{
    [JsonPropertyName("component_id")] public int ComponentId { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }

    // For a metered component this is the running period-to-date total of billable units.
    [JsonPropertyName("unit_balance")] public int UnitBalance { get; set; }
}

internal sealed class MigrationPreviewWrapper
{
    [JsonPropertyName("migration")] public MaxioMigrationPreview? Migration { get; set; }
}

internal sealed class MaxioMigrationPreview
{
    [JsonPropertyName("prorated_adjustment_in_cents")] public int ProratedAdjustmentInCents { get; set; }
    [JsonPropertyName("charge_in_cents")] public int ChargeInCents { get; set; }
    [JsonPropertyName("payment_due_in_cents")] public int PaymentDueInCents { get; set; }
    [JsonPropertyName("credit_applied_in_cents")] public int CreditAppliedInCents { get; set; }
}
