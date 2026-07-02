# eShopOnWeb-Direct — Maxio via raw HttpClient

Part of the Maxio integration comparison workspace (`../CLAUDE.md` has the big picture: this is the
**Direct** variant — Maxio called with a hand-rolled `HttpClient`, contrasted against the SDK-based
`../eShopOnWeb-Plugin`). This is its own git repo. TFM **`net8.0`** (central `Directory.Packages.props`).

## eShopOnWeb layout

`src/{ApplicationCore, Infrastructure, Web, PublicApi, BlazorAdmin, BlazorShared}` + `tests/{UnitTests,
IntegrationTests, FunctionalTests, PublicApiIntegrationTests}`. The billing seam is the interface
`ApplicationCore/Interfaces/IBillingClient.cs`; the HTTP implementation lives in Infrastructure.

## Maxio integration (all under `src/Infrastructure/Services/Maxio/`)

- **`MaxioBillingClient.cs`** — `IBillingClient` impl; the real integration. Every method maps 1:1 to a
  Maxio REST call through the shared private helper `SendAsync<TResponse>(method, path, body,
  successCodes, ct)` (serializes with `MaxioJson.Options`, maps `HttpRequestException`/timeout →
  `BillingProviderException`, non-success → `ThrowForUnexpectedStatusAsync`).
- **`MaxioPassthroughClient.cs`** — `IMaxioPassthrough` impl; raw GET passthrough returning verbatim
  status + body for the test harness.
- **`MaxioDtos.cs`** (request/response records), **`MaxioJson.cs`** (shared snake_case
  `JsonSerializerOptions`), **`MaxioErrorReader.cs`** (pulls messages from Maxio error bodies).

Config: `src/Infrastructure/Configuration/MaxioSettings.cs`. DI: `src/Infrastructure/MaxioDependencies.cs`.

### The three passthrough-tested methods (in `MaxioBillingClient.cs`)

| Method | Maxio call | Notes |
|---|---|---|
| `ListPlansAsync` (~L37) | `GET product_families/{handle:{ProductFamilyHandle}}/products.json` | family route URL-escaped via `FamilyRoute()`; projects `.Product` → `BillingPlan`. **Uses the handle.** |
| `EnsureCustomerAsync` (~L66) | `GET customers/lookup.json?reference=` then `POST customers.json` | idempotent lookup-then-create; 200→return id, 404→create. Lookup is a manual GET, not via `SendAsync`. |
| `ListCustomerSubscriptionsAsync` (~L130) | `GET customers/{id}/subscriptions.json` | maps each via `ToBillingSubscription`. |

## Passthrough controller (test harness — not shipping API)

`src/PublicApi/MaxioPassthroughController.cs` — `[ApiController]` + `[AllowAnonymous]`, injects
`IMaxioPassthrough`:
- `GET api/listplans` → `ListPlansRawAsync`
- `GET api/customer?reference=` → `LookupCustomerRawAsync` (400 if missing)
- `GET api/subscription?customerId=` → `ListCustomerSubscriptionsRawAsync` (400 if missing)

Each returns a `ContentResult` (`application/json`) carrying Maxio's **exact** status + body — bypasses
DTO flattening and `ExceptionMiddleware`'s status remap. That verbatim passthrough is what lets the
shared `../MaxioPassthroughApiTests` suite assert exact `404`s.

## Config & DI (`MaxioDependencies.cs` → `AddMaxioBillingClient`)

- Binds config section `"Maxio"` with `ValidateDataAnnotations().ValidateOnStart()`.
- Real client: `AddHttpClient<IBillingClient, MaxioBillingClient>(ConfigureMaxioClient).AddMaxioResilience("maxio")`.
  - `ConfigureMaxioClient`: `BaseAddress = settings.ResolveBaseUrl()` (US `https://{Subdomain}.chargify.com`,
    EU `https://{Subdomain}.ebilling.maxio.com`), `Timeout = InfiniteTimeSpan` (resilience owns timing),
    Basic auth = base64(`"{ApiKey}:x"`), Accept `application/json`.
  - **Resilience** (`Microsoft.Extensions.Http.Resilience` 10.7.0): no auto-redirect; retry 3× exp+jitter
    but `DisableForUnsafeHttpMethods()` (GET/HEAD only — no duplicate billing writes); circuit breaker;
    10s **per-attempt** timeout added last.
- Passthrough client: `IMaxioPassthrough → MaxioPassthroughClient`, plain scoped.
- Registered by both hosts: `src/PublicApi/Program.cs:54`, `src/Web/Configuration/ConfigureCoreServices.cs:23`.
- `src/PublicApi/appsettings.Development.json` has a `Maxio` section (ApiKey `test-key`, Subdomain `test`,
  US, ProductFamilyHandle `acme-projects`, DefaultProductHandle `gold`, MeteredComponentHandle `api-calls`,
  `SkipStartupValidation: true`). Real `ApiKey` intended via user-secrets/env, not appsettings.

## ⚠️ Known discrepancy

`MaxioPassthroughClient`'s XML comment claims it is "configured identically (same base URL, Basic auth,
resilience pipeline)" as the real client, but its constructor is `new HttpClient { BaseAddress =
new Uri("http://localhost:8080") }` — hardcoded mock address, **no Basic auth, no resilience**. Fix the
comment or the code before trusting it. (The real `MaxioBillingClient` correctly resolves the base URL
from config.)

## Contrast with the Plugin variant

- Direct is **stateful**: `SubscriptionService` also depends on `IRepository<Subscription/UsageRecord>`,
  persists a local `Subscription` entity, and `SyncFromProvider` after mutations (local int surrogate
  keys vs provider string ids). Adds real business rules (plan-exists checks, payment-method guards,
  durable idempotency via `UsageRecord`). Full comparison: `../docs/subscription-service-comparison.md`.
- No Maxio/Chargify SDK — everything hand-rolled over `HttpClient`. Key packages: `Microsoft.Extensions.Http.Resilience`,
  `System.Net.Http.Json`, `System.Text.Json`, `Microsoft.Extensions.Options.DataAnnotations`.

## Running for the test harness

Point Maxio at the mock (`http://localhost:8080`) and run the PublicApi on `https://localhost:5099`, then
run `../MaxioPassthroughApiTests`. See `../CLAUDE.md` → "Running the harness locally" for the full
three-terminal sequence and required `Maxio:*` config.
