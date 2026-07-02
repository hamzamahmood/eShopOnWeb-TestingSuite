using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.MaxioBilling;

/// <summary>Mirrors Create-Subscription.yaml - the `subscription` object of the createSubscription body.</summary>
public sealed class CreateSubscriptionModel
{
    /// <summary>FORWARDED as the product to subscribe to.</summary>
    [JsonPropertyName("product_handle")] public string? ProductHandle { get; set; }
    [JsonPropertyName("product_id")] public int? ProductId { get; set; }
    [JsonPropertyName("product_price_point_handle")] public string? ProductPricePointHandle { get; set; }
    [JsonPropertyName("product_price_point_id")] public int? ProductPricePointId { get; set; }
    [JsonPropertyName("custom_price")] public SubscriptionCustomPriceModel? CustomPrice { get; set; }
    [JsonPropertyName("coupon_code")] public string? CouponCode { get; set; }
    [JsonPropertyName("coupon_codes")] public List<string>? CouponCodes { get; set; }
    /// <summary>Collection-Method enum: automatic | remittance | prepaid | invoice. NOT forwarded - the
    /// client derives this itself (remittance when no payment token, otherwise the provider default).</summary>
    [JsonPropertyName("payment_collection_method")] public string? PaymentCollectionMethod { get; set; }
    [JsonPropertyName("receives_invoice_emails")] public string? ReceivesInvoiceEmails { get; set; }
    [JsonPropertyName("net_terms")] public string? NetTerms { get; set; }
    /// <summary>FORWARDED as the existing provider customer id.</summary>
    [JsonPropertyName("customer_id")] public int? CustomerId { get; set; }
    [JsonPropertyName("next_billing_at")] public DateTimeOffset? NextBillingAt { get; set; }
    [JsonPropertyName("initial_billing_at")] public DateTimeOffset? InitialBillingAt { get; set; }
    [JsonPropertyName("defer_signup")] public bool? DeferSignup { get; set; }
    [JsonPropertyName("stored_credential_transaction_id")] public int? StoredCredentialTransactionId { get; set; }
    [JsonPropertyName("sales_rep_id")] public int? SalesRepId { get; set; }
    [JsonPropertyName("payment_profile_id")] public int? PaymentProfileId { get; set; }
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("customer_attributes")] public CustomerAttributesModel? CustomerAttributes { get; set; }
    /// <summary>Its `chargify_token` is FORWARDED as the payment token when present.</summary>
    [JsonPropertyName("payment_profile_attributes")] public PaymentProfileAttributesModel? PaymentProfileAttributes { get; set; }
    /// <summary>Interchangeable with payment_profile_attributes; its `chargify_token` is FORWARDED as a fallback.</summary>
    [JsonPropertyName("credit_card_attributes")] public PaymentProfileAttributesModel? CreditCardAttributes { get; set; }
    [JsonPropertyName("bank_account_attributes")] public BankAccountAttributesModel? BankAccountAttributes { get; set; }
    [JsonPropertyName("components")] public List<CreateSubscriptionComponentModel>? Components { get; set; }
    [JsonPropertyName("calendar_billing")] public CalendarBillingModel? CalendarBilling { get; set; }
    [JsonPropertyName("metafields")] public Dictionary<string, string>? Metafields { get; set; }
    [JsonPropertyName("customer_reference")] public string? CustomerReference { get; set; }
    [JsonPropertyName("group")] public GroupSettingsModel? Group { get; set; }
    [JsonPropertyName("ref")] public string? Ref { get; set; }
    [JsonPropertyName("cancellation_message")] public string? CancellationMessage { get; set; }
    [JsonPropertyName("cancellation_method")] public string? CancellationMethod { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("expiration_tracks_next_billing_change")] public string? ExpirationTracksNextBillingChange { get; set; }
    [JsonPropertyName("agreement_terms")] public string? AgreementTerms { get; set; }
    [JsonPropertyName("authorizer_first_name")] public string? AuthorizerFirstName { get; set; }
    [JsonPropertyName("authorizer_last_name")] public string? AuthorizerLastName { get; set; }
    [JsonPropertyName("calendar_billing_first_charge")] public string? CalendarBillingFirstCharge { get; set; }
    [JsonPropertyName("reason_code")] public string? ReasonCode { get; set; }
    [JsonPropertyName("product_change_delayed")] public bool? ProductChangeDelayed { get; set; }
    /// <summary>Spec union string|integer (offer id or `handle:` prefix).</summary>
    [JsonPropertyName("offer_id")] public object? OfferId { get; set; }
    [JsonPropertyName("prepaid_configuration")] public UpsertPrepaidConfigurationModel? PrepaidConfiguration { get; set; }
    [JsonPropertyName("previous_billing_at")] public DateTimeOffset? PreviousBillingAt { get; set; }
    [JsonPropertyName("import_mrr")] public bool? ImportMrr { get; set; }
    [JsonPropertyName("canceled_at")] public DateTimeOffset? CanceledAt { get; set; }
    [JsonPropertyName("activated_at")] public DateTimeOffset? ActivatedAt { get; set; }
    [JsonPropertyName("agreement_acceptance")] public AgreementAcceptanceModel? AgreementAcceptance { get; set; }
    [JsonPropertyName("ach_agreement")] public AchAgreementModel? AchAgreement { get; set; }
    [JsonPropertyName("dunning_communication_delay_enabled")] public bool? DunningCommunicationDelayEnabled { get; set; }
    [JsonPropertyName("dunning_communication_delay_time_zone")] public string? DunningCommunicationDelayTimeZone { get; set; }
    [JsonPropertyName("skip_billing_manifest_taxes")] public bool? SkipBillingManifestTaxes { get; set; }
}

/// <summary>Mirrors Create-Subscription-Request.yaml (POST /subscriptions.json body).</summary>
public sealed class CreateSubscriptionRequest
{
    [JsonPropertyName("subscription")] public CreateSubscriptionModel Subscription { get; set; } = new();
}

/// <summary>Mirrors Update-Subscription.yaml - the `subscription` object of the updateSubscription body.</summary>
public sealed class UpdateSubscriptionModel
{
    [JsonPropertyName("credit_card_attributes")] public CreditCardAttributesModel? CreditCardAttributes { get; set; }
    /// <summary>FORWARDED as the target product for the delayed plan change.</summary>
    [JsonPropertyName("product_handle")] public string? ProductHandle { get; set; }
    [JsonPropertyName("product_id")] public int? ProductId { get; set; }
    /// <summary>NOT forwarded - the SchedulePlanChangeAtRenewal endpoint always sends product_change_delayed=true.</summary>
    [JsonPropertyName("product_change_delayed")] public bool? ProductChangeDelayed { get; set; }
    [JsonPropertyName("next_product_id")] public string? NextProductId { get; set; }
    [JsonPropertyName("next_product_price_point_id")] public string? NextProductPricePointId { get; set; }
    /// <summary>Spec union integer(1-28)|string("end")|null.</summary>
    [JsonPropertyName("snap_day")] public object? SnapDay { get; set; }
    [JsonPropertyName("initial_billing_at")] public DateTimeOffset? InitialBillingAt { get; set; }
    [JsonPropertyName("defer_signup")] public bool? DeferSignup { get; set; }
    [JsonPropertyName("next_billing_at")] public DateTimeOffset? NextBillingAt { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("payment_collection_method")] public string? PaymentCollectionMethod { get; set; }
    [JsonPropertyName("receives_invoice_emails")] public bool? ReceivesInvoiceEmails { get; set; }
    /// <summary>Spec union string|integer.</summary>
    [JsonPropertyName("net_terms")] public object? NetTerms { get; set; }
    [JsonPropertyName("stored_credential_transaction_id")] public int? StoredCredentialTransactionId { get; set; }
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("custom_price")] public SubscriptionCustomPriceModel? CustomPrice { get; set; }
    [JsonPropertyName("components")] public List<UpdateSubscriptionComponentModel>? Components { get; set; }
    [JsonPropertyName("dunning_communication_delay_enabled")] public bool? DunningCommunicationDelayEnabled { get; set; }
    [JsonPropertyName("dunning_communication_delay_time_zone")] public string? DunningCommunicationDelayTimeZone { get; set; }
    [JsonPropertyName("product_price_point_id")] public int? ProductPricePointId { get; set; }
    [JsonPropertyName("product_price_point_handle")] public string? ProductPricePointHandle { get; set; }
}

/// <summary>Mirrors Update-Subscription-Request.yaml (PUT /subscriptions/{id}.json body).</summary>
public sealed class UpdateSubscriptionRequest
{
    [JsonPropertyName("subscription")] public UpdateSubscriptionModel Subscription { get; set; } = new();
}

/// <summary>Mirrors Cancellation-Options.yaml - the `subscription` object of the cancellation body.</summary>
public sealed class CancellationOptionsModel
{
    /// <summary>FORWARDED as the cancellation reason.</summary>
    [JsonPropertyName("cancellation_message")] public string? CancellationMessage { get; set; }
    /// <summary>NOT forwarded - the client accepts only a single free-text reason.</summary>
    [JsonPropertyName("reason_code")] public string? ReasonCode { get; set; }
}

/// <summary>Mirrors Cancellation-Request.yaml (body of cancelSubscription and initiateDelayedCancellation).</summary>
public sealed class CancellationRequest
{
    [JsonPropertyName("subscription")] public CancellationOptionsModel Subscription { get; set; } = new();
}

/// <summary>Mirrors Auto-Resume.yaml - the `hold` object of the pauseSubscription body.</summary>
public sealed class AutoResumeModel
{
    [JsonPropertyName("automatically_resume_at")] public DateTimeOffset? AutomaticallyResumeAt { get; set; }
}

/// <summary>Mirrors Pause-Request (POST /subscriptions/{id}/hold.json body).</summary>
public sealed class PauseRequest
{
    /// <summary>NOT forwarded - the client always issues a plain, indefinite hold.</summary>
    [JsonPropertyName("hold")] public AutoResumeModel? Hold { get; set; }
}

/// <summary>Mirrors Reactivate-Subscription-Request.yaml (PUT /subscriptions/{id}/reactivate.json body).</summary>
public sealed class ReactivateSubscriptionRequest
{
    [JsonPropertyName("calendar_billing")] public ReactivationBillingModel? CalendarBilling { get; set; }
    [JsonPropertyName("include_trial")] public bool? IncludeTrial { get; set; }
    [JsonPropertyName("preserve_balance")] public bool? PreserveBalance { get; set; }
    [JsonPropertyName("coupon_code")] public string? CouponCode { get; set; }
    [JsonPropertyName("use_credits_and_prepayments")] public bool? UseCreditsAndPrepayments { get; set; }
    /// <summary>Spec oneOf boolean|Resume-Options.</summary>
    [JsonPropertyName("resume")] public object? Resume { get; set; }
}

/// <summary>Mirrors Reactivation-Billing.yaml (calendar-billing subscriptions only).</summary>
public sealed class ReactivationBillingModel
{
    /// <summary>Reactivation-Charge enum: prorated | immediate | delayed.</summary>
    [JsonPropertyName("reactivation_charge")] public string? ReactivationCharge { get; set; }
}

/// <summary>Mirrors Subscription-Product-Migration.yaml - the `migration` object of migrateSubscriptionProduct.</summary>
public sealed class SubscriptionMigrationModel
{
    [JsonPropertyName("product_id")] public int? ProductId { get; set; }
    [JsonPropertyName("product_price_point_id")] public int? ProductPricePointId { get; set; }
    [JsonPropertyName("include_trial")] public bool? IncludeTrial { get; set; }
    [JsonPropertyName("include_initial_charge")] public bool? IncludeInitialCharge { get; set; }
    [JsonPropertyName("include_coupons")] public bool? IncludeCoupons { get; set; }
    /// <summary>NOT forwarded - the client always migrates with preserve_period=true (immediate prorated change).</summary>
    [JsonPropertyName("preserve_period")] public bool? PreservePeriod { get; set; }
    /// <summary>FORWARDED as the target product handle.</summary>
    [JsonPropertyName("product_handle")] public string? ProductHandle { get; set; }
    [JsonPropertyName("product_price_point_handle")] public string? ProductPricePointHandle { get; set; }
    [JsonPropertyName("proration")] public ProrationModel? Proration { get; set; }
}

/// <summary>Mirrors Subscription-Product-Migration-Request.yaml (POST /subscriptions/{id}/migrations.json body).</summary>
public sealed class MigrationRequest
{
    [JsonPropertyName("migration")] public SubscriptionMigrationModel Migration { get; set; } = new();
}

/// <summary>Mirrors Subscription-Migration-Preview-Options.yaml - migration plus the preview-only proration_date.</summary>
public sealed class SubscriptionMigrationPreviewModel
{
    [JsonPropertyName("product_id")] public int? ProductId { get; set; }
    [JsonPropertyName("product_price_point_id")] public int? ProductPricePointId { get; set; }
    [JsonPropertyName("include_trial")] public bool? IncludeTrial { get; set; }
    [JsonPropertyName("include_initial_charge")] public bool? IncludeInitialCharge { get; set; }
    [JsonPropertyName("include_coupons")] public bool? IncludeCoupons { get; set; }
    /// <summary>NOT forwarded - the client always previews with preserve_period=true.</summary>
    [JsonPropertyName("preserve_period")] public bool? PreservePeriod { get; set; }
    /// <summary>FORWARDED as the target product handle.</summary>
    [JsonPropertyName("product_handle")] public string? ProductHandle { get; set; }
    [JsonPropertyName("product_price_point_handle")] public string? ProductPricePointHandle { get; set; }
    [JsonPropertyName("proration")] public ProrationModel? Proration { get; set; }
    [JsonPropertyName("proration_date")] public DateTimeOffset? ProrationDate { get; set; }
}

/// <summary>Mirrors Subscription-Migration-Preview-Request.yaml (POST /subscriptions/{id}/migrations/preview.json body).</summary>
public sealed class MigrationPreviewRequest
{
    [JsonPropertyName("migration")] public SubscriptionMigrationPreviewModel Migration { get; set; } = new();
}
