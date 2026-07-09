using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.MaxioBillingTestApi;

// Request DTOs whose shape mirrors the corresponding Maxio Advanced Billing API
// operation input (envelope wrappers + snake_case field names), NOT merely the
// current MaxioBillingClient method signature. The [JsonPropertyName] attributes
// bind the snake_case wire contract; they are per-property mappings to the Maxio
// contract, not a global serializer/naming-policy change. Fields the client does
// not consume are accepted and ignored so binding never breaks.

// POST /customers.json  ->  { "customer": { first_name, last_name, email, reference } }
public class CreateCustomerRequestDto
{
    [JsonPropertyName("customer")]
    public CustomerBodyDto? Customer { get; set; }
}

public class CustomerBodyDto
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }
}

// POST /subscriptions.json  ->  { "subscription": { customer_id, product_handle, ... } }
public class CreateSubscriptionRequestDto
{
    [JsonPropertyName("subscription")]
    public CreateSubscriptionBodyDto? Subscription { get; set; }
}

public class CreateSubscriptionBodyDto
{
    [JsonPropertyName("customer_id")]
    public int CustomerId { get; set; }

    [JsonPropertyName("product_handle")]
    public string? ProductHandle { get; set; }

    // Accepted/ignored — the client fixes the collection method itself.
    [JsonPropertyName("payment_collection_method")]
    public string? PaymentCollectionMethod { get; set; }
}

// POST /subscriptions/{id}/migrations/preview.json and /migrations.json
//   ->  { "migration": { product_handle | product_id, preserve_period, ... } }
public class MigrationRequestDto
{
    [JsonPropertyName("migration")]
    public MigrationBodyDto? Migration { get; set; }
}

public class MigrationBodyDto
{
    [JsonPropertyName("product_handle")]
    public string? ProductHandle { get; set; }

    [JsonPropertyName("product_id")]
    public int? ProductId { get; set; }

    // Accepted/ignored — the client sets preserve_period itself.
    [JsonPropertyName("preserve_period")]
    public bool? PreservePeriod { get; set; }
}

// DELETE /subscriptions/{id}.json  ->  { "subscription": { cancellation_message, ... } }
public class CancellationRequestDto
{
    [JsonPropertyName("subscription")]
    public CancellationBodyDto? Subscription { get; set; }
}

public class CancellationBodyDto
{
    [JsonPropertyName("cancellation_message")]
    public string? CancellationMessage { get; set; }

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; set; }
}

// POST /subscriptions/{id}/components/{component_id}/usages.json
//   ->  { "usage": { quantity, memo } }
public class CreateUsageRequestDto
{
    [JsonPropertyName("usage")]
    public UsageBodyDto? Usage { get; set; }
}

public class UsageBodyDto
{
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("memo")]
    public string? Memo { get; set; }
}
