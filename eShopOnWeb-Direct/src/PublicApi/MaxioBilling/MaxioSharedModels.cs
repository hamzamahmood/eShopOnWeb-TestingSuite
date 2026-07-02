using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

// Request DTOs for the Maxio Advanced Billing microservice controller.
//
// Every type here is shaped to match a named schema in openAPI/components/schemas/*.yaml so the
// controller's external contract mirrors the real Maxio operation, independent of the (narrower)
// shape MaxioBillingClient happens to accept today (see the controller for the DTO->client mapping).
//
// Conventions:
//   * JSON field names are the wire snake_case names, pinned with [JsonPropertyName] because the
//     app's global System.Text.Json policy is camelCase - without these attributes snake_case input
//     would not bind.
//   * A property typed `object?` mirrors a spec field declared as a union of primitives
//     (e.g. `type: [integer, string]`) or a oneOf; it accepts either JSON shape verbatim.
//   * Spec enums are modelled as `string?` (matching the existing Infrastructure DTO style); the
//     allowed values are noted in the XML doc where relevant.
//   * Fields the client cannot forward are still present (the chosen "full spec shape" policy) and
//     are flagged NOT FORWARDED in the controller's XML docs.

/// <summary>Mirrors Customer-Attributes.yaml - an inline customer used by createSubscription.</summary>
public sealed class CustomerAttributesModel
{
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("cc_emails")] public string? CcEmails { get; set; }
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("address_2")] public string? Address2 { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("zip")] public string? Zip { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("verified")] public bool? Verified { get; set; }
    [JsonPropertyName("tax_exempt")] public bool? TaxExempt { get; set; }
    [JsonPropertyName("vat_number")] public string? VatNumber { get; set; }
    [JsonPropertyName("metafields")] public Dictionary<string, string>? Metafields { get; set; }
    [JsonPropertyName("parent_id")] public int? ParentId { get; set; }
    [JsonPropertyName("salesforce_id")] public string? SalesforceId { get; set; }
    [JsonPropertyName("default_auto_renewal_profile_id")] public int? DefaultAutoRenewalProfileId { get; set; }
}

/// <summary>Mirrors Payment-Profile-Attributes.yaml (alias of credit_card_attributes on createSubscription).</summary>
public sealed class PaymentProfileAttributesModel
{
    /// <summary>chargify.js token; when present, the controller forwards this as the subscription's payment token.</summary>
    [JsonPropertyName("chargify_token")] public string? ChargifyToken { get; set; }
    [JsonPropertyName("id")] public int? Id { get; set; }
    /// <summary>Payment-Type enum, e.g. "credit_card", "bank_account", "paypal_account".</summary>
    [JsonPropertyName("payment_type")] public string? PaymentType { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("masked_card_number")] public string? MaskedCardNumber { get; set; }
    [JsonPropertyName("full_number")] public string? FullNumber { get; set; }
    /// <summary>Card-Type enum (import only).</summary>
    [JsonPropertyName("card_type")] public string? CardType { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("expiration_month")] public object? ExpirationMonth { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("expiration_year")] public object? ExpirationYear { get; set; }
    [JsonPropertyName("billing_address")] public string? BillingAddress { get; set; }
    [JsonPropertyName("billing_address_2")] public string? BillingAddress2 { get; set; }
    [JsonPropertyName("billing_city")] public string? BillingCity { get; set; }
    [JsonPropertyName("billing_state")] public string? BillingState { get; set; }
    [JsonPropertyName("billing_country")] public string? BillingCountry { get; set; }
    [JsonPropertyName("billing_zip")] public string? BillingZip { get; set; }
    /// <summary>All-Vaults enum (import only).</summary>
    [JsonPropertyName("current_vault")] public string? CurrentVault { get; set; }
    [JsonPropertyName("vault_token")] public string? VaultToken { get; set; }
    [JsonPropertyName("customer_vault_token")] public string? CustomerVaultToken { get; set; }
    [JsonPropertyName("customer_id")] public int? CustomerId { get; set; }
    [JsonPropertyName("paypal_email")] public string? PaypalEmail { get; set; }
    [JsonPropertyName("payment_method_nonce")] public string? PaymentMethodNonce { get; set; }
    [JsonPropertyName("gateway_handle")] public string? GatewayHandle { get; set; }
    [JsonPropertyName("cvv")] public string? Cvv { get; set; }
    [JsonPropertyName("last_four")] public string? LastFour { get; set; }
}

/// <summary>Mirrors Credit-Card-Attributes.yaml (the trimmed credit_card_attributes on updateSubscription).</summary>
public sealed class CreditCardAttributesModel
{
    [JsonPropertyName("full_number")] public string? FullNumber { get; set; }
    [JsonPropertyName("expiration_month")] public string? ExpirationMonth { get; set; }
    [JsonPropertyName("expiration_year")] public string? ExpirationYear { get; set; }
}

/// <summary>Mirrors Bank-Account-Attributes.yaml.</summary>
public sealed class BankAccountAttributesModel
{
    [JsonPropertyName("chargify_token")] public string? ChargifyToken { get; set; }
    [JsonPropertyName("bank_name")] public string? BankName { get; set; }
    [JsonPropertyName("bank_routing_number")] public string? BankRoutingNumber { get; set; }
    [JsonPropertyName("bank_account_number")] public string? BankAccountNumber { get; set; }
    /// <summary>Bank-Account-Type enum, e.g. "checking", "savings".</summary>
    [JsonPropertyName("bank_account_type")] public string? BankAccountType { get; set; }
    [JsonPropertyName("bank_branch_code")] public string? BankBranchCode { get; set; }
    [JsonPropertyName("bank_iban")] public string? BankIban { get; set; }
    /// <summary>Bank-Account-Holder-Type enum, e.g. "personal", "business".</summary>
    [JsonPropertyName("bank_account_holder_type")] public string? BankAccountHolderType { get; set; }
    /// <summary>Payment-Type enum.</summary>
    [JsonPropertyName("payment_type")] public string? PaymentType { get; set; }
    /// <summary>Bank-Account-Vault enum (import only).</summary>
    [JsonPropertyName("current_vault")] public string? CurrentVault { get; set; }
    [JsonPropertyName("vault_token")] public string? VaultToken { get; set; }
    [JsonPropertyName("customer_vault_token")] public string? CustomerVaultToken { get; set; }
}

/// <summary>Mirrors Price.yaml - one price bracket inside a custom price point.</summary>
public sealed class PriceModel
{
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("starting_quantity")] public object? StartingQuantity { get; set; }
    /// <summary>Spec union integer|string|null.</summary>
    [JsonPropertyName("ending_quantity")] public object? EndingQuantity { get; set; }
    /// <summary>Spec union number|string.</summary>
    [JsonPropertyName("unit_price")] public object? UnitPrice { get; set; }
}

/// <summary>Mirrors Component-Custom-Price.yaml - subscription-unique component pricing.</summary>
public sealed class ComponentCustomPriceModel
{
    [JsonPropertyName("tax_included")] public bool? TaxIncluded { get; set; }
    /// <summary>Pricing-Scheme enum: stairstep | volume | per_unit | tiered.</summary>
    [JsonPropertyName("pricing_scheme")] public string? PricingScheme { get; set; }
    [JsonPropertyName("interval")] public int? Interval { get; set; }
    /// <summary>Interval-Unit enum: day | month (nullable).</summary>
    [JsonPropertyName("interval_unit")] public string? IntervalUnit { get; set; }
    [JsonPropertyName("prices")] public List<PriceModel>? Prices { get; set; }
    [JsonPropertyName("renew_prepaid_allocation")] public bool? RenewPrepaidAllocation { get; set; }
    [JsonPropertyName("rollover_prepaid_remainder")] public bool? RolloverPrepaidRemainder { get; set; }
    [JsonPropertyName("expiration_interval")] public int? ExpirationInterval { get; set; }
    /// <summary>Expiration-Interval-Unit enum: day | month | never (nullable).</summary>
    [JsonPropertyName("expiration_interval_unit")] public string? ExpirationIntervalUnit { get; set; }
}

/// <summary>Mirrors Billing-Schedule.yaml (Multifrequency sites only).</summary>
public sealed class BillingScheduleModel
{
    /// <summary>ISO8601 date (format: date), e.g. "2024-01-01".</summary>
    [JsonPropertyName("initial_billing_at")] public string? InitialBillingAt { get; set; }
}

/// <summary>Mirrors Subscription-Custom-Price.yaml.</summary>
public sealed class SubscriptionCustomPriceModel
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("handle")] public string? Handle { get; set; }
    /// <summary>Spec union integer|string (int64).</summary>
    [JsonPropertyName("price_in_cents")] public object? PriceInCents { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("interval")] public object? Interval { get; set; }
    /// <summary>Interval-Unit enum (nullable): day | month.</summary>
    [JsonPropertyName("interval_unit")] public string? IntervalUnit { get; set; }
    /// <summary>Spec union integer|string (int64).</summary>
    [JsonPropertyName("trial_price_in_cents")] public object? TrialPriceInCents { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("trial_interval")] public object? TrialInterval { get; set; }
    /// <summary>Interval-Unit enum.</summary>
    [JsonPropertyName("trial_interval_unit")] public string? TrialIntervalUnit { get; set; }
    /// <summary>Trial-Type enum (nullable): no_obligation | payment_expected.</summary>
    [JsonPropertyName("trial_type")] public string? TrialType { get; set; }
    /// <summary>Spec union integer|string (int64).</summary>
    [JsonPropertyName("initial_charge_in_cents")] public object? InitialChargeInCents { get; set; }
    [JsonPropertyName("initial_charge_after_trial")] public bool? InitialChargeAfterTrial { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("expiration_interval")] public object? ExpirationInterval { get; set; }
    /// <summary>Expiration-Interval-Unit enum (nullable).</summary>
    [JsonPropertyName("expiration_interval_unit")] public string? ExpirationIntervalUnit { get; set; }
    [JsonPropertyName("tax_included")] public bool? TaxIncluded { get; set; }
}

/// <summary>Mirrors Create-Subscription-Component.yaml.</summary>
public sealed class CreateSubscriptionComponentModel
{
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("component_id")] public object? ComponentId { get; set; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
    [JsonPropertyName("unit_balance")] public int? UnitBalance { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("allocated_quantity")] public object? AllocatedQuantity { get; set; }
    [JsonPropertyName("quantity")] public int? Quantity { get; set; }
    /// <summary>Spec union integer|string.</summary>
    [JsonPropertyName("price_point_id")] public object? PricePointId { get; set; }
    [JsonPropertyName("custom_price")] public ComponentCustomPriceModel? CustomPrice { get; set; }
}

/// <summary>Mirrors Update-Subscription-Component.yaml.</summary>
public sealed class UpdateSubscriptionComponentModel
{
    [JsonPropertyName("component_id")] public int? ComponentId { get; set; }
    [JsonPropertyName("custom_price")] public ComponentCustomPriceModel? CustomPrice { get; set; }
}

/// <summary>Mirrors Calendar-Billing.yaml.</summary>
public sealed class CalendarBillingModel
{
    /// <summary>Spec union integer(1-28)|string("end")|null.</summary>
    [JsonPropertyName("snap_day")] public object? SnapDay { get; set; }
    /// <summary>First-Charge-Type enum: prorated | immediate | delayed.</summary>
    [JsonPropertyName("calendar_billing_first_charge")] public string? CalendarBillingFirstCharge { get; set; }
}

/// <summary>Mirrors Group-Target.yaml.</summary>
public sealed class GroupTargetModel
{
    /// <summary>Group-Target-Type enum: self | parent | eldest | customer | subscription.</summary>
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("id")] public int? Id { get; set; }
}

/// <summary>Mirrors Group-Billing.yaml.</summary>
public sealed class GroupBillingModel
{
    [JsonPropertyName("accrue")] public bool? Accrue { get; set; }
    [JsonPropertyName("align_date")] public bool? AlignDate { get; set; }
    [JsonPropertyName("prorate")] public bool? Prorate { get; set; }
}

/// <summary>Mirrors Group-Settings.yaml.</summary>
public sealed class GroupSettingsModel
{
    [JsonPropertyName("target")] public GroupTargetModel? Target { get; set; }
    [JsonPropertyName("billing")] public GroupBillingModel? Billing { get; set; }
}

/// <summary>Mirrors Agreement-Acceptance.yaml.</summary>
public sealed class AgreementAcceptanceModel
{
    [JsonPropertyName("ip_address")] public string? IpAddress { get; set; }
    [JsonPropertyName("terms_url")] public string? TermsUrl { get; set; }
    [JsonPropertyName("privacy_policy_url")] public string? PrivacyPolicyUrl { get; set; }
    [JsonPropertyName("return_refund_policy_url")] public string? ReturnRefundPolicyUrl { get; set; }
    [JsonPropertyName("delivery_policy_url")] public string? DeliveryPolicyUrl { get; set; }
    [JsonPropertyName("secure_checkout_policy_url")] public string? SecureCheckoutPolicyUrl { get; set; }
}

/// <summary>Mirrors ACH-Agreement.yaml.</summary>
public sealed class AchAgreementModel
{
    [JsonPropertyName("agreement_terms")] public string? AgreementTerms { get; set; }
    [JsonPropertyName("authorizer_first_name")] public string? AuthorizerFirstName { get; set; }
    [JsonPropertyName("authorizer_last_name")] public string? AuthorizerLastName { get; set; }
    [JsonPropertyName("ip_address")] public string? IpAddress { get; set; }
}

/// <summary>Mirrors Upsert-Prepaid-Configuration.yaml.</summary>
public sealed class UpsertPrepaidConfigurationModel
{
    [JsonPropertyName("initial_funding_amount_in_cents")] public long? InitialFundingAmountInCents { get; set; }
    [JsonPropertyName("replenish_to_amount_in_cents")] public long? ReplenishToAmountInCents { get; set; }
    [JsonPropertyName("auto_replenish")] public bool? AutoReplenish { get; set; }
    [JsonPropertyName("replenish_threshold_amount_in_cents")] public long? ReplenishThresholdAmountInCents { get; set; }
}

/// <summary>Mirrors Proration.yaml (nested inside a migration).</summary>
public sealed class ProrationModel
{
    [JsonPropertyName("preserve_period")] public bool? PreservePeriod { get; set; }
}
