using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.MaxioBillingTestApi.Models;

// Request DTOs whose shape mirrors the corresponding Maxio Advanced Billing API operation input
// (the snake_case envelopes Maxio itself accepts), NOT merely the current client method signature.
// This keeps the external microservice contract stable. Each action binds one of these and maps its
// fields onto the client method's parameters — no validation, coercion, or defaulting here.

/// <summary>POST /customers.json — <c>{ "customer": { ... } }</c>.</summary>
public sealed class CreateCustomerRequest
{
    [JsonPropertyName("customer")] public CustomerBody? Customer { get; set; }

    public sealed class CustomerBody
    {
        [JsonPropertyName("reference")] public string? Reference { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
    }
}

/// <summary>POST /subscriptions.json — <c>{ "subscription": { ... } }</c>.</summary>
public sealed class CreateSubscriptionRequest
{
    [JsonPropertyName("subscription")] public SubscriptionBody? Subscription { get; set; }

    public sealed class SubscriptionBody
    {
        [JsonPropertyName("customer_id")] public int CustomerId { get; set; }
        [JsonPropertyName("product_handle")] public string? ProductHandle { get; set; }
    }
}

/// <summary>
/// POST /subscriptions/{id}/migrations[/preview].json — <c>{ "migration": { ... } }</c>.
/// Used by both the preview and the (immediate) commit routes.
/// </summary>
public sealed class MigrationRequest
{
    [JsonPropertyName("migration")] public MigrationBody? Migration { get; set; }

    public sealed class MigrationBody
    {
        [JsonPropertyName("product_handle")] public string? ProductHandle { get; set; }
        // Accepted to mirror the Maxio input; the client fixes preserve_period=true itself.
        [JsonPropertyName("preserve_period")] public bool? PreservePeriod { get; set; }
    }
}

/// <summary>POST /subscriptions/{id}/components/{component_id}/usages.json — <c>{ "usage": { ... } }</c>.</summary>
public sealed class RecordUsageRequest
{
    [JsonPropertyName("usage")] public UsageBody? Usage { get; set; }

    public sealed class UsageBody
    {
        [JsonPropertyName("quantity")] public int Quantity { get; set; }
        [JsonPropertyName("memo")] public string? Memo { get; set; }
    }
}

/// <summary>DELETE /subscriptions/{id}.json — <c>{ "subscription": { "cancellation_message": ... } }</c>.</summary>
public sealed class CancelSubscriptionRequest
{
    [JsonPropertyName("subscription")] public CancelBody? Subscription { get; set; }

    public sealed class CancelBody
    {
        [JsonPropertyName("cancellation_message")] public string? CancellationMessage { get; set; }
    }
}
