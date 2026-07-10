# MaxioMockServer — error codes by endpoint

Cross-references every route in `MaxioMockServer/Program.cs` against the Maxio OpenAPI spec
(`openAPI/openapi.yaml`) to document which HTTP status codes each mock endpoint actually returns,
what triggers each one, the response body shape, and whether that status is documented in the spec
for the corresponding operation.

Two response shapes are used throughout: the **Error-List-Response** `{"errors": [...]}` (or, for
one field-level example, `{"errors": {field: "message"}}`), and a **bare JSON string** (products-list
404 only). Cancel-subscription is the sole endpoint using a **singular** `{"error": "..."}` key.

All `[422]` bodies below (except the pre-route validation ones) come from the route handler itself,
matching Maxio's real validation-error messages from the spec's own examples where one exists.

## 1. `GET /product_families/{product_family_id}/products.json`

| Status | Trigger | Body |
|---|---|---|
| 200 | Known family id | Products array |
| 404 | Unknown family id | Bare string `"A valid product_family_id is required"` |

Spec (`listProductsForProductFamily`): documents **200 + 404** — matches exactly, including the
bare-string body shape.

## 2. `GET /customers/lookup.json?reference=`

| Status | Trigger | Body |
|---|---|---|
| 200 | Known reference, or a recovered `retry_`/`ratelimit_`/`race_` reference on its 2nd attempt | Customer object |
| 404 | Blank/missing `reference` | `{"errors": ["A customer reference is required."]}` |
| 404 | Unknown reference (incl. `race_*` on its 1st attempt) | `{"errors": ["Customer not found."]}` |
| 503 | `retry_*` reference, 1st attempt (simulated transient outage) | `{"errors": ["The billing service is temporarily unavailable. Please retry."]}` |
| 429 | `ratelimit_*` reference, 1st attempt (simulated rate limit); sends `Retry-After: 1` | `{"errors": ["Too many requests for this customer. You can perform 5 requests within 00:30:00."]}` |
| *(connection reset, no HTTP status)* | `connbreak_*` reference, every 4th call across a shared counter | none — `ctx.Abort()`, no response bytes written |

Spec (`readCustomerByReference`): documents **200 only** — the 404/429/503/connection-reset paths
are all undocumented in the spec. The 503/429/connbreak behaviors are comparison-harness-only
simulations (not modeling anything Maxio documents for this specific operation) used to prove both
integrations' retry logic recovers idempotent GETs; see `RetrySafetyTests` /
`ResilientRetryRecoveryTests` in `MaxioApiTests`.

## 3. `GET /customers/{customer_id}/subscriptions.json`

| Status | Trigger | Body |
|---|---|---|
| 200 | Known customer id | Subscriptions array |
| 404 | Unknown customer id | `{"errors": ["Customer not found."]}` |

Spec (`listCustomerSubscriptions`): documents **200 only** — the 404 is undocumented (spec gap).

## 4. `POST /customers.json`

| Status | Trigger | Body |
|---|---|---|
| 422 | *(pre-route)* `customer` wrapper missing/not an object, or `first_name`/`last_name`/`email` missing or wrong type, or `reference` wrong type | `{"errors": [...]}` — `StrictValidationMiddleware` |
| 422 | Blank/whitespace `email` | `{"errors": ["Email address: cannot be blank."]}` |
| 422 | Reference already taken (known reference, or a `race_*` reference that "lost" a concurrent create) | `{"errors": ["Reference has already been taken"]}` |
| 200 | Fresh reference | Customer object, fixed id `98766` |

Spec (`createCustomer`): documents **200 + 422**. The spec's 422 examples show *two* shapes — a
field-level dict `{"errors": {"customer": "can't be blank"}}` and a list
`{"errors": ["First name: cannot be blank.", ...]}`. The mock only ever produces the **list** shape
(both from `StrictValidationMiddleware` and the route), never the dict shape.

## 5. `GET /subscriptions/{subscription_id}.json`

| Status | Trigger | Body |
|---|---|---|
| 200 | Known subscription id | Subscription object |
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |

Spec (`readSubscription`): documents **200 only** — the 404 is undocumented (spec gap).

## 6. `POST /subscriptions.json`

| Status | Trigger | Body |
|---|---|---|
| 422 | *(pre-route)* `subscription` wrapper not an object, `customer_id`/`product_id` not numeric, or `payment_collection_method` not one of `automatic`/`remittance`/`prepaid`/`invoice` | `{"errors": [...]}` — `StrictValidationMiddleware` |
| 422 | Unknown/missing `customer_id` | `{"errors": ["Customer: must exist"]}` |
| 422 | `product_handle` is one of `card-required` / `threeds-required` / `card-declined` (simulated payment failure) | `{"errors": [card/payment/3-D-Secure messages]}` |
| 422 | Unknown/missing `product_handle` | `{"errors": ["Product doesn't exist"]}` |
| 201 | Known customer + known product handle | Subscription object |

Spec (`createSubscription`): documents **201 + 422** — shape matches (`Error-List-Response`); the
spec's own 422 example (bank routing/account number blank) isn't one the mock reproduces, but the
shape is identical.

## 7. `POST /subscriptions/{subscription_id}/hold.json` (Pause)

| Status | Trigger | Body |
|---|---|---|
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |
| 422 | Any subscription id other than the active canned one (`15100121`) | `{"errors": ["This subscription is not eligible to be put on hold."]}` |
| 200 | `15100121` | Subscription object, state `on_hold` |

Spec (`pauseSubscription`): documents **200 + 422** (message matches verbatim) — the 404 is
undocumented (spec gap).

## 8. `POST /subscriptions/{subscription_id}/resume.json`

| Status | Trigger | Body |
|---|---|---|
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |
| 422 | Any subscription id other than the on-hold canned one (`15100210`) | `{"errors": ["Only subscriptions that are on hold can be resumed."]}` |
| 200 | `15100210` | Subscription object, state `active` |

Spec (`resumeSubscription`): documents **200 + 422** (message matches verbatim) — the 404 is
undocumented (spec gap).

## 9. `PUT /subscriptions/{subscription_id}/reactivate.json`

| Status | Trigger | Body |
|---|---|---|
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |
| 422 | Any subscription id other than the canceled canned one (`15100299`) | `{"errors": ["Cannot reactivate a subscription that is not marked \"Canceled\", \"Unpaid\", or \"Trial Ended\"."]}` |
| 200 | `15100299` | Subscription object, state `active` |

Spec (`reactivateSubscription`): documents **200 + 422** — the mock's message is the first of the
spec's three example error strings, verbatim. The 404 is undocumented (spec gap).

## 10. `POST /subscriptions/{subscription_id}/migrations.json`

| Status | Trigger | Body |
|---|---|---|
| 422 | *(pre-route)* `migration` wrapper not an object, `product_id` not numeric, or `preserve_period` not boolean | `{"errors": [...]}` — `StrictValidationMiddleware` |
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |
| 422 | Subscription id other than the active canned one (`15100121`) | `{"errors": ["Subscription must be active"]}` |
| 422 | Unknown/missing target `product_handle` | `{"errors": ["Invalid Product"]}` |
| 200 | `15100121` + known target product handle | Subscription object with the new product |

Spec (`migrateSubscriptionProduct`): documents **200 + 422** — `"Invalid Product"` is one of the
spec's three example error strings, verbatim. The 404 is undocumented (spec gap).

## 11. `DELETE /subscriptions/{subscription_id}.json` (Cancel)

| Status | Trigger | Body |
|---|---|---|
| 422 | *(pre-route)* optional body present but `subscription` present-and-not-an-object, or `cancellation_message`/`reason_code` wrong type | `{"errors": [...]}` — `StrictValidationMiddleware` |
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |
| 422 | Already-canceled canned subscription (`15100299`) | `{"error": "The subscription is already canceled"}` — **singular** `error` key |
| 200 | Any other known subscription id | Subscription object, state `canceled` |

Spec (`cancelSubscription`): documents **200 + 404 (no body) + 422** — the mock's 404 has a body
where the spec's doesn't (a strengthening, not a divergence in status code). The 422's singular
`error` key is an exact match to the spec's first example; the spec also documents two other 422
list-shaped examples the mock doesn't reproduce (field length / type validation on cancel options).

## 12. `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json`

| Status | Trigger | Body |
|---|---|---|
| 422 | *(pre-route)* `usage` wrapper not an object, `quantity` not numeric, or `memo` not a string | `{"errors": [...]}` — `StrictValidationMiddleware` |
| 404 | Unknown subscription id | `{"errors": ["Subscription not found."]}` |
| 200 | Known subscription id (any `component_id` — **not validated** by the mock) | Usage object |

Spec (`createUsage`): documents **200 + 422** (`"Price point: could not be found."`) — the mock
never returns that specific 422 since `component_id` isn't validated at all; both integrations only
ever send the one metered component from config, so this is intentionally unexercised, not a gap. The
404 is undocumented in the spec (spec gap).

## 13. `GET /product_families/{product_family_id}/components/{component_id}.json`

| Status | Trigger | Body |
|---|---|---|
| 200 | Known family id **and** known component id/handle | Component object |
| 404 | Unknown family id, or unknown component id/handle | `{"errors": ["Component not found."]}` |

Spec (`readComponent`): documents **200 only** — the 404 is undocumented (spec gap).

## Fallback — any unmatched route

| Status | Trigger | Body |
|---|---|---|
| 404 | Any path/method not matched by the 13 routes above | `{"errors": ["The requested resource was not found."]}` |

Not a Maxio operation — a mock-only catch-all (`app.MapFallback`).

## Summary

- **Every mutating route** (`POST`/`PUT`/`DELETE` with a body) can also 422 from
  `StrictValidationMiddleware` *before* the route's own business-logic 422s — these are contract
  violations (missing wrapper, wrong type, bad enum), distinct from the domain 422s listed per route.
- **Every subscription-scoped route** (read/hold/resume/reactivate/migrate/cancel/usage) 404s the same
  way for an unknown id — `{"errors": ["Subscription not found."]}` — even though the spec only
  documents that 404 for `cancelSubscription`; the mock is intentionally more complete than the spec
  here to make "unknown id" behavior testable everywhere.
- **404 body shape is the one real spec inconsistency the mock preserves rather than papers over**:
  products-list returns a bare string, every customer/subscription/component route returns
  `{"errors": [...]}`.
- **Cancel is the only endpoint with a singular `error` key** in its error body — everything else uses
  the plural `errors` array.
- **429 / 503 / connection-reset** on customer lookup are mock-only comparison-harness devices (keyed
  off reference prefixes `ratelimit_`/`retry_`/`connbreak_`), not derived from any documented Maxio
  status for that operation — they exist purely to exercise both integrations' retry/resilience paths.
