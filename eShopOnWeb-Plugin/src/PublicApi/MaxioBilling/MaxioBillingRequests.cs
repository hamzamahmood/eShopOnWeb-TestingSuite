using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

// =====================================================================================================
// Request DTOs for MaxioBillingController.
//
// Each DTO is shaped after the corresponding Maxio Advanced Billing API operation (snake_case field
// names, wrapper envelopes, and route/query/body placement match the spec). Only the fields the
// underlying IBillingClient method can actually forward are strongly typed + validated; every other
// documented Maxio field is declared as a JsonElement? so it is accepted for contract fidelity but
// silently ignored (the client hardcodes or drops it). Fields marked "// inert" below are never
// forwarded to the provider.
// =====================================================================================================

// -----------------------------------------------------------------------------------------------------
// FindOrCreateCustomer  (composite: readCustomerByReference + createCustomer) — shaped after createCustomer
// POST /customers.json   body: { "customer": { ... } }
// -----------------------------------------------------------------------------------------------------
public class CreateCustomerRequest
{
    [Required]
    [JsonPropertyName("customer")]
    public CreateCustomerBody Customer { get; set; } = new();
}

public class CreateCustomerBody
{
    /// <summary>Forwarded as the customer reference (lookup key for the find-or-create). Required by this passthrough.</summary>
    [Required]
    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    /// <summary>Forwarded to the client. Required by this passthrough.</summary>
    [Required]
    [EmailAddress]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Forwarded to the client (defaults to "eShopOnWeb" when blank).</summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    /// <summary>Forwarded to the client (defaults to "Customer" when blank).</summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    [JsonPropertyName("cc_emails")] public JsonElement? CcEmails { get; set; }
    [JsonPropertyName("organization")] public JsonElement? Organization { get; set; }
    [JsonPropertyName("address")] public JsonElement? Address { get; set; }
    [JsonPropertyName("address_2")] public JsonElement? Address2 { get; set; }
    [JsonPropertyName("city")] public JsonElement? City { get; set; }
    [JsonPropertyName("state")] public JsonElement? State { get; set; }
    [JsonPropertyName("zip")] public JsonElement? Zip { get; set; }
    [JsonPropertyName("country")] public JsonElement? Country { get; set; }
    [JsonPropertyName("phone")] public JsonElement? Phone { get; set; }
    [JsonPropertyName("locale")] public JsonElement? Locale { get; set; }
    [JsonPropertyName("vat_number")] public JsonElement? VatNumber { get; set; }
    [JsonPropertyName("tax_exempt")] public JsonElement? TaxExempt { get; set; }
    [JsonPropertyName("tax_exempt_reason")] public JsonElement? TaxExemptReason { get; set; }
    [JsonPropertyName("parent_id")] public JsonElement? ParentId { get; set; }
    [JsonPropertyName("salesforce_id")] public JsonElement? SalesforceId { get; set; }
}

// -----------------------------------------------------------------------------------------------------
// CreateSubscription
// POST /subscriptions.json   body: { "subscription": { ... } }
// -----------------------------------------------------------------------------------------------------
public class CreateSubscriptionRequest
{
    [Required]
    [JsonPropertyName("subscription")]
    public CreateSubscriptionBody Subscription { get; set; } = new();
}

public class CreateSubscriptionBody
{
    /// <summary>Forwarded to the client. Required by this passthrough (the client identifies the plan by handle only).</summary>
    [Required]
    [JsonPropertyName("product_handle")]
    public string ProductHandle { get; set; } = string.Empty;

    /// <summary>Forwarded to the client. Required by this passthrough (the client identifies the customer by id only).</summary>
    [Required]
    [JsonPropertyName("customer_id")]
    public long? CustomerId { get; set; }

    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    // NOTE: payment_collection_method is inert here — the client always forces "remittance" regardless.
    [JsonPropertyName("product_id")] public JsonElement? ProductId { get; set; }
    [JsonPropertyName("product_price_point_handle")] public JsonElement? ProductPricePointHandle { get; set; }
    [JsonPropertyName("product_price_point_id")] public JsonElement? ProductPricePointId { get; set; }
    [JsonPropertyName("custom_price")] public JsonElement? CustomPrice { get; set; }
    [JsonPropertyName("coupon_code")] public JsonElement? CouponCode { get; set; }
    [JsonPropertyName("coupon_codes")] public JsonElement? CouponCodes { get; set; }
    [JsonPropertyName("payment_collection_method")] public JsonElement? PaymentCollectionMethod { get; set; }
    [JsonPropertyName("receives_invoice_emails")] public JsonElement? ReceivesInvoiceEmails { get; set; }
    [JsonPropertyName("net_terms")] public JsonElement? NetTerms { get; set; }
    [JsonPropertyName("next_billing_at")] public JsonElement? NextBillingAt { get; set; }
    [JsonPropertyName("initial_billing_at")] public JsonElement? InitialBillingAt { get; set; }
    [JsonPropertyName("defer_signup")] public JsonElement? DeferSignup { get; set; }
    [JsonPropertyName("stored_credential_transaction_id")] public JsonElement? StoredCredentialTransactionId { get; set; }
    [JsonPropertyName("sales_rep_id")] public JsonElement? SalesRepId { get; set; }
    [JsonPropertyName("payment_profile_id")] public JsonElement? PaymentProfileId { get; set; }
    [JsonPropertyName("reference")] public JsonElement? Reference { get; set; }
    [JsonPropertyName("customer_attributes")] public JsonElement? CustomerAttributes { get; set; }
    [JsonPropertyName("payment_profile_attributes")] public JsonElement? PaymentProfileAttributes { get; set; }
    [JsonPropertyName("credit_card_attributes")] public JsonElement? CreditCardAttributes { get; set; }
    [JsonPropertyName("bank_account_attributes")] public JsonElement? BankAccountAttributes { get; set; }
    [JsonPropertyName("components")] public JsonElement? Components { get; set; }
    [JsonPropertyName("calendar_billing")] public JsonElement? CalendarBilling { get; set; }
    [JsonPropertyName("metafields")] public JsonElement? Metafields { get; set; }
    [JsonPropertyName("customer_reference")] public JsonElement? CustomerReference { get; set; }
    [JsonPropertyName("group")] public JsonElement? Group { get; set; }
    [JsonPropertyName("ref")] public JsonElement? Ref { get; set; }
    [JsonPropertyName("cancellation_message")] public JsonElement? CancellationMessage { get; set; }
    [JsonPropertyName("cancellation_method")] public JsonElement? CancellationMethod { get; set; }
    [JsonPropertyName("currency")] public JsonElement? Currency { get; set; }
    [JsonPropertyName("expires_at")] public JsonElement? ExpiresAt { get; set; }
    [JsonPropertyName("expiration_tracks_next_billing_change")] public JsonElement? ExpirationTracksNextBillingChange { get; set; }
    [JsonPropertyName("agreement_terms")] public JsonElement? AgreementTerms { get; set; }
    [JsonPropertyName("authorizer_first_name")] public JsonElement? AuthorizerFirstName { get; set; }
    [JsonPropertyName("authorizer_last_name")] public JsonElement? AuthorizerLastName { get; set; }
    [JsonPropertyName("calendar_billing_first_charge")] public JsonElement? CalendarBillingFirstCharge { get; set; }
    [JsonPropertyName("reason_code")] public JsonElement? ReasonCode { get; set; }
    [JsonPropertyName("product_change_delayed")] public JsonElement? ProductChangeDelayed { get; set; }
    [JsonPropertyName("offer_id")] public JsonElement? OfferId { get; set; }
    [JsonPropertyName("prepaid_configuration")] public JsonElement? PrepaidConfiguration { get; set; }
    [JsonPropertyName("previous_billing_at")] public JsonElement? PreviousBillingAt { get; set; }
    [JsonPropertyName("import_mrr")] public JsonElement? ImportMrr { get; set; }
    [JsonPropertyName("canceled_at")] public JsonElement? CanceledAt { get; set; }
    [JsonPropertyName("activated_at")] public JsonElement? ActivatedAt { get; set; }
    [JsonPropertyName("agreement_acceptance")] public JsonElement? AgreementAcceptance { get; set; }
    [JsonPropertyName("ach_agreement")] public JsonElement? AchAgreement { get; set; }
    [JsonPropertyName("dunning_communication_delay_enabled")] public JsonElement? DunningCommunicationDelayEnabled { get; set; }
    [JsonPropertyName("dunning_communication_delay_time_zone")] public JsonElement? DunningCommunicationDelayTimeZone { get; set; }
    [JsonPropertyName("skip_billing_manifest_taxes")] public JsonElement? SkipBillingManifestTaxes { get; set; }
}

// -----------------------------------------------------------------------------------------------------
// RecordUsage
// POST /subscriptions/{subscription_id}/components/{component_id}/usages.json   body: { "usage": { ... } }
// (component_id path segment is inert — the client always uses the configured metered component)
// -----------------------------------------------------------------------------------------------------
public class CreateUsageRequest
{
    [Required]
    [JsonPropertyName("usage")]
    public CreateUsageBody Usage { get; set; } = new();
}

public class CreateUsageBody
{
    /// <summary>Forwarded to the client. Required (must be &gt; 0).</summary>
    [Range(0.01, 1_000_000)]
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    /// <summary>Forwarded to the client.</summary>
    [JsonPropertyName("memo")]
    public string? Memo { get; set; }

    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    [JsonPropertyName("price_point_id")] public JsonElement? PricePointId { get; set; }
    [JsonPropertyName("billing_schedule")] public JsonElement? BillingSchedule { get; set; }
    [JsonPropertyName("custom_price")] public JsonElement? CustomPrice { get; set; }
}

// -----------------------------------------------------------------------------------------------------
// PreviewPlanChange / CommitPlanChange  (shaped after the migration operations; `timing` is a control
// field this passthrough adds — it is NOT a Maxio parameter — to select the client's underlying operation)
//   PreviewPlanChange -> POST /subscriptions/{subscription_id}/migrations/preview.json
//   CommitPlanChange  -> POST /subscriptions/{subscription_id}/migrations.json  (Immediate)
//                        or PUT /subscriptions/{subscription_id}.json           (AtRenewal)
// body: { "migration": { ... }, "timing": "Immediate" | "AtRenewal" }
// -----------------------------------------------------------------------------------------------------
public class MigrationRequest
{
    [Required]
    [JsonPropertyName("migration")]
    public MigrationBody Migration { get; set; } = new();

    /// <summary>Control field (not a Maxio parameter): "Immediate" (prorated migration) or "AtRenewal" (no proration). Required.</summary>
    [Required]
    [JsonPropertyName("timing")]
    public string Timing { get; set; } = string.Empty;
}

public class MigrationBody
{
    /// <summary>Forwarded to the client as the target plan. Required by this passthrough (identified by handle only).</summary>
    [Required]
    [JsonPropertyName("product_handle")]
    public string ProductHandle { get; set; } = string.Empty;

    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    // NOTE: preserve_period is inert — the client always sends preserve_period=true.
    [JsonPropertyName("product_id")] public JsonElement? ProductId { get; set; }
    [JsonPropertyName("product_price_point_id")] public JsonElement? ProductPricePointId { get; set; }
    [JsonPropertyName("product_price_point_handle")] public JsonElement? ProductPricePointHandle { get; set; }
    [JsonPropertyName("include_trial")] public JsonElement? IncludeTrial { get; set; }
    [JsonPropertyName("include_initial_charge")] public JsonElement? IncludeInitialCharge { get; set; }
    [JsonPropertyName("include_coupons")] public JsonElement? IncludeCoupons { get; set; }
    [JsonPropertyName("preserve_period")] public JsonElement? PreservePeriod { get; set; }
    [JsonPropertyName("proration")] public JsonElement? Proration { get; set; }
    /// <summary>Preview-only in the Maxio spec; inert here.</summary>
    [JsonPropertyName("proration_date")] public JsonElement? ProrationDate { get; set; }
}

// -----------------------------------------------------------------------------------------------------
// Cancel
// DELETE /subscriptions/{subscription_id}.json   body: { "subscription": { ... } }
// (`timing` is a control field this passthrough adds — the client uses it for cancel_at_end_of_period)
// -----------------------------------------------------------------------------------------------------
public class CancellationRequest
{
    [JsonPropertyName("subscription")]
    public CancellationBody Subscription { get; set; } = new();

    /// <summary>Control field (not in this Maxio operation's body): "Immediate" or "EndOfPeriod". Required.</summary>
    [Required]
    [JsonPropertyName("timing")]
    public string Timing { get; set; } = string.Empty;
}

public class CancellationBody
{
    /// <summary>Forwarded to the client as the cancellation reason.</summary>
    [JsonPropertyName("cancellation_message")]
    public string? CancellationMessage { get; set; }

    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    [JsonPropertyName("reason_code")] public JsonElement? ReasonCode { get; set; }
}

// -----------------------------------------------------------------------------------------------------
// Pause
// POST /subscriptions/{subscription_id}/hold.json   body: { "hold": { ... } }  (entire body inert)
// -----------------------------------------------------------------------------------------------------
public class PauseRequest
{
    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    [JsonPropertyName("hold")] public JsonElement? Hold { get; set; }
}

// -----------------------------------------------------------------------------------------------------
// Reactivate
// PUT /subscriptions/{subscription_id}/reactivate.json   body: flat (no wrapper) — entire body inert
// -----------------------------------------------------------------------------------------------------
public class ReactivateRequest
{
    // --- inert: accepted for Maxio-contract fidelity, not forwarded by this passthrough ---
    [JsonPropertyName("calendar_billing")] public JsonElement? CalendarBilling { get; set; }
    [JsonPropertyName("include_trial")] public JsonElement? IncludeTrial { get; set; }
    [JsonPropertyName("preserve_balance")] public JsonElement? PreserveBalance { get; set; }
    [JsonPropertyName("coupon_code")] public JsonElement? CouponCode { get; set; }
    [JsonPropertyName("use_credits_and_prepayments")] public JsonElement? UseCreditsAndPrepayments { get; set; }
    [JsonPropertyName("resume")] public JsonElement? Resume { get; set; }
}
