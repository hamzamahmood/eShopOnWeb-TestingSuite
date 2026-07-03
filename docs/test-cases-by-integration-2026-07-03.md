# Test Cases by Integration — Pass/Fail

**Date:** 2026-07-03
**Suite:** `MaxioPassthroughApiTests` (38 cases) run against the mock Maxio server.

| Integration | Passed | Failed | Total |
|---|---|---|---|
| Plugin (APIMatic SDK) | 37 | 1 | 38 |
| Direct (raw HTTP) | 29 | 9 | 38 |

---

| Testcase purpose | Testcase name | Plugin | Direct |
|---|---|---|---|
| List available plans for the configured product family | `ListPlansTests.ListPlans_returns_the_configured_familys_plans_with_common_fields` | ✅ Pass | ✅ Pass |
| Find-or-create a new customer, idempotent on repeat calls | `FindOrCreateCustomerTests.Fresh_reference_creates_a_customer_and_is_idempotent_on_repeat_calls` | ✅ Pass | ✅ Pass |
| Reject a blank email before calling the billing provider | `FindOrCreateCustomerTests.Blank_email_is_rejected_before_reaching_the_billing_provider` | ✅ Pass | ✅ Pass |
| Look up an existing customer by reference and get their ID | `CustomerLookupTests.Known_reference_returns_the_customer_id` | ✅ Pass | ❌ Fail |
| Looking up a non-existent customer returns 404 | `CustomerLookupTests.Unknown_reference_yields_404_not_found` | ✅ Pass | ✅ Pass |
| List a known customer's subscriptions | `SubscriptionTests.Known_customer_returns_the_subscriptions_array_with_common_fields` | ✅ Pass | ✅ Pass |
| Listing subscriptions for an unknown customer returns an error | `SubscriptionTests.Unknown_customer_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Read a known subscription's details | `ReadSubscriptionTests.Known_subscription_returns_its_common_fields` | ✅ Pass | ✅ Pass |
| Reading an unknown subscription returns an error | `ReadSubscriptionTests.Unknown_subscription_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Create a subscription for a known customer and plan | `CreateSubscriptionTests.Known_customer_and_product_creates_a_subscription` | ✅ Pass | ❌ Fail |
| Creating a subscription for an unknown customer returns an error | `CreateSubscriptionTests.Unknown_customer_id_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Creating a subscription with an unknown plan returns an error | `CreateSubscriptionTests.Unknown_product_handle_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Pause (hold) an active subscription | `PauseSubscriptionTests.Active_subscription_is_paused` | ✅ Pass | ✅ Pass |
| Pausing an already-paused subscription returns an error | `PauseSubscriptionTests.Already_on_hold_subscription_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Resume a paused (on-hold) subscription | `ResumeSubscriptionTests.On_hold_subscription_is_resumed` | ✅ Pass | ✅ Pass |
| Cannot resume a subscription that is already active | `ResumeSubscriptionTests.Active_subscription_cannot_be_resumed` | ✅ Pass | ✅ Pass |
| Reactivate a canceled subscription | `ReactivateSubscriptionTests.Canceled_subscription_is_reactivated` | ✅ Pass | ✅ Pass |
| Cannot reactivate a subscription that is already active | `ReactivateSubscriptionTests.Active_subscription_cannot_be_reactivated` | ✅ Pass | ✅ Pass |
| Change an active subscription's plan to another known plan | `CommitPlanChangeTests.Active_subscription_migrates_to_a_different_known_product` | ✅ Pass | ✅ Pass |
| Cannot change the plan of a canceled subscription | `CommitPlanChangeTests.Canceled_subscription_cannot_be_migrated` | ✅ Pass | ✅ Pass |
| Changing to an unknown plan returns an error | `CommitPlanChangeTests.Unknown_product_handle_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Cancel an active subscription immediately | `CancelSubscriptionTests.Active_subscription_is_canceled` | ✅ Pass | ✅ Pass |
| Canceling an already-canceled subscription returns an error | `CancelSubscriptionTests.Already_canceled_subscription_yields_an_error_status` | ✅ Pass | ✅ Pass |
| Record metered usage on a known subscription | `RecordUsageTests.Known_subscription_records_usage_with_the_given_quantity_and_memo` | ✅ Pass | ✅ Pass |
| Recording usage on an unknown subscription returns an error | `RecordUsageTests.Unknown_subscription_yields_an_error_status` | ❌ Fail | ❌ Fail |
| A rate-limited (429) customer lookup automatically recovers | `RetrySafetyTests.Rate_limited_lookup_recovers` | ✅ Pass | ✅ Pass |
| A transient 503 during customer lookup automatically recovers | `RetrySafetyTests.Transient_503_lookup_recovers` | ✅ Pass | ✅ Pass |
| Error responses never leak internals — unknown product on create | `ErrorHygieneTests.Error_responses_never_leak_internal_details(scenario: "create-unknown-product")` | ✅ Pass | ✅ Pass |
| Error responses never leak internals — unknown product on migrate | `ErrorHygieneTests.Error_responses_never_leak_internal_details(scenario: "migrate-unknown-product")` | ✅ Pass | ✅ Pass |
| Error responses never leak internals — cancel already-canceled | `ErrorHygieneTests.Error_responses_never_leak_internal_details(scenario: "cancel-already-canceled")` | ✅ Pass | ✅ Pass |
| Error responses never leak internals — pause on-hold subscription | `ErrorHygieneTests.Error_responses_never_leak_internal_details(scenario: "pause-on-hold-subscription")` | ✅ Pass | ✅ Pass |
| Error responses never leak internals — read unknown subscription | `ErrorHygieneTests.Error_responses_never_leak_internal_details(scenario: "read-unknown-subscription")` | ✅ Pass | ✅ Pass |
| Missing subscription returns a precise 404 (Plugin advantage) | `PluginAdvantageTests.Missing_subscription_returns_404_not_found` | ✅ Pass | ❌ Fail |
| Find-or-create recovers from a concurrent-create race (Plugin advantage) | `PluginAdvantageTests.Find_or_create_customer_recovers_from_a_concurrent_create_race` | ✅ Pass | ❌ Fail |
| Payment failure surfaces a typed error — card required (Plugin advantage) | `PluginAdvantageTests.Payment_failure_surfaces_a_typed_payment_verification_error(productHandle: "card-required")` | ✅ Pass | ❌ Fail |
| Payment failure surfaces a typed error — 3-D Secure required (Plugin advantage) | `PluginAdvantageTests.Payment_failure_surfaces_a_typed_payment_verification_error(productHandle: "threeds-required")` | ✅ Pass | ❌ Fail |
| Payment failure surfaces a typed error — card declined (Plugin advantage) | `PluginAdvantageTests.Payment_failure_surfaces_a_typed_payment_verification_error(productHandle: "card-declined")` | ✅ Pass | ❌ Fail |
| Unknown provider subscription state maps to a safe default (Plugin advantage) | `StateDriftTests.Unknown_provider_state_maps_to_a_safe_default` | ✅ Pass | ❌ Fail |
