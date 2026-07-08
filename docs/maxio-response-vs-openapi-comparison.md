# Integration response vs. Maxio OpenAPI response — field-by-field comparison

**What this compares:** the response bodies the running `api/maxio` controller (on `http://localhost:5000`)
returns, against the response schemas defined by the Maxio Advanced Billing OpenAPI spec in `openAPI/`
(`components/schemas/*.yaml`).

**How it was produced:** live `curl` against each endpoint of the running integration (ground truth for the
"Integration returns" column) mapped against the referenced Maxio response schema (ground truth for the
"Maxio OpenAPI" column). The `MaxioPassthroughApiTests` suite asserts a subset of these fields; where it does,
it's noted.

> The integration under test does **not** return Maxio's raw wire body. `MaxioBillingController` returns
> **flattened, provider-agnostic DTOs**. So *every* endpoint diverges from the spec in the same four structural
> ways, plus per-endpoint specifics. The four cross-cutting differences are called out once here and not
> repeated in every table.

## The four cross-cutting differences (apply to all 11 endpoints)

| # | Difference | Maxio OpenAPI | Integration |
|---|---|---|---|
| 1 | **Envelope** | Every resource is wrapped in a named key: `{"subscription": {…}}`, `{"product": {…}}`, `{"customer": {…}}`, `{"usage": {…}}`. Lists are arrays of wrapped objects (`[{"subscription": {…}}, …]`). | Bare object or bare array — **no envelope key**. |
| 2 | **Casing** | `snake_case` on every property (`price_in_cents`, `current_period_ends_at`). | `camelCase` on every property (`priceInCents`, `currentPeriodEndsAt`). |
| 3 | **Coverage** | Full resource — dozens of fields per schema. | **Curated subset** — only the handful the feature needs; everything else dropped. |
| 4 | **Derived / renamed fields** | — | Adds fields with **no Maxio equivalent** (dollar `price`, `nextBillingDate`, `periodToDateTotal`) and **hoists** nested-object fields to the top level (`productHandle`, `customerId`, …). |

Beyond these, the value-level and type-level divergences below are the "interesting" ones.

---

## 1. List plans — `GET product-families/{id}/products`
Maxio op: `GET /product_families/{id}/products.json` → **array of `Product-Response`** (`[{"product": Product}, …]`).
Integration returns a **bare array of flattened plan objects**.

| Integration field | Value seen | Maxio `Product` source | Note |
|---|---|---|---|
| `id` | `3858146` | `product.id` | same name |
| `productId` | `3858146` | — | **duplicate of `id`**; no such Maxio field |
| `handle` | `gold` | `product.handle` | ✓ (asserted) |
| `name` | `Gold Plan` | `product.name` | ✓ (asserted) |
| `price` | `10` | — | **derived dollars** = `price_in_cents / 100`; Maxio has **no `price` field** |
| `priceInCents` | `1000` | `product.price_in_cents` | rename only |
| `interval` | `1` | `product.interval` | rename only |
| `intervalUnit` | `month` | `product.interval_unit` | rename only |
| `description` | `This is our gold plan.` | `product.description` | ✓ |
| `productFamilyId` | `527890` | `product.product_family.id` | **flattened** from nested `product_family` object |
| `taxable` | `false` | `product.taxable` | ✓ |
| `requireCreditCard` | `true` | `product.require_credit_card` | rename only |
| `productPricePointId` | `1512822` | `product.product_price_point_id` | rename only |
| `productPricePointHandle` | `default` | `product.product_price_point_handle` | rename only |

**Dropped Maxio fields (~24):** `accounting_code`, `request_credit_card`, `expiration_interval(_unit)`,
`created_at`, `updated_at`, `initial_charge_in_cents`, `trial_price_in_cents`, `trial_interval(_unit)`,
`archived_at`, `return_params`, `update_return_url`, `initial_charge_after_trial`, `version_number`,
`update_return_params`, `product_family` (full nested object), `public_signup_pages`,
`product_price_point_name`, `request_billing_address`, `require_billing_address`, `require_shipping_address`,
`tax_code`, `default_product_price_point_id`, `use_site_exchange_rate`, `item_category`.

> **Test mismatch:** `ListPlansTests` asserts `intervalCount` and `requiresPaymentMethod` — **neither name
> exists** in this response (it has `interval` and `requireCreditCard`), so the test fails with
> `KeyNotFoundException`. See the failing-test report.

---

## 2. Find-or-create customer — `POST customers`
Maxio op: `GET /customers/lookup.json` → `POST /customers.json` → **`Customer-Response`** (`{"customer": Customer}`).
Integration returns a **bare flattened customer object**.

| Integration field | Value seen | Maxio `Customer` source | Note |
|---|---|---|---|
| `id` | `"98765"` | `customer.id` | **type change: Maxio `integer` → JSON string** |
| `customerId` | `"98765"` | — | **duplicate of `id`** (string); no such Maxio field |
| `reference` | `cust_12345` | `customer.reference` | ✓ |
| `firstName` | `John` | `customer.first_name` | rename |
| `lastName` | `Doe` | `customer.last_name` | rename |
| `email` | `john.doe@example.com` | `customer.email` | rename |
| `organization` | `Acme Corporation` | `customer.organization` | rename |

**Dropped Maxio fields (~30):** `cc_emails`, `created_at`, `updated_at`, `address`, `address_2`, `city`,
`state`, `state_name`, `zip`, `country`, `country_name`, `phone`, `verified`, `portal_*`, `tax_exempt`,
`vat_number`, `parent_id`, `locale`, `default_subscription_group_uid`, `salesforce_id`, `tax_exempt_reason`,
`default_auto_renewal_profile_id`.

---

## 3–8. Subscription endpoints (identical response shape)
Covers **List customer subscriptions** (`GET customers/{id}/subscriptions` — bare array), **Read**
(`GET subscriptions/{id}`), **Create** (`POST subscriptions`, 201), **Pause** (`POST …/hold`), **Resume**
(`POST …/resume`), **Reactivate** (`PUT …/reactivate`), **Commit plan change** (`POST …/migrations`), **Cancel**
(`DELETE subscriptions/{id}`).

Maxio op returns **`Subscription-Response`** (`{"subscription": Subscription}`); list returns an array of them.
Integration returns a **bare flattened subscription object** (list → bare array of them).

| Integration field | Value seen | Maxio `Subscription` source | Note |
|---|---|---|---|
| `id` | `15100121` | `subscription.id` | same name |
| `state` | `active` / `on_hold` | `subscription.state` | raw snake_case value **forwarded verbatim** (matches Maxio enum) |
| `reference` | `sub_ref_98765_gold` | `subscription.reference` | ✓ |
| `productHandle` | `gold` | `subscription.product.handle` | **flattened** from nested `product`; no scalar in Maxio |
| `productName` | `Gold Plan` | `subscription.product.name` | **flattened** |
| `productId` | `3858146` | `subscription.product.id` | **flattened** |
| `price` | `10` | — | **derived dollars** from `product.price_in_cents`; no Maxio field |
| `productPriceInCents` | `1000` | `subscription.product_price_in_cents` | rename |
| `currentPeriodStartedAt` | `2026-07-01…` | `subscription.current_period_started_at` | rename |
| `currentPeriodEndsAt` | `2026-08-01…` | `subscription.current_period_ends_at` | rename |
| `nextAssessmentAt` | `2026-08-01…` | `subscription.next_assessment_at` | rename |
| `nextBillingDate` | `2026-08-01…` | — | **synthesized alias** of `next_assessment_at`; **no Maxio field** |
| `activatedAt` | `2025-07-01…` | `subscription.activated_at` | rename |
| `createdAt` | `2025-07-01…` | `subscription.created_at` | rename |
| `cancelAtEndOfPeriod` | `false` | `subscription.cancel_at_end_of_period` | rename |
| `canceledAt` | `null` | `subscription.canceled_at` | rename |
| `delayedCancelAt` | `null` | `subscription.delayed_cancel_at` | rename |
| `customerId` | `98765` (**int**) | `subscription.customer.id` | **flattened**; note **int here** vs string in nested `customer` |
| `customerReference` | `cust_12345` | `subscription.customer.reference` | **flattened** |
| `totalRevenueInCents` | `12000` | `subscription.total_revenue_in_cents` | rename |
| `balanceInCents` | `0` | `subscription.balance_in_cents` | rename |
| `currentBillingAmountInCents` | `1000` | `subscription.current_billing_amount_in_cents` | rename (Maxio: read-op only) |
| `productPricePointId` | `1512822` | `subscription.product_price_point_id` | rename |
| `previousState` | `active` | `subscription.previous_state` | rename |
| `paymentCollectionMethod` | `automatic` | `subscription.payment_collection_method` | rename |
| `signupRevenue` | `"10.00"` | `subscription.signup_revenue` | ✓ (string) |
| `product` `{id, handle, name, priceInCents}` | — | `subscription.product` | **trimmed nested copy**: Maxio nests the **full ~40-field `Product`**; integration keeps 4, renames `price_in_cents`→`priceInCents` |
| `customer` `{id, reference, firstName, lastName, email}` | `id:"98765"` | `subscription.customer` | **trimmed nested copy**: Maxio nests the **full `Customer`**; here `id` is a **string** (top-level `customerId` is an int — inconsistent) |

**Dropped Maxio `Subscription` fields (~40):** `product_version_number`, `trial_started_at`, `trial_ended_at`,
`expires_at`, `updated_at`, `cancellation_message`, `cancellation_method`, `signup_payment_id`, `coupon_code(s)`,
`snap_day`, `credit_card`, `bank_account`, `group`, `payment_type`, `referral_code`, `next_product_id/handle`,
`coupon_use*`, `reason_code`, `automatically_resume_at`, `offer_id`, `payer_id`,
`product_price_point_type`, `next_product_price_point_id`, `net_terms`, `stored_credential_transaction_id`,
`on_hold_at`, `prepaid_dunning`, `coupons`, `dunning_communication_delay_*`, `receives_invoice_emails`,
`locale`, `currency`, `scheduled_cancellation_at`, `credit_balance_in_cents`, `prepayment_balance_in_cents`,
`prepaid_configuration`, `self_service_page_token`.

> **Test mismatches:** `ReadSubscriptionTests` / `CreateSubscriptionTests` call `TestJson.GetSubscriptionId`,
> which looks for `subscriptionId` **or** `providerSubscriptionId`. This response uses **`id`** →
> `KeyNotFoundException`. The status codes and `productHandle`/`state` all match; only id extraction fails.

---

## 9. Record usage — `POST subscriptions/{id}/components/{comp}/usages`
Maxio op: `POST /subscriptions/{id}/components/{comp}/usages.json` → **`Usage-Response`** (`{"usage": Usage}`).
Integration returns a **bare flattened usage object**.

| Integration field | Value seen | Maxio `Usage` source | Note |
|---|---|---|---|
| `id` | `138522957` | `usage.id` | same name |
| `quantity` | `5` | `usage.quantity` | Maxio type is `integer` **or** `string`; integration returns number |
| `memo` | `test` | `usage.memo` | ✓ |
| `componentId` | `641814` | `usage.component_id` | rename |
| `componentHandle` | `api-calls` | `usage.component_handle` | rename |
| `subscriptionId` | `15100121` | `usage.subscription_id` | rename |
| `pricePointId` | `555001` | `usage.price_point_id` | rename |
| `createdAt` | `2026-07-02…` | `usage.created_at` | rename |
| `periodToDateTotal` | `null` | — | **synthesized**; **no Maxio field** |

**Dropped Maxio `Usage` field:** `overage_quantity`.

> **Test mismatch:** `RecordUsageTests` calls `TestJson.GetUsageId` (looks for `usageId`/`providerUsageId`);
> this response uses **`id`** → `KeyNotFoundException`. `quantity`/`memo`/status all match.

---

## Summary of divergence *types* (across all endpoints)

| Divergence type | Examples |
|---|---|
| **Envelope stripped** | `{"subscription": {…}}` → bare object; `[{"product": …}]` → bare array |
| **snake_case → camelCase** | every field (`price_in_cents` → `priceInCents`) |
| **Curated subset** | ~24 product / ~40 subscription / ~30 customer / 1 usage Maxio fields dropped |
| **Derived dollar field** | `price` (= `price_in_cents / 100`) — not in the spec |
| **Synthesized field** | `nextBillingDate` (alias of `next_assessment_at`), `periodToDateTotal` (null) |
| **Nested-object hoisting** | `productHandle`/`productName`/`productId`, `customerId`/`customerReference` lifted out of nested `product`/`customer` |
| **Duplicated field** | `productId` (= `id`) on plans; `customerId` (= `id`) on customer response |
| **Trimmed nested copies** | `product` (4 of ~40 fields), `customer` (5 of ~30 fields) retained alongside the hoisted scalars |
| **Type changes** | customer `id`: Maxio `integer` → string; **inconsistent** — subscription-root `customerId` is an int, nested `customer.id` is a string |
| **Value passthrough** | `state` forwarded as Maxio's raw snake_case enum value (`on_hold`), not remapped |
