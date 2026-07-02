# MaxioBillingController Comparison: Direct vs Plugin

Comparing:
- `eShopOnWeb-Direct\src\PublicApi\MaxioBilling\MaxioBillingController.cs`
- `eShopOnWeb-Plugin\src\PublicApi\MaxioBilling\MaxioBillingController.cs`

## Summary

Both expose `IBillingClient` as one HTTP endpoint per client method under `/api/maxio`, replacing the old raw-passthrough controller. Neither returns Maxio's raw response body anymore — both return flattened, provider-agnostic DTOs, and errors are remapped by the shared `ExceptionMiddleware` instead of being passed through with Maxio's exact status/body. Most endpoints line up route-for-route, but several diverge in route, request contract, or how many underlying Maxio calls they make — those divergences are the useful signal for anyone comparing the two integrations or extending the `MaxioPassthroughApiTests` suite.

## Endpoints that are equivalent (same route + same underlying Maxio operation)

| # | Operation | Direct route | Plugin route | Underlying Maxio call | Input |
|---|---|---|---|---|---|
| 1 | List plans | `GET /api/maxio/product-families/{productFamilyId}/products` | `GET /api/maxio/product-families/{productFamilyId}/products` | `GET /product_families/{id}/products.json` (`listProductsForProductFamily`) | Path id + all query params accepted but **inert on both** — always uses the server-configured family |
| 2 | Find-or-create customer | `POST /api/maxio/customers` | `POST /api/maxio/customers` | `GET /customers/lookup.json` → `POST /customers.json` if absent (`readCustomerByReference` + `createCustomer`) | `customer.reference`, `email` forwarded on both. Direct requires reference/email/first_name/last_name all non-blank; Plugin requires only reference/email (blank names default to "eShopOnWeb"/"Customer") |
| 3 | List customer subscriptions | `GET /api/maxio/customers/{customerId:int}/subscriptions` | `GET /api/maxio/customers/{customerId}/subscriptions` | `GET /customers/{customer_id}/subscriptions.json` (`listCustomerSubscriptions`) | `customerId` path param only |
| 4 | Read subscription | `GET /api/maxio/subscriptions/{subscriptionId:int}` | `GET /api/maxio/subscriptions/{subscriptionId}` | `GET /subscriptions/{subscription_id}.json` (`readSubscription`) | `subscriptionId` path only; `include` query inert on both |
| 5 | Pause subscription | `POST /api/maxio/subscriptions/{subscriptionId:int}/hold` | `POST /api/maxio/subscriptions/{subscriptionId}/hold` | `POST /subscriptions/{id}/hold.json` (`pauseSubscription`) | `subscriptionId` path only; body entirely inert on both |
| 6 | Resume subscription | `POST /api/maxio/subscriptions/{subscriptionId:int}/resume` | `POST /api/maxio/subscriptions/{subscriptionId}/resume` | `POST /subscriptions/{id}/resume.json` (`resumeSubscription`) | `subscriptionId` path only; resumption-charge query inert on both |
| 7 | Reactivate subscription | `PUT /api/maxio/subscriptions/{subscriptionId:int}/reactivate` | `PUT /api/maxio/subscriptions/{subscriptionId}/reactivate` | `PUT /subscriptions/{id}/reactivate.json` (`reactivateSubscription`) | `subscriptionId` path only; body entirely inert on both |
| 8 | Create subscription | `POST /api/maxio/subscriptions` | `POST /api/maxio/subscriptions` | `POST /subscriptions.json` (`createSubscription`) | `subscription.customer_id` + `product_handle` forwarded by both. **Caveat:** Direct also optionally forwards a `chargify_token` payment method; Plugin declares `payment_profile_attributes`/`credit_card_attributes` inert and never forwards one |
| 9 | Commit plan change (immediate) | `POST /api/maxio/subscriptions/{subscriptionId:int}/migrations` | `POST /api/maxio/subscriptions/{subscriptionId}/migrations` with `timing:"Immediate"` | `POST /subscriptions/{id}/migrations.json`, `preserve_period=true` (`migrateSubscriptionProduct`) | `subscriptionId` path + `migration.product_handle` body on both. Plugin additionally requires a `timing` control field — must be `"Immediate"` to match Direct's behavior |
| 10 | Cancel subscription (immediate) | `DELETE /api/maxio/subscriptions/{subscriptionId:int}` | `DELETE /api/maxio/subscriptions/{subscriptionId}` with `timing:"Immediate"` | `DELETE /subscriptions/{id}.json` (`cancelSubscription`) | `subscriptionId` path + `subscription.cancellation_message` body on both. Plugin additionally requires `timing:"Immediate"` to match |
| 11 | Record usage | `POST /api/maxio/subscriptions/{subscriptionIdOrReference:int}/usages` | `POST /api/maxio/subscriptions/{subscriptionId}/components/{componentId}/usages` | `POST /subscriptions/{id}/components/{component_id}/usages.json` (`createUsage`) | `subscriptionId` path + `usage.quantity` (required) + `usage.memo` body on both. Plugin's `componentId` path segment is inert (always server-configured). **Caveat:** Direct also calls `readComponent` first to verify configuration before every usage call — Plugin does not |

## Look-alikes that are NOT equivalent

| Operation | Direct | Plugin | Why they diverge |
|---|---|---|---|
| Metered component read | `GET metered-component` → `readComponent` (family-scoped, returns full component data) | `GET metered-component/verify` → `FindComponent`/`GET /components/lookup.json?handle=` (site-wide lookup, then manually checks kind + family), returns `204` with no body | Different Maxio operation entirely, different response shape |
| Usage balance / summary | `GET .../component-balance` — **one** call (`readSubscriptionComponent`), returns a bare int balance | `GET .../components/{id}/summary` — **two** calls (`readSubscriptionComponent` **and** `readSubscription`, composited) | Plugin does strictly more work / different Maxio call count |
| Preview plan change | `POST .../migrations/preview` — always calls `previewSubscriptionProductMigration` | Same route, but `timing:"AtRenewal"` **never calls Maxio** (locally computed quote of 0); only `timing:"Immediate"` matches Direct | Behavior forks on the `timing` field |
| Schedule plan change at renewal | Dedicated `PUT /subscriptions/{id}` (`updateSubscription`, `product_change_delayed=true`) | Only reachable via `POST .../migrations` with `timing:"AtRenewal"` | Different route/verb for the same eventual Maxio call |
| Schedule cancel at end of period | Dedicated `POST .../delayed_cancel` (`initiateDelayedCancellation`) | Only reachable via `DELETE /subscriptions/{id}` with `timing:"EndOfPeriod"` | Different route/verb for the same eventual Maxio call |
| Customer lookup only | No standalone equivalent (lookup only happens inside the composite `POST customers`) | `GET customers/lookup?reference=` (`readCustomerByReference`, read-only) | Plugin-only endpoint |

## Error handling

Both propagate `BillingProviderException` / domain exceptions to the shared `ExceptionMiddleware`, but the two middlewares map statuses differently (see each repo's `PublicApi/Middleware/ExceptionMiddleware.cs`) — most notably a `BillingProviderException` with a 4xx origin status becomes **422** on Direct but **502** on Plugin. Neither controller passes Maxio's raw error body/status through anymore, unlike the old passthrough controller.

## Response shapes

Both return flattened DTOs, not Maxio's raw envelopes, but the two DTO sets differ per integration (e.g. plan price is `priceInCents` on Direct vs `price` in dollars on Plugin; subscription `state` is lowercase `"active"` on Direct vs `"Active"` on Plugin). See `IBillingClient` / `BillingModels.cs` (Direct) and `Models/Subscriptions/*.cs` (Plugin) for the exact shapes.
