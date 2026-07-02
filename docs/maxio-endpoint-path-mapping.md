# Maxio Endpoint ↔ Exposed Controller Path Mapping

Maps each **Maxio Advanced Billing API endpoint template** (the upstream Chargify/Maxio wire path) to the
**exposed `MaxioBillingController` route** (`/api/maxio/...`) that fronts it, for both integrations.

Source of truth:
- `eShopOnWeb-Direct/src/PublicApi/MaxioBilling/MaxioBillingController.cs`
- `eShopOnWeb-Plugin/src/PublicApi/MaxioBilling/MaxioBillingController.cs`

Conventions used below:
- Controller routes are shown **without** the `api/maxio` prefix (every route carries it).
- Maxio path params are shown in their upstream snake_case form (`{subscription_id}`, `{product_family_id}`, `{component_id}`).
- **Config-fixed** = the Maxio path/query param is not taken from the caller; it is supplied from `MaxioSettings`
  (`ProductFamilyId`, `MeteredComponentId`). Such segments are omitted from the route (Direct) or accepted-but-inert (Plugin).
- Direct constrains numeric ids in the route with `:int`; Plugin accepts them as `string` and validates numeric-ness in code.

---

## eShopOnWeb-Direct (raw HTTP client)

| # | Exposed controller route (under `/api/maxio`) | Underlying Maxio endpoint template | Notes |
|---|---|---|---|
| 1 | `GET product-families/{productFamilyId}/products` | `GET /product_families/{product_family_id}/products.json` | `productFamilyId` in route is inert — client uses config-fixed `product_family_id`. Query params accepted, not forwarded. |
| 2 | `GET metered-component` | `GET /product_families/{product_family_id}/components/{component_id}.json` | Both path params config-fixed; endpoint takes no input. |
| 3 | `POST customers` | `GET /customers/lookup.json` → `POST /customers.json` | Composite: lookup by `reference`, create only if absent. |
| 4 | `GET customers/{customerId:int}/subscriptions` | `GET /customers/{customer_id}/subscriptions.json` | — |
| 5 | `POST subscriptions` | `POST /subscriptions.json` | Success **200 OK**. |
| 6 | `GET subscriptions/{subscriptionId:int}` | `GET /subscriptions/{subscription_id}.json` | `include[]` query inert. |
| 7 | `POST subscriptions/{subscriptionIdOrReference:int}/usages` | `GET /product_families/{product_family_id}/components/{component_id}.json` → `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json` | **Two calls:** `readComponent` config-guard first, then the usage POST. `component_id` config-fixed (no route segment). |
| 8 | `GET subscriptions/{subscriptionId:int}/component-balance` | `GET /subscriptions/{subscription_id}/components/{component_id}.json` | One call; returns a bare int balance. `component_id` config-fixed. |
| 9 | `POST subscriptions/{subscriptionId:int}/migrations/preview` | `POST /subscriptions/{subscription_id}/migrations/preview.json` | `preserve_period` always true (not forwarded). |
| 10 | `POST subscriptions/{subscriptionId:int}/migrations` | `POST /subscriptions/{subscription_id}/migrations.json` (`preserve_period=true`) | Immediate, prorated plan change. |
| 11 | `PUT subscriptions/{subscriptionId:int}` | `PUT /subscriptions/{subscription_id}.json` (`product_change_delayed=true`) | Dedicated route for "schedule plan change at renewal". |
| 12 | `POST subscriptions/{subscriptionId:int}/hold` | `POST /subscriptions/{subscription_id}/hold.json` | Body inert (always indefinite hold). |
| 13 | `POST subscriptions/{subscriptionId:int}/resume` | `POST /subscriptions/{subscription_id}/resume.json` | Resumption-charge query inert. |
| 14 | `DELETE subscriptions/{subscriptionId:int}` | `DELETE /subscriptions/{subscription_id}.json` | Immediate cancel. |
| 15 | `POST subscriptions/{subscriptionId:int}/delayed_cancel` | `POST /subscriptions/{subscription_id}/delayed_cancel.json` | Dedicated route for "cancel at end of period". |
| 16 | `PUT subscriptions/{subscriptionId:int}/reactivate` | `PUT /subscriptions/{subscription_id}/reactivate.json` | Body inert. |

## eShopOnWeb-Plugin (`AsadAli.AdvancedBilling.Sdk` generated SDK)

> SDK method names describe intent, not always the wire path — the templates below are the actual HTTP paths the
> SDK issues (e.g. `PauseSubscription()` hits `/hold.json`). Verify by extracting UTF-16LE string literals from
> `MaxioAdvancedBilling.dll`, not from method names.

| # | Exposed controller route (under `/api/maxio`) | Underlying Maxio endpoint template | Notes |
|---|---|---|---|
| 1 | `GET product-families/{productFamilyId}/products` | `GET /product_families/{product_family_id}/products.json` | `productFamilyId` inert; config-fixed family. Query params inert. |
| 2 | `POST customers` | `GET /customers/lookup.json` → `POST /customers.json` | Composite find-or-create. Blank names default to `"eShopOnWeb"`/`"Customer"`. |
| 3 | `GET customers/lookup?reference=` | `GET /customers/lookup.json` | **Plugin-only** read-only lookup; 404 when absent. |
| 4 | `POST subscriptions` | `POST /subscriptions.json` | Success **201 Created** (with `Location` header). |
| 5 | `GET subscriptions/{subscriptionId}` | `GET /subscriptions/{subscription_id}.json` | `include` query inert. |
| 6 | `GET customers/{customerId}/subscriptions` | `GET /customers/{customer_id}/subscriptions.json` | — |
| 7 | `GET metered-component/verify` | `GET /components/lookup.json?handle={handle}` | Site-wide `FindComponent` lookup, then checks kind + family in code. `handle` query inert. Returns **204 No Content**. |
| 8 | `POST subscriptions/{subscriptionId}/components/{componentId}/usages` | `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json` | `componentId` path segment inert (config-fixed). |
| 9 | `GET subscriptions/{subscriptionId}/components/{componentId}/summary` | `GET /subscriptions/{subscription_id}/components/{component_id}.json` **+** `GET /subscriptions/{subscription_id}.json` | **Two calls** composited. `componentId` inert. |
| 10 | `POST subscriptions/{subscriptionId}/migrations/preview` | Immediate → `POST /subscriptions/{subscription_id}/migrations/preview.json`; AtRenewal → **no Maxio call** (locally-computed zero quote) | Behavior forks on the `timing` control field. |
| 11 | `POST subscriptions/{subscriptionId}/migrations` | Immediate → `POST /subscriptions/{subscription_id}/migrations.json` (`preserve_period=true`); AtRenewal → `PUT /subscriptions/{subscription_id}.json` | `timing` selects the underlying op. Success **200 OK**. |
| 12 | `POST subscriptions/{subscriptionId}/hold` | `POST /subscriptions/{subscription_id}/hold.json` | Body inert. |
| 13 | `POST subscriptions/{subscriptionId}/resume` | `POST /subscriptions/{subscription_id}/resume.json` | Resumption-charge query inert. |
| 14 | `DELETE subscriptions/{subscriptionId}` | Immediate → `DELETE /subscriptions/{subscription_id}.json`; EndOfPeriod → `POST /subscriptions/{subscription_id}/delayed_cancel.json` | `timing` control field selects the underlying op. |
| 15 | `PUT subscriptions/{subscriptionId}/reactivate` | `PUT /subscriptions/{subscription_id}/reactivate.json` | Body inert. |

---

## Route-only divergences at a glance

Same underlying Maxio call, different exposed route/verb:

| Maxio endpoint template | Direct route | Plugin route |
|---|---|---|
| `PUT /subscriptions/{subscription_id}.json` (delayed plan change) | dedicated `PUT subscriptions/{id}` | `POST subscriptions/{id}/migrations` with `timing:"AtRenewal"` |
| `POST /subscriptions/{subscription_id}/delayed_cancel.json` | dedicated `POST subscriptions/{id}/delayed_cancel` | `DELETE subscriptions/{id}` with `timing:"EndOfPeriod"` |
| `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json` | `POST subscriptions/{id}/usages` (no component segment) | `POST subscriptions/{id}/components/{componentId}/usages` (segment inert) |
| metered-component verify | `GET metered-component` → `readComponent` (family-scoped, full body) | `GET metered-component/verify` → `components/lookup.json` (site-wide, 204 no body) |
| usage balance/summary | `GET subscriptions/{id}/component-balance` (1 call, bare int) | `GET subscriptions/{id}/components/{id}/summary` (2 calls composited) |
| `GET /customers/lookup.json` (standalone) | *(none — only inside `POST customers`)* | `GET customers/lookup?reference=` |
