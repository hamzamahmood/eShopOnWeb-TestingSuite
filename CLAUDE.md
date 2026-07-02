# eShopOnWeb — Maxio Integration Comparison Workspace

This folder is **not a single solution**. It is a comparison harness that holds two parallel
implementations of the same Maxio Advanced Billing (formerly Chargify) integration, plus the shared
tooling used to test both against identical canned data.

**Goal:** demonstrate that the **Plugin** integration (APIMatic-generated SDK + a provider-agnostic
plugin seam) is cleaner and more production-ready than the **Direct** integration (hand-rolled
`HttpClient`), and back that claim with tests that run byte-for-byte identically against both.

## Layout

The **workspace root is a single git repository** — one `.git` at the root tracks every subfolder
(`eShopOnWeb-Direct/`, `eShopOnWeb-Plugin/`, `MaxioMockServer/`, etc.); the subfolders do *not* have
their own `.git`. Commit from the root; there are no nested checkouts to manage separately.

| Path | What it is |
|---|---|
| `eShopOnWeb-Direct/` | eShopOnWeb fork; Maxio called via **raw `HttpClient`** (no SDK). `net8.0`. |
| `eShopOnWeb-Plugin/` | eShopOnWeb fork; Maxio called via **APIMatic SDK** (`AsadAli.AdvancedBilling.Sdk`) behind an `IBillingClient` plugin seam. `net8.0`. |
| `MaxioMockServer/` | ASP.NET Core Minimal API mock of the Maxio API. `net10.0`. Listens on `http://localhost:8080`. |
| `MaxioPassthroughApiTests/` | Standalone xUnit black-box HTTP suite. `net9.0`. Runs the **same** tests against either integration. |
| `openAPI/` | `openapi.yaml` (Maxio Advanced Billing spec 3.1.0) + `components/`. Source of truth for response shapes. |
| `docs/` | `subscription-service-comparison.md` — detailed Plugin-vs-Direct architectural comparison. |

> TFMs across the workspace: **net8.0** (Direct + Plugin), **net9.0** (tests), **net10.0** (mock). The
> **.NET 9 and .NET 10 SDKs** are required; the eShop repos target net8.0 but their `global.json` uses
> `rollForward: latestMajor`, so a newer SDK builds them against the installed **.NET 8 runtime** — a
> standalone .NET 8 *SDK* is not needed. Build/run each repo from its own folder.

## The comparison in one table

The test harness exercises three read endpoints. Same public route on both integrations; different
internal implementation:

| # | Endpoint | Public route | Direct method (raw HTTP) | Plugin method (SDK) |
|---|---|---|---|---|
| 1 | List available plans | `GET /api/listplans` | `MaxioBillingClient.ListPlansAsync` | `MaxioBillingClient.ListPlansAsync` |
| 2 | Look up customer by reference | `GET /api/customer?reference={ref}` | `EnsureCustomerAsync` (lookup-then-create) | `FindCustomerIdAsync` (lookup only) |
| 3 | List a customer's subscriptions | `GET /api/subscription?customerId={id}` | `ListCustomerSubscriptionsAsync` | `ListCustomerSubscriptionsAsync` |

The public routes are served by a **passthrough controller** in each PublicApi that returns Maxio's
**exact** JSON body and status code (no DTO flattening, no error remap) — that verbatim passthrough is
what lets one test suite validate both.

## How the pieces connect (end-to-end test flow)

```
MaxioPassthroughApiTests  --HTTP-->  PublicApi (Direct OR Plugin)  --HTTP-->  MaxioMockServer
   (xUnit, net9.0)                     passthrough controller                (canned data, :8080)
   PUBLICAPI_BASEURL                   IMaxioPassthrough impl
   default https://localhost:5099
```

- **Tests never talk to the mock directly.** They hit the PublicApi; the PublicApi calls the mock.
- **Switch which integration is under test** by pointing `PUBLICAPI_BASEURL` at whichever PublicApi is
  running (default `https://localhost:5099`). Set via env var or `MaxioPassthroughApiTests/tests.runsettings`.
- Both integrations currently have their **Maxio base URL hardcoded to `http://localhost:8080`** so they
  hit the mock out of the box:
  - Plugin: `eShopOnWeb-Plugin/src/Infrastructure/Dependencies.cs` → `ConfigureMaxioServices`,
    `options.Server.Production.Us.BaseUrl = "http://localhost:8080"`.
  - Direct: real client `MaxioBillingClient` resolves `https://{Subdomain}.chargify.com` from config, but
    the **passthrough** client `MaxioPassthroughClient` `new HttpClient { BaseAddress = "http://localhost:8080" }`
    is hardcoded (its XML comment claiming "configured identically" is inaccurate — no Basic auth, no
    resilience pipeline; see `eShopOnWeb-Direct/src/Infrastructure/Services/Maxio/MaxioPassthroughClient.cs`).

## Canned data (must stay consistent across mock + tests)

Defined in `MaxioMockServer/MockStore.cs` + `MaxioMockServer/MockData/*.json`; test defaults live in
`MaxioPassthroughApiTests/TestSettings.cs`.

| Concept | Known value → `200` | Unknown → `404` |
|---|---|---|
| Product family | id `527890`, handle `acme-projects` | bare JSON string `"A valid product_family_id is required"` |
| Customer reference | `cust_12345` → customer `id 98765` | `{ "errors": ["Customer not found."] }` |
| Customer subscriptions | `customer_id 98765` → 1 Gold Plan sub | `{ "errors": ["Customer not found."] }` |

Products payload has 2 items: Free (`id 3801242`) and **Gold Plan** (`id 3858146`, handle `gold`,
`price_in_cents 1000`, `interval_unit month`). The flow is internally consistent:
`cust_12345` → `98765` → subscription `id 15100121` (state `active`, product `gold`).

The 5 tests (`MaxioPassthroughApiTests/Tests/`) assert both success payloads **and exact `404`
passthrough** (guarding against the app's old `4xx → 422` remap):
`ListPlansTests` (success only — no request input), `CustomerLookupTests` (success + 404),
`SubscriptionTests` (success + 404).

## Running the harness locally

Three things must be up (run each from its own repo folder, in separate terminals):

```sh
# 1. Mock server (canned Maxio responses on :8080)
cd MaxioMockServer && dotnet run

# 2. One PublicApi — Direct OR Plugin — on https://localhost:5099
cd eShopOnWeb-Direct/src/PublicApi && dotnet run     # ...or eShopOnWeb-Plugin/...

# 3. The tests, pointed at that PublicApi
cd MaxioPassthroughApiTests && dotnet test
#    target a different instance/port:  PUBLICAPI_BASEURL=https://localhost:5099 dotnet test
```

Required PublicApi config under the `Maxio` section for the mock to resolve (see each repo's
`appsettings.Development.json`): `Maxio:ProductFamilyId=527890` **and** `Maxio:ProductFamilyHandle=acme-projects`
(Plugin uses the id, Direct uses the handle), and any non-empty `Maxio:ApiKey` + `Maxio:Subdomain` (mock
enforces no auth). Direct also sets `Maxio:SkipStartupValidation=true` in its
`appsettings.Development.json`; the Plugin does not set it (nor `DefaultProductHandle` /
`MeteredComponentHandle`) and still starts and passes all tests, so that key is Direct-only, not
universally required.

## Where things live (per integration)

Both repos share the eShopOnWeb project layout: `src/{ApplicationCore, Infrastructure, Web, PublicApi,
BlazorAdmin, BlazorShared}` + `tests/`. Maxio-specific code:

**Direct** (`eShopOnWeb-Direct/`):
- `src/Infrastructure/Services/Maxio/MaxioBillingClient.cs` — real client; every method → one Maxio REST
  call via shared `SendAsync<T>` helper. Base URL from `MaxioSettings.ResolveBaseUrl()`.
- `src/Infrastructure/Services/Maxio/` also: `MaxioPassthroughClient.cs`, `MaxioDtos.cs`, `MaxioJson.cs`
  (snake_case opts), `MaxioErrorReader.cs`.
- `src/Infrastructure/MaxioDependencies.cs` — `AddMaxioBillingClient`: typed `HttpClient` + Basic auth
  (`base64("{ApiKey}:x")`) + **resilience pipeline** (`Microsoft.Extensions.Http.Resilience`: retry
  3× on safe methods only, circuit breaker, 10s per-attempt timeout).
- `src/PublicApi/MaxioPassthroughController.cs` — `[AllowAnonymous]` routes, wraps `MaxioRawResponse`
  into a `ContentResult` with provider's exact status.
- Config: `src/Infrastructure/Configuration/MaxioSettings.cs`.

**Plugin** (`eShopOnWeb-Plugin/`):
- SDK: `AsadAli.AdvancedBilling.Sdk` `1.0.1`, namespace `MaxioAdvancedBilling`, client
  `MaxioAdvancedBillingClient`, DI extension `AddMaxioAdvancedBillingClient`. **SDK types never leak
  above Infrastructure** — callers see only `IBillingClient` / DTOs.
- `src/Infrastructure/Services/MaxioBillingClient.cs` — wraps SDK calls
  (`_client.ProductFamilies.ListProductsForProductFamily`, `_client.Customers.ReadCustomerByReference`,
  `_client.Customers.ListCustomerSubscriptions`), translates `SdkException<TError>` to ApplicationCore
  exceptions. Note the `RawError` trap on read/find/list ops (no `TryGet` accessors).
- `src/Infrastructure/Services/`: also `MaxioPassthroughClient.cs`, `MaxioRequestLoggingHandler.cs`
  (`DelegatingHandler`).
- `src/Infrastructure/Dependencies.cs` — `ConfigureMaxioServices`: SDK client via options
  (`BasicAuth { Username=ApiKey, Password="x" }`, `RetryOptions.Default() with { Timeout = 15s }`).
- `src/PublicApi/MaxioPassthroughController.cs` — same routes/contract as Direct.
- Config: `src/Infrastructure/Configuration/MaxioSettings.cs`.

For SDK usage patterns/gotchas there are `maxio-sdk:*` skills available (auth, calling endpoints,
models, error handling, resilience, testing) — load the relevant one when working the Plugin SDK code.

## Adding a 4th+ endpoint (the common next task)

To extend the comparison you must touch **four repos in lockstep**, keeping data consistent:
1. `MaxioMockServer/Program.cs` — add the route; add payload to `MockData/` + known ids to `MockStore.cs`.
2. `eShopOnWeb-Direct/` — add the raw `HttpClient` method + passthrough method + controller route.
3. `eShopOnWeb-Plugin/` — add the SDK-based method + passthrough method + controller route.
4. `MaxioPassthroughApiTests/Tests/` — add success + (where a bad input exists) `404` passthrough tests.
5. Verify the openAPI spec (`openAPI/openapi.yaml`) for the real response shape.

## Gotchas / notes

- Windows + PowerShell primary shell. Paths use `D:\work\eshop-integration\eshopOnWeb\...`.
- Mock ignores auth and all query params (`page`, `filter[...]`, `include`) — returns static payloads.
- Mock logs every request/response to console + `MaxioMockServer/logs/requests-YYYY-MM-DD.log`.
- Tests use `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` (the `curl -k` equivalent)
  for the local dev HTTPS cert; a not-running PublicApi yields a clear assertion, not a raw connection error.
- Passthrough controllers are `[AllowAnonymous]` **test harness** endpoints — not part of the shipping API.
