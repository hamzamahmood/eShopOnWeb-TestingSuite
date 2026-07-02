using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

/// <summary>Mirrors Create-Customer.yaml - the `customer` object of the createCustomer request body.</summary>
public sealed class CreateCustomerModel
{
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("cc_emails")] public string? CcEmails { get; set; }
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    /// <summary>The unique per-app reference; forwarded as the idempotency key of EnsureCustomerAsync.</summary>
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("address_2")] public string? Address2 { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("zip")] public string? Zip { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("locale")] public string? Locale { get; set; }
    [JsonPropertyName("vat_number")] public string? VatNumber { get; set; }
    [JsonPropertyName("tax_exempt")] public bool? TaxExempt { get; set; }
    [JsonPropertyName("tax_exempt_reason")] public string? TaxExemptReason { get; set; }
    [JsonPropertyName("parent_id")] public int? ParentId { get; set; }
    [JsonPropertyName("salesforce_id")] public string? SalesforceId { get; set; }
}

/// <summary>
/// Mirrors Create-Customer-Request.yaml (POST /customers.json body). Shapes the "Ensure Customer"
/// endpoint after createCustomer, the richer of the two operations it composes (the lookup leg needs
/// only `reference`, which is a subset of these fields).
/// </summary>
public sealed class CreateCustomerRequest
{
    [JsonPropertyName("customer")] public CreateCustomerModel Customer { get; set; } = new();
}
