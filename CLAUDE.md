# eShopOnWeb — Maxio Advanced Billing Integration Workspace

This working directory is **not** a single app. It holds a **paired comparison** of two ways to
integrate the same third-party API (Maxio Advanced Billing, formerly Chargify) into the Microsoft
eShopOnWeb reference app, plus a mock server and a black-box test suite that exercise both.

The user (hamza.mahmood@apimatic.io) works at **APIMatic**, the company that generates SDKs like the
one the Plugin integration uses. This is a demo / case-study pair illustrating the ergonomics of a
**generated SDK vs. hand-rolled raw HTTP** for the same real-world API. Differences between the two
integrations are **intentional signal**, not accidental drift.

## Directory layout

| Path | What it is | TFM |
|---|---|---|
| `eShopOnWeb-Direct/` | Integration built with a hand-rolled typed `HttpClient` — **raw HTTP, no vendor SDK** | PublicApi `net8.0` |
| `eShopOnWeb-Plugin/` | Same feature built against the `AsadAli.AdvancedBilling.Sdk` NuGet package (APIMatic-generated Maxio SDK) | PublicApi `net8.0` |
| `MaxioMockServer/` | Minimal-API mock of the Maxio API (generated from its OpenAPI spec), for local testing without real Maxio | `net10.0` |
| `MaxioPassthroughApiTests/` | Standalone xUnit black-box suite hitting the `/api/maxio` controller of **whichever** integration is running | `net9.0` |
| `docs/maxio-billing-controller-comparison.md` | Endpoint-by-endpoint Direct-vs-Plugin comparison table | — |

Both integration repos are near-identical forks of eShopOnWeb; only the Maxio feature differs.
The two `.sln` files are `eShopOnWeb-Direct/eShopOnWeb.sln` and `eShopOnWeb-Plugin/eShopOnWeb.sln`.
`MaxioMockServer` and `MaxioPassthroughApiTests` are **outside** both solutions (no project references).

---

## Shared architecture (both integrations)

The Maxio feature is layered the same way in both repos:

```
PublicApi/MaxioBilling/MaxioBillingController.cs   route prefix: api/maxio, [AllowAnonymous]
        │  depends only on ↓ (never on the SDK / HttpClient)
ApplicationCore/Interfaces/IBillingClient.cs       provider-agnostic seam
        │  implemented by ↓
Infrastructure/Services/.../MaxioBillingClient.cs  the only place that talks to Maxio
        │  errors bubble as ApplicationCore exceptions ↓
PublicApi/Middleware/ExceptionMiddleware.cs        maps exceptions → HTTP status, surfaces only safe Message
```

- `IBillingClient` is the single provider-agnostic contract. **No Maxio/SDK type ever crosses it** — the
  controller and ApplicationCore have no compile-time dependency on the SDK or wire DTOs.
- `MaxioBillingController` exposes **one HTTP endpoint per `IBillingClient` method** under `api/maxio`.
  It replaced an older raw **passthrough** controller. Neither controller returns Maxio's raw response
  body anymore — both return **flattened, provider-agnostic DTOs**, and errors are remapped by
  `ExceptionMiddleware` instead of passed through with Maxio's exact status/body.
- **Request DTOs deliberately mirror the full Maxio API input** (snake_case `[JsonPropertyName]`,
  envelope wrappers like `{ "customer": {...} }`). But the client forwards only the subset it supports;
  every other documented Maxio field is declared **inert** (typed as `JsonElement?` / marked "NOT
  FORWARDED") and silently ignored. Path params fixed by config (product_family_id, component_id) are
  omitted from routes or accepted-but-ignored.
- There is a `SubscriptionService` (`ISubscriptionService`) sitting above `IBillingClient` for the Web
  app, but **`MaxioBillingController` bypasses it and calls `IBillingClient` directly.**
- Comments in both repos reference a `MaxioPassthroughController` — **no such file exists** (stale comment).

### The 11 endpoints that line up on both integrations

Same route + same underlying Maxio op on both (see `docs/maxio-billing-controller-comparison.md` for the
authoritative table). Direct routes add `:int` route constraints; Plugin does numeric validation in-code.

| Operation | Route (under `api/maxio`) | Underlying Maxio call |
|---|---|---|
| List plans | `GET product-families/{productFamilyId}/products` | `GET /product_families/{id}/products.json` |
| Find-or-create customer | `POST customers` | `GET /customers/lookup.json` → `POST /customers.json` |
| List customer subscriptions | `GET customers/{customerId}/subscriptions` | `GET /customers/{id}/subscriptions.json` |
| Read subscription | `GET subscriptions/{subscriptionId}` | `GET /subscriptions/{id}.json` |
| Pause | `POST subscriptions/{id}/hold` | `POST /subscriptions/{id}/hold.json` |
| Resume | `POST subscriptions/{id}/resume` | `POST /subscriptions/{id}/resume.json` |
| Reactivate | `PUT subscriptions/{id}/reactivate` | `PUT /subscriptions/{id}/reactivate.json` |
| Create subscription | `POST subscriptions` | `POST /subscriptions.json` |
| Commit plan change (immediate) | `POST subscriptions/{id}/migrations` | `POST /subscriptions/{id}/migrations.json` (`preserve_period=true`) |
| Cancel (immediate) | `DELETE subscriptions/{id}` | `DELETE /subscriptions/{id}.json` |
| Record usage | Direct `POST subscriptions/{id}/usages` · Plugin `POST subscriptions/{id}/components/{componentId}/usages` | `POST /subscriptions/{id}/components/{comp}/usages.json` |

### Key divergences (the interesting part)

- **Error status mapping.** A `BillingProviderException` now maps to **422 (Unprocessable Entity) on both**
  integrations (previously Plugin returned 502). Full mapping lives in each repo's
  `PublicApi/Middleware/ExceptionMiddleware.cs`. Direct: `BillingProviderException{4xx}`→422,
  `BillingProviderException{else}`→502, `SubscriptionNotFound`→404, `StalePlanChangePreview`→409,
  `InvalidSubscriptionState`→422, `MeteredComponentMisconfigured`→500, `Duplicate`→409, else 500.
  Plugin: `SubscriptionNotFoundException`→404, `Duplicate`/`IllegalSubscriptionTransition`/`StalePreview`→409,
  `PaymentVerificationRequired`→422, `MeteredComponentMisconfigured`/`BillingProviderException`→**422**, else 500.
  (The Plugin controller's XML summaries and a `[ProducesResponseType(502)]` on `metered-component/verify` still
  say 502 — stale source comments, not the runtime behavior.)
- **Create-subscription success status:** Direct returns **200**, Plugin returns **201 Created**.
- **Response shapes differ.** Plan price is `priceInCents` (Direct) vs `price` in dollars (Plugin).
  Subscription `state` is lowercase snake_case `"active"`/`"on_hold"` (Direct, raw passthrough) vs the SDK
  enum name `"Active"`/`"OnHold"` (Plugin). Subscription id is `providerSubscriptionId` (int, Direct) vs
  `subscriptionId` (string, Plugin). Usage id is `providerUsageId` (long) vs `usageId` (string).
- **`timing` control field (Plugin only).** Plugin unifies several Maxio ops behind one route + a `timing`
  field: migrations `timing:"Immediate"` calls Maxio / `"AtRenewal"` returns a locally-computed zero quote;
  cancel `timing:"Immediate"` vs `"EndOfPeriod"`. Direct uses **dedicated separate routes** for the renewal
  / end-of-period variants (`PUT subscriptions/{id}`, `POST subscriptions/{id}/delayed_cancel`).
- **Metered component verify.** Direct `GET metered-component` → `readComponent` (family-scoped, full data).
  Plugin `GET metered-component/verify` → `FindComponent` (site-wide lookup), returns **204 no body**.
- **Usage summary/balance.** Direct `GET subscriptions/{id}/component-balance` → one call, bare int.
  Plugin `GET subscriptions/{id}/components/{id}/summary` → two calls composited.
- **Customer lookup.** Plugin has a standalone `GET customers/lookup?reference=` (read-only, Plugin-only).
  Direct has no equivalent — lookup only happens inside the composite `POST customers`.
- **Record-usage prerequisite (Direct only).** Direct's `RecordUsageAsync` **first calls `readComponent`**
  (`GET /product_families/{id}/components/{comp}.json`) as a config guard before every usage POST. Plugin
  does not. → the mock's component route must be reachable for Direct usage calls (see mock config below).
- **Transport-error handling differs — a real gap on Plugin.** Direct's client catches `HttpRequestException`
  and timeout `TaskCanceledException` and wraps them into `BillingProviderException` ("provider unavailable").
  **Plugin's client only catches `SdkException<TError>`**, so a transport-level failure (connection refused,
  DNS) is *not* wrapped — it bubbles to the middleware's final `else` and leaks a raw **500** with the internal
  message, contradicting the middleware's "never leak raw exception text" contract. Affects every Plugin
  consumer, not just this controller.

---

## eShopOnWeb-Direct specifics (raw HTTP)

- **Client:** `src/Infrastructure/Services/Maxio/MaxioBillingClient.cs`, namespace
  `Microsoft.eShopWeb.Infrastructure.Services.Maxio`. Ctor: `HttpClient`, `IOptions<MaxioSettings>`, `ILogger`.
  Central helper `SendAsync<TResponse>(method, path, body, successStatusCodes, ct)` builds the request,
  serializes with `MaxioJson.Options`, and deserializes. Handles are URL-encoded as `handle:{...}`.
- **JSON:** `src/Infrastructure/Services/Maxio/MaxioJson.cs` — `SnakeCaseLower` naming, `IgnoreWhenWritingNull`,
  `AllowReadingFromString` numbers.
- **Wire DTOs:** `src/Infrastructure/Services/Maxio/MaxioDtos.cs` — `internal sealed`, one per Maxio schema,
  PascalCase names auto-mapped to snake_case (no `[JsonPropertyName]`).
- **Errors:** `MaxioErrorReader.cs` parses Maxio's three error shapes (`{errors:[...]}`, `{errors:{field:..}}`,
  `{error:...}`). Non-success → `BillingProviderException(safeMessage, providerMessages, statusCode)`
  (`src/ApplicationCore/Exceptions/BillingProviderException.cs`). 5xx → generic "unavailable" message.
- **Return records:** `src/ApplicationCore/Interfaces/BillingModels.cs` — `BillingPlan`, `BillingSubscription`,
  `BillingUsageResult`, `BillingProrationPreview`, enums `PlanChangeTiming`, `CancelTiming`.
- **PublicApi request models:** `src/PublicApi/MaxioBilling/Maxio{Customer,Subscription,Component,Shared}Models.cs`
  (public, full Maxio shape, explicit `[JsonPropertyName]`). Note: PublicApi and Infrastructure both declare
  types named `CreateCustomerRequest`, `CreateSubscriptionRequest`, `PauseRequest` etc. in **different
  namespaces** — distinct types, don't confuse them.
- **DI:** `src/Infrastructure/MaxioDependencies.cs` → `AddMaxioBillingClient(IServiceCollection, IConfiguration)`,
  called from `src/PublicApi/Program.cs`. Registers a **typed** `AddHttpClient<IBillingClient, MaxioBillingClient>`
  with `.AddMaxioResilience("maxio")`. Auth = HTTP **Basic** `Base64("{ApiKey}:x")`. Resilience (Polly): retry 3×
  exp+jitter but `DisableForUnsafeHttpMethods()` (only GET/idempotent retried — **no duplicate billing side
  effects**), circuit breaker, per-attempt timeout 10s; `HttpClient.Timeout = Infinite` (resilience owns it).
- **Settings:** `src/Infrastructure/Configuration/MaxioSettings.cs`, bound from section `"Maxio"` with
  `ValidateDataAnnotations().ValidateOnStart()`. `[Required]`: `ApiKey`, `Subdomain`, `ProductFamilyHandle`,
  `DefaultProductHandle`, `MeteredComponentHandle`. Also `Environment` (US/EU), `BaseUrl` override,
  `ProductFamilyId`, `MeteredComponentId`, **`SkipStartupValidation`** (Direct-only). `Program.cs` runs a
  startup `GetMeteredComponentAsync` validation unless `SkipStartupValidation=true`.
- **Base URL resolution:** `ResolveBaseUrl()` → `BaseUrl` if set, else EU `https://{sub}.ebilling.maxio.com`,
  else `https://{sub}.chargify.com`.

## eShopOnWeb-Plugin specifics (generated SDK)

- **SDK:** `AsadAli.AdvancedBilling.Sdk` **1.0.1** (central `Directory.Packages.props`; assembly/root namespace
  `MaxioAdvancedBilling`). Local nupkg DLL:
  `C:\Users\Hamza Mahmood\.nuget\packages\asadali.advancedbilling.sdk\1.0.1\lib\netstandard2.0\MaxioAdvancedBilling.dll`.
- **Client:** `src/Infrastructure/Services/MaxioBillingClient.cs`, namespace
  `Microsoft.eShopWeb.Infrastructure.Services`. Ctor: `MaxioAdvancedBilling.MaxioAdvancedBillingClient`,
  `IOptions<MaxioSettings>`, `IAppLogger`. Uses sub-controllers `ProductFamilies`, `Customers`,
  `Subscriptions`, `Components`, `SubscriptionComponents`, `SubscriptionProducts`, `SubscriptionStatus`.
  Companion `MaxioRequestLoggingHandler` (DelegatingHandler) logs `Method Path -> Status` only (never
  headers/bodies, so the Basic-auth secret is never captured).
- **Errors:** every SDK call wrapped for `SdkException<TError>`. Two error shapes: typed per-operation
  `{Op}Error` (via `TryGetErrorListResponse1`/`TryGetRawError`) and `SdkException<RawError>` for
  read/list/find/delete ops (**RawError has no TryGet accessors — read `StatusCode`/`ReadAsString()` directly**).
  Translators `WrapError`/`WrapRawError`/`ToSubscribeFailure` produce ApplicationCore exceptions; payment
  keyword in validation msgs → `PaymentVerificationRequiredException`. `CreateCustomer` also catches
  `JsonException` and recovers by re-reading (idempotency). Hardcoded: `CollectionMethod.Remittance`,
  migrations `PreservePeriod=true`, usage → configured `MeteredComponentId`.
- **DI:** `src/Infrastructure/Dependencies.cs` → `ConfigureMaxioServices`. Registers a default `HttpClient`
  (primary handler `AllowAutoRedirect=false` + logging handler) and `services.AddMaxioAdvancedBillingClient(...)`
  (generated DI extension). Options: `Environment = Eu` if `Environment=="EU"` else `Us`; overrides
  `Server.Production.{Us,Eu}.BaseUrl` when `BaseUrl` set; `.Site = Subdomain`;
  `BasicAuth = { Username = ApiKey, Password = "x" }`; `Retry = RetryOptions.Default() with { Timeout = 15s }`.
  Then `AddScoped<IBillingClient, MaxioBillingClient>` and `AddSingleton<IIdempotencyCache, MemoryIdempotencyCache>`.
- **DTOs:** `src/PublicApi/MaxioBilling/MaxioBillingRequests.cs` + `MaxioBillingResponses.cs`
  (`CustomerIdResponse`, `SubscriptionResponse`, `PlanChangeQuoteResponse`; `PlanDto`/`UsageDto`/`UsageSummaryDto`
  are ApplicationCore DTOs returned directly).
- **Settings:** `src/Infrastructure/Configuration/MaxioSettings.cs`. Same section `"Maxio"`. No
  `SkipStartupValidation` on Plugin (passing it is a harmless no-op).

> **Verifying SDK endpoint mappings:** SDK method names describe intent, not always the wire path — e.g.
> `SubscriptionStatus.PauseSubscription()` actually hits `POST /subscriptions/{id}/hold.json`, not `/pause`.
> To get ground truth, extract UTF-16LE string literals from `MaxioAdvancedBilling.dll` (path templates are
> stored as .NET string constants — grep for strings containing `/` and `.json`). Don't trust a subagent's
> inference from method names alone.

### Config values (both repos)

- `src/PublicApi/appsettings.json` has **no** `Maxio` section (intentional). Real credentials come from
  **user-secrets** in dev (`UserSecretsId 5b662463-1efd-4bae-bde4-befe0be3e8ff`), never appsettings.json.
- `src/PublicApi/appsettings.Development.json` has a placeholder `Maxio` block pointing `BaseUrl` at
  `http://localhost:8080` (the mock). *(Plugin's Development file has a trailing comma — technically malformed
  strict JSON but tolerated by the config loader.)*
- `Program.cs` force-loads `appsettings.test.json` then env vars (**env wins**), so `Maxio__*` /
  `UseOnlyInMemoryDatabase` env vars override everything.
- **DB choice:** `UseOnlyInMemoryDatabase=true` → EF Core InMemory (no SQL Server / localdb needed); otherwise
  SQL Server LocalDB `(localdb)\mssqllocaldb`. Default (Development) = real LocalDB.

---

## MaxioMockServer

Tiny ASP.NET Core **Minimal API** (`Program.cs`, no controllers) that stands in for the Maxio API.

- **Run:** `cd MaxioMockServer && dotnet run` → listens on **`http://localhost:8080`** (hard-coded via
  `UseUrls`). Startup log: "Maxio mock server listening on http://localhost:8080".
- **No auth enforced** — `Authorization` header ignored. Only `RequestResponseLoggingMiddleware` (logs each
  request/response to console + daily file `logs/requests-YYYY-MM-DD.log`, body truncated at 2000 chars). No
  error-injection middleware — status codes are chosen inline per route.
- **13 routes** implemented (all return spec-shaped Maxio/Chargify JSON):
  `GET /product_families/{id}/products.json`, `GET /customers/lookup.json`,
  `GET /customers/{id}/subscriptions.json`, `POST /customers.json`, `GET /subscriptions/{id}.json`,
  `POST /subscriptions.json`, `POST /subscriptions/{id}/hold.json`, `POST /subscriptions/{id}/resume.json`,
  `PUT /subscriptions/{id}/reactivate.json`, `POST /subscriptions/{id}/migrations.json`,
  `DELETE /subscriptions/{id}.json`, `POST /subscriptions/{id}/components/{comp}/usages.json`,
  `GET /product_families/{id}/components/{comp}.json`. Everything else → 404 via `MapFallback`.
- **Canned data** (`MockData/*.json`, loaded once by `MockStore.cs`, a singleton). Change ids in `MockStore.cs`,
  payloads in `MockData/*.json`:
  - Product family **527890** (handle `acme-projects`); products handle `gold` (id 3858146, 1000¢) and
    `zero-dollar-product` (id 3801242, 0¢).
  - Customer reference **`cust_12345`** → id **98765**. Fresh reference on `POST /customers.json` → fixed id
    98766; known reference → 422 duplicate.
  - Subscriptions **15100121** (active), **15100210** (on_hold), **15100299** (canceled), and **15100377**
    (state **`assessing`** — a plausible-but-unknown provider state; read-only, backs `StateDriftTests`).
  - Metered component **641814** / handle **`api-calls`** (in family 527890).
- **Stateless mutations:** lifecycle/mutating routes parse a canned template and return a **patched copy**
  (`MockStore.WithState`/`WithProduct`) — the stored template is never mutated. Deterministic, order-independent.
- **Error simulation:** 404 (unknown family/customer/subscription/component, missing `reference`, fallback);
  422 (blank email, duplicate reference, unknown customer/product on create, wrong-state lifecycle action);
  201 on subscription create; cancel of already-canceled 15100299 → 422 with singular `{"error":...}` key.
- **Strict request-body validation** (`Middleware/StrictValidationMiddleware.cs`, runs after logging, before
  routes): validates mutating-endpoint bodies against the OpenAPI contract — required wrapper key + required
  attributes (only `createCustomer` requires any: first_name/last_name/email), JSON types, and the
  `payment_collection_method` enum — returning a spec-shaped `{"errors":[...]}` **422** on violation.
  **Permissive on unknown fields** (spec sets no `additionalProperties:false`; e.g. Plugin's non-spec
  `cancel_at_end_of_period` on cancel is tolerated). Transparent to all valid traffic (both integrations pass).
- **Comparison-harness behaviors:**
  - `race_*` customer reference → concurrent-create race: lookup 404 on attempt 1, `POST /customers.json`
    422 "already taken", subsequent lookup 200. Backs `PluginAdvantageTests` (Plugin recovers, Direct doesn't).
  - **Payment-failure product handles** (`paymentFailureHandles` map on `POST /subscriptions.json`, checked
    before the known-product-handle check) → 422 with card/payment messages (Plugin → typed
    `PaymentVerificationRequiredException`, Direct → generic). Three handles, each message carrying ≥1 Plugin
    keyword: `card-required`, `threeds-required`, `card-declined`. Backs the `PluginAdvantageTests` payment
    `[Theory]`.
  - **`assessing`** subscription state (id 15100377, read route) → Plugin `MapState` returns `Other`, Direct
    forwards the raw string. Backs `StateDriftTests`.
  - `retry_*`/`ratelimit_*` lookup references → 503/429 then success. **NOT a Direct-vs-Plugin differentiator**
    — both integrations retry idempotent GETs (Direct via Polly, Plugin via the SDK's `RetryOptions`), so both
    recover. Backs `RetrySafetyTests` (safety-net: passes on both).

---

## MaxioPassthroughApiTests

Standalone **xUnit** black-box HTTP suite (no project references, not in either solution). It runs against
whichever PublicApi is up (point `PUBLICAPI_BASEURL` at it). The suite has since been **tightened toward
Plugin expectations** — most facts are still green on both, but create-success (201), read-unknown (404), plus
the `PluginAdvantageTests` and the newer advantage tests, now assert **Plugin-specific** behavior and **fail on
Direct** by design. The only mechanical Direct/Plugin knob is `RECORD_USAGE_PATH_TEMPLATE` (route shape).

**Verified live (2026-07-06), both integrations against the mock:** Plugin **39/39 pass** (fully green).
Direct **29/39 pass**, **8 by-design failures**: `CreateSubscriptionTests.Known_customer_and_product_creates_a_subscription`
(201 vs Direct's 200), `ReadSubscriptionTests.Unknown_subscription_yields_an_error_status` (404 vs Direct's
422), and the 6 `PluginAdvantageTests`/`StateDriftTests` advantage cases. Plus **2 skipped** (`CustomerLookupTests`,
both facts — route-divergence auto-skip, no route on Direct). The safety-net additions (`ErrorHygieneTests`,
`RetrySafetyTests`, `ResilientRetryRecoveryTests`) pass on **both**.

`RecordUsageTests.Unknown_subscription_yields_an_error_status` and
`ReactivateSubscriptionTests.Active_subscription_cannot_be_reactivated` were live-verified (both integrations)
to return **422**, not the previously asserted 404 / hedged `{422, 502}` — see
`docs/maxio-error-code-divergence-mock-vs-tests.md`. Both are now pinned to 422 and pass on **both**
integrations; they are no longer part of any by-design-failure set.
> On Git Bash, do **not** pass `RECORD_USAGE_PATH_TEMPLATE=/api/...` via the `VAR=val` prefix — MSYS rewrites
> the leading-slash value into a Windows path and the test builds a `file://` URI. Use PowerShell `$env:` (or
> `MSYS_NO_PATHCONV=1`) for the Direct run.

- **Run:** `dotnet test` (defaults base URL `https://localhost:5099`), or
  `PUBLICAPI_BASEURL=http://localhost:5199 dotnet test` to target a specific instance. `tests.runsettings` is
  optional (for Rider).
- **`TestSettings.cs`** — every value is an env-var override with a default matching the mock's canned data.
  Key ones: `PUBLICAPI_BASEURL`; `LIST_PLANS_PATH` (default `/api/maxio/product-families/527890/products`);
  **`RECORD_USAGE_PATH_TEMPLATE`** — default is the **Plugin** form (`.../components/1/usages`); set it to
  `.../subscriptions/{subscriptionId}/usages` for **Direct** (the one route that genuinely differs).
- **`ApiClient.cs`** — thin `HttpClient` wrapper; trusts the dev HTTPS cert (`DangerousAcceptAnyServerCertificate`,
  i.e. curl `-k`); Get/Post/Put/Delete returning `ApiResponse(StatusCode, Body, ContentType)`. Clear failure
  message if the PublicApi is unreachable. **DELETE builds an explicit request and awaits inside `using`** — an
  earlier bug disposed the request before the send completed and threw spurious `HttpRequestException` on every
  DELETE-with-body; keep it `async`/awaited.
- **`TestJson.cs`** — tolerant readers that bridge the two integrations' differing shapes: `GetSubscriptionId`,
  `GetUsageId`, `GetCustomerId`, and **`StatesEqual`** (strips non-letters + case-insensitive, so Direct's
  `on_hold` matches Plugin's `OnHold`). Use `StatesEqual` for any multi-word state assertion.
- **`Tests/` — 17 files, 39 test cases.** Three groups:
  - **Endpoint suite (11 files, 23 facts)** — one file per endpoint, each with a success case (exact status +
    common flattened fields) and a failure case. The dual **status-set** assertions were **tightened to single
    statuses** after Plugin's middleware moved `BillingProviderException`→422: provider-error failure cases now
    assert exactly **422** (still green on both, since Direct's 4xx-origin `BillingProviderException` is also
    422) — including `ReactivateSubscriptionTests`, whose `{422, 502}` hedge was dropped once live verification
    confirmed 502 is unreachable for that case on either integration. But two cases were narrowed to
    **Plugin-only** expectations and will **fail on Direct**: create-success asserts **201** (Direct returns
    200), and read-unknown-subscription asserts **404** (Direct 422). Files: `ListPlansTests`,
    `FindOrCreateCustomerTests`, `SubscriptionTests`, `ReadSubscriptionTests`, `CreateSubscriptionTests`,
    `PauseSubscriptionTests`, `ResumeSubscriptionTests`, `ReactivateSubscriptionTests`, `CommitPlanChangeTests`,
    `CancelSubscriptionTests`, `RecordUsageTests`. `RecordUsageTests.Unknown_subscription_yields_an_error_status`
    asserts **422**, not 404 — neither integration's record-usage path classifies Maxio's 404 into a typed
    not-found exception the way `ReadSubscriptionAsync` does, so it isn't part of the Plugin-only set either;
    it's green on **both**. (The per-file XML doc comments that documented the old dual-integration design were
    removed with this change.)
  - **`PluginAdvantageTests` (2 facts + 1 `[Theory]` of 3 = 5 cases)** — assert the **Plugin's superior
    behavior**, so they **pass on Plugin and FAIL on Direct by design** (the failure pins what Direct lacks):
    (1) missing subscription → **404** on Plugin vs 422 on Direct; (2) find-or-create **recovers from a
    concurrent-create race** (200) on Plugin vs 422 on Direct; (3) a payment `[Theory]` — every payment-failure
    handle surfaces a **typed** `PaymentVerificationRequiredException` body ("Additional payment information is
    required…") on Plugin — absent on Direct (all 422). Backed by the mock's `race_*` reference and the
    `paymentFailureHandles` map (see MaxioMockServer). Settings: `RACE_REFERENCE_PREFIX` (`race_`),
    `PAYMENT_REQUIRED_PRODUCT_HANDLE` (`card-required`, back-compat), `PAYMENT_REQUIRED_PRODUCT_HANDLES`
    (`card-required,threeds-required,card-declined`).
  - **Newer advantage + safety-net files (5 files):**
    - `CustomerLookupTests` (advantage, 2 facts) — `GET customers/lookup?reference=` known-ref → 200 + id;
      unknown-ref → 404 (`CustomerLookupPath` builder; Plugin-only endpoint). Both facts are **Skipped on
      Direct** (route-divergence auto-skip — no route there — not a fail), and pass on Plugin.
    - `StateDriftTests` (advantage, 1 fact) — unknown provider state `assessing` (id 15100377) → Plugin maps to
      `Other` (**fails on Direct**, raw `assessing`). Setting: `UNKNOWN_STATE_SUBSCRIPTION_ID` (`15100377`).
    - `ErrorHygieneTests` (safety-net, `[Theory]` of 5) — every failure body is clean JSON with no
      internals leaked (forbidden-substring sweep). Passes on **both**.
    - `RetrySafetyTests` (safety-net, 2 facts) — 429/transient-503 on the find-or-create lookup GET recovers
      to 200 (uses `NewRateLimitReference()`/`NewTransient5xxReference()`). Passes on **both**.
    - `ResilientRetryRecoveryTests` (safety-net, 1 fact) — 12 back-to-back find-or-create calls each hitting a
      simulated transport-level connection break on the lookup GET's first attempt; the retry pipeline recovers
      every time. Passes on **both**.

---

## End-to-end run recipe (the actionable bit)

The test suite starts nothing — it assumes processes are already running. To exercise one integration:

**1. Start the mock** (from `MaxioMockServer/`):
```
dotnet run                       # → http://localhost:8080
```

**2. Start exactly ONE PublicApi** — Direct **or** Plugin, not both — in-memory DB, routed at the mock
(from `eShopOnWeb-Direct/` or `eShopOnWeb-Plugin/`):
```
UseOnlyInMemoryDatabase=true ASPNETCORE_URLS=http://localhost:5199 ASPNETCORE_ENVIRONMENT=Development \
  Maxio__BaseUrl=http://localhost:8080 Maxio__Subdomain=acme Maxio__ApiKey=test-api-key \
  Maxio__ProductFamilyId=527890 Maxio__ProductFamilyHandle=acme-projects \
  Maxio__MeteredComponentHandle=api-calls Maxio__MeteredComponentId=641814 \
  Maxio__SkipStartupValidation=true \
  dotnet run --project src/PublicApi --no-launch-profile
```
- `MeteredComponentHandle=api-calls` / `MeteredComponentId=641814` are **required** or Direct's usage endpoint
  404s at its component-verify step.
- `Maxio__SkipStartupValidation=true` matters only for Direct (no-op on Plugin).

**3. Run the suite** against that instance (from `MaxioPassthroughApiTests/`):
```
PUBLICAPI_BASEURL=http://localhost:5199 dotnet test
# For Direct also set the usage route:
PUBLICAPI_BASEURL=http://localhost:5199 \
  RECORD_USAGE_PATH_TEMPLATE=/api/maxio/subscriptions/{subscriptionId}/usages dotnet test
```
To compare both integrations, run steps 2–3 **twice** (once per PublicApi), not simultaneously.

> On Windows/PowerShell the `VAR=value cmd` prefix syntax above is bash-only. Use the Bash tool for these, or
> set env vars with `$env:VAR='value'` before the command in PowerShell.

---

## Gotchas / lessons learned

- **Files get edited concurrently by the user during long sessions.** Treat controller/route files as ground
  truth via a **fresh Read**, not memory of an earlier read in the same conversation. The list-plans route was
  changed under a running session once. Confirm routes with a live curl before trusting any doc.
- **`docs/maxio-billing-controller-comparison.md` can drift** from the code. It's a good map but verify against
  source when it matters.
- **Compile-only verification misses real bugs here.** Booting both apps live against the mock and running the
  suite has caught bugs that compiled fine (the DELETE-with-body race; the `OnHold` vs `on_hold` state-compare).
  Prefer end-to-end verification for anything touching this feature.
- **Plugin leaks transport errors as raw 500** (see divergences above) — if you touch Plugin's client error
  handling, that's the known gap to close (add a `catch` for `HttpRequestException`/timeout → `BillingProviderException`).
