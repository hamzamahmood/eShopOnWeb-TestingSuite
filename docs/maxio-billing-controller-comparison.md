# MaxioBillingController Comparison: Direct vs Plugin

Comparing the two standalone `MaxioBillingTestApi` hosts (the `api/maxio` surface no longer lives in
`PublicApi` — that controller/folder was removed from both repos):
- `eShopOnWeb-Direct\src\MaxioBillingTestApi\Controllers\MaxioBillingController.cs`
- `eShopOnWeb-Plugin\MaxioBillingTestApi\Controllers\MaxioBillingController.cs`

> For a flat Maxio-endpoint-template → exposed-controller-route mapping, see
> [`maxio-billing-service-route-map.md`](./maxio-billing-service-route-map.md) and
> [`maxio-endpoint-path-mapping.md`](./maxio-endpoint-path-mapping.md).

## Summary

Each integration exposes its real `MaxioBillingClient` (via the provider-agnostic `IBillingClient` seam) as
one HTTP endpoint per client method under `/api/maxio`, from a dedicated standalone Web API host with no
database. The controller binds the request, forwards to its one client method, returns the client's typed
result untouched on success, and maps failures to an HTTP status **inline in each action's catch** (there is
no shared `ExceptionMiddleware` in these hosts). Both hosts serve the same ~15 routes and both return
**`200 OK`** on create-subscription. The interesting divergences are in **error-status mapping**, the
**`IBillingClient` method surface**, and two genuinely different routes (usage read, metered verify shape).

## Endpoints that are equivalent (same route + same underlying Maxio operation)

| # | Operation | Route (both, under `api/maxio`) | Underlying Maxio call | Notes |
|---|---|---|---|---|
| 1 | List plans | `GET product-families/{productFamilyId}/products` | `GET /product_families/{id}/products.json` | Path id inert — client uses the server-configured family |
| 2 | Find-or-create customer | `POST customers` | `GET /customers/lookup.json` → `POST /customers.json` | `customer.reference`, `email` forwarded on both |
| 3 | Customer lookup (read-only) | `GET customers/lookup?reference=` | `GET /customers/lookup.json` | Present on **both** now (Direct: `FindCustomerIdByReferenceAsync`; Plugin: `FindCustomerIdAsync`); returns id or 404 |
| 4 | List customer subscriptions | `GET customers/{customerId}/subscriptions` | `GET /customers/{id}/subscriptions.json` | |
| 5 | Read subscription | `GET subscriptions/{subscriptionId}` | `GET /subscriptions/{id}.json` | |
| 6 | Preview plan change | `POST subscriptions/{id}/migrations/preview` | `POST /subscriptions/{id}/migrations/preview.json` | Both call it immediately (Direct `PlanChangeTiming.Immediate`; Plugin `applyNow:true`) |
| 7 | Commit plan change (immediate) | `POST subscriptions/{id}/migrations` | `POST /subscriptions/{id}/migrations.json` (`preserve_period=true`) | |
| 8 | Pause | `POST subscriptions/{id}/hold` | `POST /subscriptions/{id}/hold.json` | Direct `ApplyLifecycleActionAsync(Pause)`; Plugin `PauseAsync` |
| 9 | Resume | `POST subscriptions/{id}/resume` | `POST /subscriptions/{id}/resume.json` | |
| 10 | Reactivate | `PUT subscriptions/{id}/reactivate` | `PUT /subscriptions/{id}/reactivate.json` | |
| 11 | Create subscription | `POST subscriptions` | `POST /subscriptions.json` | **200 on both** (former Plugin 201 is gone) |
| 12 | Cancel (immediate) | `DELETE subscriptions/{id}` | `DELETE /subscriptions/{id}.json` | Direct `ApplyLifecycleActionAsync(Cancel)`; Plugin `CancelAsync(endOfPeriod:false)` |
| 13 | Record usage | `POST subscriptions/{id}/components/{componentId}/usages` | `POST /subscriptions/{id}/components/{comp}/usages.json` | `{componentId}` inert on both — always server-configured |
| 14 | Metered-component verify | `GET metered-component/verify` | (see divergence) | Same route, different behavior/shape |

**Route-constraint divergence:** Plugin binds `{id:int}` (a non-numeric id route-misses → empty-body 404 →
suite auto-skips). Direct uses an `int` action parameter with no route constraint → ASP.NET model-binding
returns **400** on a non-numeric id.

## Look-alikes that are NOT equivalent

| Operation | Direct | Plugin | Why they diverge |
|---|---|---|---|
| Metered-component verify | `GET metered-component/verify` → `GetMeteredComponentAsync` returns the full `BillingComponent` (**200 + data**) | `GET metered-component/verify` → `EnsureMeteredComponentAsync` (a `void` validation) → **200 with empty body** | Same route, but Direct returns component data while Plugin only asserts config and returns nothing |
| Usage read | `GET subscriptions/{id}/component-balance` → `GetUsageTotalAsync` → a **bare int** | `GET subscriptions/{id}/components/{componentId}/summary` → `GetPeriodToDateUsageAsync` (period-to-date total) | Different route + shape |

## Error handling — the headline difference

Each host maps failures **inline per action** (no `ExceptionMiddleware`). The two policies differ sharply.

**Direct** — the client throws a single `BillingProviderException` carrying the origin Maxio `int? StatusCode`
(and inner exception for transport/parse). The controller maps it **richly, preserving the status**:

| Condition | HTTP | Category |
|---|---|---|
| `StatusCode == 400` | 400 | `invalid-request` |
| `401` / `403` | 502 | `provider-authorization-failed` |
| `404` | **404** | `{resource}-not-found` |
| `422` | **422** | `billing-rule-violation` |
| `429` | 429 | `provider-rate-limited` |
| any other status | 502 | `provider-error` |
| `StatusCode == null`, inner `HttpRequestException` | 503 | `provider-unavailable` |
| `StatusCode == null`, inner `TaskCanceledException` (timeout) | 504 | `provider-timeout` |
| `StatusCode == null`, inner `JsonException` | 502 | `provider-error` |
| `StatusCode == null`, other | 404 | `{resource}-not-found` |

**Plugin** — flat central `MapError`, ignoring the underlying Maxio status:

| Exception | HTTP | Category |
|---|---|---|
| `BillingConfigurationException` | 422 | `billing_configuration` |
| `BillingProviderException` | **502** | `billing_provider_error` |
| `OperationCanceledException` | 499 | `request_canceled` |
| anything else | 500 | `unexpected_error` |

**Consequence for the black-box suite:** failure cases expect a **4xx** (business error) or a clean 5xx (for
injected upstream faults). Direct's status-preserving mapping satisfies the 4xx cases; Plugin's flat 502 fails
them. The one 4xx Plugin gets right is not-found on **read** ops — both clients return `null` on a Maxio 404
for `GetSubscription`/customer-lookup, so those controllers return **404** — but every other Plugin op on an
unknown id becomes `BillingProviderException`→502.

## `IBillingClient` method surface (differs; same domain types)

Both interfaces live at `src/ApplicationCore/Interfaces/IBillingClient.cs` and return the same
`ApplicationCore/Entities/SubscriptionAggregate` types (`SubscriptionPlan`, `CustomerSubscription`, etc.), but
the method set differs:

- **Direct:** `ApplyLifecycleActionAsync(id, SubscriptionLifecycleAction, reason)` for
  pause/resume/cancel/reactivate; `PlanChangeTiming` enum on `PreviewPlanChangeAsync`/`ChangePlanAsync`;
  `FindCustomerIdByReferenceAsync`, `GetSubscriptionsForCustomerAsync`, `CreateSubscriptionAsync`,
  `GetMeteredComponentAsync`, `GetUsageTotalAsync`.
- **Plugin:** separate `PauseAsync`/`ResumeAsync`/`CancelAsync(id, endOfPeriod, reason)`/`ReactivateAsync`; an
  `applyNow` bool on `PreviewPlanChangeAsync`/`ChangePlanAsync`; `FindCustomerIdAsync`,
  `ListCustomerSubscriptionsAsync`, `SubscribeAsync`, `EnsureMeteredComponentAsync`,
  `GetPeriodToDateUsageAsync`, and an extra `FindPlanAsync`.

## Exceptions

- **Direct:** `BillingProviderException` (the only billing exception, carries `int? StatusCode`),
  `DuplicateException`, plus basket exceptions. **No `BillingConfigurationException`.**
- **Plugin:** adds `BillingConfigurationException`. Both dropped the older typed set
  (`SubscriptionNotFoundException`, `PaymentVerificationRequiredException`, etc.).

## Response shapes

Both return flattened domain DTOs, not Maxio's raw envelopes; the two DTO sets differ (casing, dollars vs
cents, raw vs mapped `state`). The `MaxioPassthroughApiTests` suite compares response **bodies** with an AI
verifier that matches by meaning, so field/shape drift doesn't break tests — only the HTTP status is asserted
deterministically.
