# Maxio Advanced Billing — Mock Server

A tiny ASP.NET Core (.NET 10, Minimal API) mock of the **Maxio Advanced Billing (Chargify)** API,
for local development/testing of the eShop integration. It serves fixed, spec-shaped responses for
the Maxio operations both `MaxioBillingController`s (Direct and Plugin) call, and logs every
request/response.

Base URL: **`http://localhost:8080`**

## Endpoints

| Purpose | Route |
|---------|-------|
| List available plans (products) | `GET /product_families/{product_family_id}/products.json` |
| Look up customer by reference | `GET /customers/lookup.json?reference=...` |
| Create (find-or-create) a customer | `POST /customers.json` |
| List a customer's subscriptions | `GET /customers/{customer_id}/subscriptions.json` |
| Read a subscription | `GET /subscriptions/{subscription_id}.json` |
| Create a subscription | `POST /subscriptions.json` |
| Pause (hold) a subscription | `POST /subscriptions/{subscription_id}/hold.json` |
| Resume a subscription | `POST /subscriptions/{subscription_id}/resume.json` |
| Reactivate a subscription | `PUT /subscriptions/{subscription_id}/reactivate.json` |
| Migrate a subscription's product | `POST /subscriptions/{subscription_id}/migrations.json` |
| Cancel a subscription | `DELETE /subscriptions/{subscription_id}.json` |
| Record usage | `POST /subscriptions/{subscription_id}/components/{component_id}/usages.json` |
| Read the configured metered component | `GET /product_families/{product_family_id}/components/{component_id}.json` |

Responses match the shapes in `../openapi.yaml` (`Product-Response`, `Customer-Response`,
`Subscription-Response`, `Usage-Response`, `Component-Response`, and the `{ "errors": [...] }` /
`{ "error": "..." }` error shapes).

## Known (canned) identifiers

Responses are **static**, keyed by known ids/handles that resolve to one of a handful of canned
states — see `MockStore.cs` for the exact sets:

| Known value | Meaning |
|---|---|
| Product family `527890` / `handle:acme-projects` | The configured product family |
| Product handles `gold`, `zero-dollar-product` | The two known plans |
| Customer reference `cust_12345` → id `98765` | The one pre-existing customer |
| Subscription `15100121` | **active**, Gold Plan, customer 98765 |
| Subscription `15100210` | **on_hold** |
| Subscription `15100299` | **canceled** |
| Component `641814` / `handle:api-calls` | The configured metered component |

Every subscription-lifecycle action (pause/resume/reactivate/migrate/cancel) only succeeds from the
canonical "from" state a real Maxio transition would require — e.g. only the **active** subscription
can be paused or migrated; only the **on_hold** one can be resumed; only the **canceled** one can be
reactivated. Acting on the wrong id (or a not-in-any-set id) returns the same 422/404 a real Maxio
call would (see each route's XML doc in `Program.cs` for the exact message). Mutating routes are
**stateless** — they parse the canonical canned body and return a patched copy (`MockStore.WithState`
/ `WithProduct`); the stored template is never modified, so results are deterministic across repeated
calls and test-ordering-independent.

`POST /customers.json` with a reference NOT already known always "creates" successfully with a fixed
id (`98766`) — repeat calls for the same fresh reference return the same id, mirroring Maxio's real
per-reference idempotency guarantee. A reference that IS already known (`cust_12345`) is rejected as a
duplicate (`422`).

### Comparison-harness behaviors

A few prefixed/named inputs drive scenarios the `../MaxioPassthroughApiTests` suite uses to contrast the
two integrations:

| Input | Behavior |
|---|---|
| Customer reference `race_*` | Reproduces a concurrent-create race: the **first** `GET /customers/lookup.json` misses (404), `POST /customers.json` then returns `422` "Reference has already been taken", and a **subsequent** lookup returns `200`. Backs `PluginAdvantageTests` — the Plugin recovers by re-reading; the Direct client does not. |
| Product handle `card-required` on `POST /subscriptions.json` | Returns a `422` with card/payment validation messages, as Maxio would when a subscription needs a verified payment method. The Plugin classifies this as a typed payment-verification error; the Direct client surfaces it generically. |
| Customer reference `retry_*` / `ratelimit_*` on lookup | First attempt fails transiently (`503` / `429 + Retry-After`), a retried attempt succeeds. **Not** a Direct-vs-Plugin differentiator — both integrations retry idempotent GETs — retained as a capability only. |

The canned data is internally consistent, so a realistic flow works end-to-end:
lookup `cust_12345` → customer `id: 98765` → its one subscription `15100121`.

You can add/edit ids in `MockStore.cs` and payloads in `MockData/*.json`.

## Run

```sh
cd MaxioMockServer
dotnet run
```

The server listens on `http://localhost:8080` (see the startup log line).

## Try it

Success paths:

```sh
curl http://localhost:8080/product_families/527890/products.json
curl "http://localhost:8080/customers/lookup.json?reference=cust_12345"
curl http://localhost:8080/customers/98765/subscriptions.json
curl http://localhost:8080/subscriptions/15100121.json
curl -X POST http://localhost:8080/subscriptions/15100121/hold.json
curl -X POST http://localhost:8080/subscriptions/15100210/resume.json
curl -X PUT http://localhost:8080/subscriptions/15100299/reactivate.json
curl -X DELETE http://localhost:8080/subscriptions/15100121.json
curl -X POST "http://localhost:8080/subscriptions.json" -H 'Content-Type: application/json' \
  -d '{"subscription":{"customer_id":98765,"product_handle":"gold"}}'
curl -X POST "http://localhost:8080/subscriptions/15100121/migrations.json" -H 'Content-Type: application/json' \
  -d '{"migration":{"product_handle":"zero-dollar-product"}}'
curl -X POST "http://localhost:8080/subscriptions/15100121/components/handle:api-calls/usages.json" \
  -H 'Content-Type: application/json' -d '{"usage":{"quantity":10,"memo":"test"}}'
curl -X POST "http://localhost:8080/customers.json" -H 'Content-Type: application/json' \
  -d '{"customer":{"reference":"new_ref_1","email":"a@b.com"}}'
```

Error paths (add `-i` to see the status code):

```sh
curl -i http://localhost:8080/product_families/999/products.json         # 404 bare JSON string
curl -i "http://localhost:8080/customers/lookup.json?reference=nope"     # 404 errors body
curl -i "http://localhost:8080/customers/lookup.json"                    # 404 missing reference
curl -i http://localhost:8080/customers/111111/subscriptions.json        # 404 errors body
curl -i http://localhost:8080/customers/abc/subscriptions.json           # 404 (non-integer id)
curl -i http://localhost:8080/subscriptions/88888888.json                # 404 unknown subscription
curl -i -X POST http://localhost:8080/subscriptions/15100210/hold.json   # 422 already on hold
curl -i -X POST http://localhost:8080/subscriptions/15100121/resume.json # 422 not on hold
curl -i -X PUT http://localhost:8080/subscriptions/15100121/reactivate.json  # 422 not canceled
curl -i -X DELETE http://localhost:8080/subscriptions/15100299.json      # 422 already canceled (singular "error" key)
curl -i -X POST "http://localhost:8080/customers.json" -H 'Content-Type: application/json' \
  -d '{"customer":{"reference":"cust_12345","email":"a@b.com"}}'         # 422 duplicate reference
curl -i http://localhost:8080/unknown/route                              # 404 fallback
```

## Logging

Every request and response is logged to the **console** and appended to
`logs/requests-YYYY-MM-DD.log`. Response bodies are truncated in the log at 2,000 characters
(the full body is still returned to the client). The `logs/` folder is created automatically.

## Request validation (strict)

Request **bodies** for the mutating endpoints are validated against the Maxio OpenAPI contract
(`../openAPI/openapi.yaml`) *before* each route's own business logic, by `StrictValidationMiddleware`.
A request that violates the spec is rejected with a spec-shaped `{ "errors": [ ... ] }` `422`, exactly
as the real API would. Scope is deliberately spec-faithful:

- the required wrapper key (`customer` / `subscription` / `usage` / `migration`) must be present and an object;
- fields the spec marks `required` must be present and non-null — only `createCustomer` has any (`first_name`, `last_name`, `email`);
- when present, a field must match its declared JSON type, and `payment_collection_method` must be one of `automatic` / `remittance` / `prepaid` / `invoice`;
- **unknown/extra properties are tolerated** — the spec sets no `additionalProperties: false` (e.g. the Plugin sends a `cancel_at_end_of_period` on cancel that the current spec's `Cancellation-Options` doesn't list; the mock accepts it).

Examples (add `-i` to see the `422`):

```sh
curl -i -X POST http://localhost:8080/subscriptions.json -H 'Content-Type: application/json' -d '{}'
#  -> {"errors":["Subscription: must be provided."]}
curl -i -X POST http://localhost:8080/subscriptions.json -H 'Content-Type: application/json' \
  -d '{"subscription":{"payment_collection_method":"bogus"}}'
#  -> {"errors":["Payment collection method: must be one of: automatic, remittance, prepaid, invoice."]}
curl -i -X POST http://localhost:8080/customers.json -H 'Content-Type: application/json' \
  -d '{"customer":{"email":"a@b.com","last_name":"X"}}'
#  -> {"errors":["First name: cannot be blank."]}
```

## Authentication

The real Maxio API uses HTTP Basic auth (`username` = API key, `password` = `x`). **This mock does
not enforce authentication** — all requests are accepted regardless of the `Authorization` header.
(Request-*body* validation is enforced — see "Request validation" above — but auth is not.)

## Notes / limitations

- Query parameters (`page`, `per_page`, `filter[...]`, date filters, `include`) are accepted but
  **not applied** — the static payloads are returned as-is. (Request *bodies*, however, are validated —
  see "Request validation (strict)".)
- The mock has no real state machine: mutating routes return a patched copy of a canonical canned
  subscription rather than tracking a subscription's state across calls (see "Known (canned)
  identifiers" above for exactly which id transitions succeed).
- Only the routes documented above are implemented; any other path/method returns a `404` fallback.
