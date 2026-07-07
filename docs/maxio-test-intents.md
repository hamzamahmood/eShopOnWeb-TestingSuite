# MaxioPassthroughApiTests — test intents

## endpoint

| Test | Intent |
|---|---|
| `ListPlans_returns_the_configured_familys_plans_with_common_fields` | List the configured product family's plans with their common fields |
| `Known_customer_returns_the_subscriptions_array_with_common_fields` | List a known customer's subscriptions with their common fields |
| `Unknown_customer_yields_an_error_status` *(SubscriptionTests)* | List subscriptions for an unknown customer |
| `Known_subscription_returns_its_common_fields` | Read a known subscription's common fields |
| `Unknown_subscription_yields_an_error_status` *(ReadSubscriptionTests)* | Read an unknown subscription |
| `Known_customer_and_product_creates_a_subscription` | Create a subscription for a known customer and product |
| `Unknown_product_handle_yields_an_error_status` *(CreateSubscriptionTests)* | Create a subscription with an unknown product handle |
| `Unknown_customer_id_yields_an_error_status` | Create a subscription for an unknown customer id |
| `Active_subscription_is_paused` | Pause an active subscription |
| `Already_on_hold_subscription_yields_an_error_status` | Pause a subscription that is already on hold |
| `On_hold_subscription_is_resumed` | Resume an on-hold subscription |
| `Active_subscription_cannot_be_resumed` | Resume a subscription that is already active |
| `Canceled_subscription_is_reactivated` | Reactivate a canceled subscription |
| `Active_subscription_cannot_be_reactivated` | Reactivate a subscription that is already active |
| `Active_subscription_migrates_to_a_different_known_product` | Migrate an active subscription to a different known product |
| `Unknown_product_handle_yields_an_error_status` *(CommitPlanChangeTests)* | Migrate a subscription to an unknown product handle |
| `Canceled_subscription_cannot_be_migrated` | Migrate a canceled subscription |
| `Active_subscription_is_canceled` | Cancel an active subscription immediately |
| `Already_canceled_subscription_yields_an_error_status` | Cancel a subscription that is already canceled |
| `Known_subscription_records_usage_with_the_given_quantity_and_memo` | Record usage with a given quantity and memo on a known subscription |
| `Unknown_subscription_yields_an_error_status` *(RecordUsageTests)* | Record usage on an unknown subscription |
| `Fresh_reference_creates_a_customer_and_is_idempotent_on_repeat_calls` | Create a customer for a fresh reference, then repeat the call idempotently |
| `Blank_email_is_rejected_before_reaching_the_billing_provider` | Reject a blank email before reaching the billing provider |
| `Known_reference_returns_the_customer_id` | Look up a customer by a known reference (Plugin-only endpoint) |
| `Unknown_reference_yields_404_not_found` | Look up a customer by an unknown reference (Plugin-only endpoint) |
| `Unknown_provider_state_maps_to_a_safe_default` | Read a subscription whose provider state is unrecognized (safe-default mapping) |
| `Missing_subscription_returns_404_not_found` | Read a missing subscription (REST-correct 404 vs Direct's 422) |
| `Find_or_create_customer_recovers_from_a_concurrent_create_race` | Recover find-or-create from a concurrent create race |
| `Payment_failure_surfaces_a_typed_payment_verification_error` *(Theory)* | Create a subscription with payment-failure handle '{productHandle}' (typed payment-verification error) |
| `Error_responses_never_leak_internal_details` *(Theory)* | Error hygiene: {scenario} response never leaks internal details |
| `Rate_limited_lookup_recovers` | Recover from a 429 rate limit on the find-or-create lookup |
| `Transient_503_lookup_recovers` | Recover from a transient 503 on the find-or-create lookup |
| `Recovers_from_intermittent_connection_breaks_across_many_calls` | Recover from a connection break on find-or-create call {i}/{CallCount} |
