# Task: add a Maxio Advanced Billing integration to eShopOnWeb

You are working in a clean checkout of the **eShopOnWeb** reference application (ASP.NET Core, .NET 8,
Clean Architecture: ApplicationCore / Infrastructure / Web / PublicApi). Add a billing integration that
calls the **Maxio Advanced Billing** REST API.

## Goal

Expose the following **22 HTTP endpoints under `/api/billing`** in the **PublicApi** host, each backed by
a client that calls Maxio and surfaces Maxio's data/behavior. Make the endpoints **anonymous** (callable
with no auth token). You choose your own response field names and internal structure; the **request
bodies are fixed** (shown below) so they must be accepted exactly.

| # | Method · route | What it does | Request body |
|---|---|---|---|
| 1 | `GET /api/billing/plans` | list available plans → each plan's id, name, price | — |
| 2 | `POST /api/billing/customers` | find-or-create a customer (look up by reference; create if absent) → customer id | `{ "reference": string, "firstName": string, "lastName": string, "email": string }` |
| 3 | `GET /api/billing/customers/{customerId}/subscriptions` | list a customer's subscriptions → each id + state | — |
| 4 | `GET /api/billing/subscriptions/{subscriptionId}` | read a subscription → id, state, plan | — |
| 5 | `POST /api/billing/subscriptions/{subscriptionId}/pause` | pause → resulting state | — |
| 6 | `POST /api/billing/subscriptions/{subscriptionId}/resume` | resume → resulting state | — |
| 7 | `POST /api/billing/subscriptions/{subscriptionId}/reactivate` | reactivate → resulting state | — |
| 8 | `POST /api/billing/subscriptions` | create a subscription → new id + state | `{ "customerReference": string, "productHandle": string }` |
| 9 | `POST /api/billing/subscriptions/{subscriptionId}/plan-change` | change plan immediately → updated subscription | `{ "productHandle": string }` |
| 10 | `DELETE /api/billing/subscriptions/{subscriptionId}` | cancel immediately → resulting state | — |
| 11 | `POST /api/billing/subscriptions/{subscriptionId}/usage` | record metered usage → recorded usage | `{ "quantity": number, "memo": string? }` |
| 12 | `GET /api/billing/components` | list catalog components → each id, name, kind | — |
| 13 | `GET /api/billing/components/{componentId}` | read a component → id, name, kind | — |
| 14 | `POST /api/billing/components` | create a metered component → new id | `{ "name": string, "unitName": string, "pricingScheme": string }` |
| 15 | `GET /api/billing/components/{componentId}/price-points` | list a component's price points → each id, name | — |
| 16 | `POST /api/billing/components/{componentId}/price-points` | create a price point → new id | `{ "name": string, "pricingScheme": string, "unitPrice": string }` |
| 17 | `GET /api/billing/subscriptions/{subscriptionId}/components` | list a subscription's components → each id + allocated quantity | — |
| 18 | `GET /api/billing/subscriptions/{subscriptionId}/components/{componentId}/allocations` | list allocations → each id + quantity | — |
| 19 | `POST /api/billing/subscriptions/{subscriptionId}/components/{componentId}/allocations` | create an allocation → id + quantity | `{ "quantity": number, "memo": string? }` |
| 20 | `GET /api/billing/subscriptions/{subscriptionId}/invoices` | list a subscription's invoices → each uid/number, total, status | — |
| 21 | `GET /api/billing/coupons` | list coupons → each id + code | — |
| 22 | `POST /api/billing/coupons` | create a coupon → new id + code | `{ "code": string, "name": string, "percentage": string }` |

The Maxio product-family id and metered-component id are supplied via configuration (below); do not put
them in the routes.

## Production-readiness requirements (acceptance criteria)

Your integration must satisfy **every** property below:

**Resilience**
- Transient upstream errors (HTTP 5xx) and rate limiting (429) on safe reads recover automatically.
- A transport-level failure (connection reset/refused, DNS, TLS) or a hung/unresponsive upstream must
  never crash the app or hang forever — it must surface as a clean, mapped error within a bounded time.
- Retries must be bounded (never retry forever).
- A failed **write** (e.g. create subscription, plan change, record usage, create allocation, create component/price-point/coupon) must **never be duplicated** by a retry.

**Error handling**
- Upstream/domain errors map to sensible HTTP status codes for your caller.
- An unknown resource returns a client error (4xx), never a 5xx or a crash.
- Malformed upstream responses are handled gracefully (a mapped error, no crash).
- No error response leaks internal details — no stack traces, exception text, secrets, or the raw upstream
  response body.

**Correctness**
- Each endpoint returns the expected data on the happy path.
- Unexpected/extra fields in upstream responses are tolerated.
- Invalid requests are rejected locally (4xx) **without** calling upstream.

**Security**
- The API key comes from configuration and **never** appears in logs.
- Authentication is applied to every upstream call.

**Configuration**
- Required configuration is validated at startup (fail fast) rather than erroring on the first request.

## Environment / configuration

The app is launched with these settings (provided as environment variables); read them from the `Maxio`
configuration section (`Maxio__*`), and never hardcode the base URL or key:

- `Maxio__BaseUrl` — the Maxio API base URL
- `Maxio__ApiKey` — API key (HTTP **Basic**: username = the API key, password = the literal `"x"`)
- `Maxio__Subdomain` — the Maxio site
- `Maxio__ProductFamilyId`, `Maxio__ProductFamilyHandle` — the product family (path param for "list plans")
- `Maxio__MeteredComponentId`, `Maxio__MeteredComponentHandle` — the metered component (path param for "record usage")
- `UseOnlyInMemoryDatabase=true` — the app uses the in-memory database and must boot with this set.

## Definition of done

The integration is complete when the production-readiness gate passes. From the repository root run:

```
.\gate.cmd
```

It boots the app and the provider, runs the checks, and prints `[PASS]`/`[FAIL]` per check with a short
message. **Iterate until every check is green.** You may run it as often as you like. You cannot read the
gate's source, or the provider's source or data.

## Maxio API reference

{{ARM_MATERIAL}}

## Constraints

- Put your integration code in the eShopOnWeb layers (ApplicationCore / Infrastructure / PublicApi),
  following the repository's existing patterns.
- Do not change the routes above — the gate targets them exactly.
- Do not hardcode responses to make the gate pass; the endpoints must genuinely call Maxio.
