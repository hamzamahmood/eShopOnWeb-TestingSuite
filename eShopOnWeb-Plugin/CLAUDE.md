# eShopOnWeb-Plugin — Maxio via APIMatic SDK + plugin seam

Part of the Maxio integration comparison workspace (`../CLAUDE.md` has the big picture: this is the
**Plugin** variant — Maxio called through the APIMatic-generated SDK behind a provider-agnostic
`IBillingClient`, contrasted against the raw-HttpClient `../eShopOnWeb-Direct`). This is its own git
repo. TFM **`net8.0`** (`Directory.Packages.props`; `global.json` SDK `8.0.100`, rollForward latestMajor).

## eShopOnWeb layout

`src/{ApplicationCore, Infrastructure, Web, PublicApi, BlazorAdmin, BlazorShared, SubscriptionsSeed}` +
`tests/{UnitTests, IntegrationTests, FunctionalTests, PublicApiIntegrationTests}`. The billing seam is
`ApplicationCore/Interfaces/IBillingClient.cs`; the SDK impl lives in Infrastructure and **SDK types
never leak above Infrastructure** — callers see only `IBillingClient` and ApplicationCore DTOs.

## The SDK

- Package **`AsadAli.AdvancedBilling.Sdk` `1.0.1`** (APIMatic-generated), referenced by
  `src/Infrastructure/Infrastructure.csproj`.
- Namespace `MaxioAdvancedBilling`; client type `MaxioAdvancedBillingClient`; DI extension
  `AddMaxioAdvancedBillingClient`.
- For usage patterns/gotchas, load the `maxio-sdk:*` skills (getting-started, client-initialization,
  authentication, calling-endpoints, models, error-handling, configuration-resilience, testing) — the
  source shows signatures but not the traps.

## Maxio integration (all under `src/Infrastructure/Services/`)

- **`MaxioBillingClient.cs`** — `IBillingClient` impl; wraps every SDK call and translates
  `SdkException<TError>` → ApplicationCore exceptions.
- **`MaxioPassthroughClient.cs`** — `IMaxioPassthrough` impl; uses the **same** SDK client, re-serializes
  the SDK response model back to JSON on success, or surfaces the SDK exception's raw status + body on
  error (transport failure → synthesized 502).
- **`MaxioRequestLoggingHandler.cs`** — `DelegatingHandler` logging method/path/status (no headers/bodies).

Config: `src/Infrastructure/Configuration/MaxioSettings.cs`. DI: `src/Infrastructure/Dependencies.cs`.

### The three passthrough-tested methods (in `MaxioBillingClient.cs`)

| Method | SDK call | Notes |
|---|---|---|
| `ListPlansAsync` (~L45) | `_client.ProductFamilies.ListProductsForProductFamily(productFamilyId, …, includeArchived:false, …, ct)` | **uses the id** (Direct uses the handle); filters null/archived; `MapPlan`. Catches `SdkException<ListProductsForProductFamilyError>`. |
| `FindCustomerIdAsync` (~L66) | `_client.Customers.ReadCustomerByReference(customerReference, ct)` | returns `Customer.Id` as invariant string; `SdkException<RawError>` + `NotFound` → `null`. (Lookup only — no create, unlike Direct's `EnsureCustomerAsync`.) |
| `ListCustomerSubscriptionsAsync` (~L171) | `_client.Customers.ListCustomerSubscriptions(id, ct)` | `ParseId` → `double`; filters non-null; `MapSubscription`; `SdkException<RawError>` → `WrapRawError`. |

> **`RawError` trap:** read/find/list ops throw `SdkException<RawError>` (not a typed per-op error), and
> `RawError` has **no `TryGet` accessors** — read status/body straight off it. See `maxio-sdk:dotnet-error-handling`.

## Passthrough controller (test harness — not shipping API)

`src/PublicApi/MaxioPassthroughController.cs` — `[ApiController]` + `[AllowAnonymous]`, injects
`IMaxioPassthrough`. Same routes/contract as the Direct variant:
- `GET /api/listplans` → `ListPlansRawAsync`
- `GET /api/customer?reference=` → `LookupCustomerRawAsync`
- `GET /api/subscription?customerId=` → `ListCustomerSubscriptionsRawAsync`

Each returns a `ContentResult` with Maxio's **exact** JSON body + status (no DTO flattening, no remap,
bypasses `ExceptionMiddleware`) — enabling the shared `../MaxioPassthroughApiTests` suite.

## Config & DI (`Dependencies.cs` → `ConfigureMaxioServices`)

- Binds `MaxioSettings` from config section `"Maxio"`.
- Registers `MaxioRequestLoggingHandler` (transient); default `HttpClient` uses
  `HttpClientHandler { AllowAutoRedirect = false }` + the logging handler.
- `services.AddMaxioAdvancedBillingClient(options => …)`:
  - **Base URL**: `options.Server.Production.Us.BaseUrl = "http://localhost:8080"` — points at the local
    **mock**, not real Maxio. The commented-out block shows the intended real US/EU + subdomain wiring.
  - **Auth**: `options.BasicAuth = new BasicAuthCredentials { Username = ApiKey, Password = "x" }` —
    Maxio's API-key-as-Basic-username pattern.
  - **Resilience**: `options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }`.
- DI: `IBillingClient → MaxioBillingClient` (scoped), `IMaxioPassthrough → MaxioPassthroughClient`
  (scoped), `IIdempotencyCache → MemoryIdempotencyCache` (singleton). Called from both Web and PublicApi.

> ⚠️ Base URL is hardcoded to the mock (`http://localhost:8080`). To hit real Maxio, restore the
> commented US/EU environment block and remove the localhost override.

## Contrast with the Direct variant

- Plugin is **stateless**: `SubscriptionService` depends only on `IBillingClient`, `IPublisher`,
  `IIdempotencyCache` — no local DB; every read/write round-trips to the provider. Uses an opaque
  preview-token pattern for plan changes, a `SubscriptionState` enum, and in-memory idempotency. Thinner
  pass-through with fewer local business rules than Direct. Full comparison:
  `../docs/subscription-service-comparison.md`.

## Running for the test harness

Base URL already targets the mock; run the mock (`http://localhost:8080`) and this PublicApi on
`https://localhost:5099`, then run `../MaxioPassthroughApiTests`. See `../CLAUDE.md` → "Running the
harness locally" for the full sequence and required `Maxio:*` config (Plugin resolves `/api/listplans`
via `Maxio:ProductFamilyId=527890`).
