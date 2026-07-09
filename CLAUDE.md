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
| `eShopOnWeb-Direct/` | Integration built with a hand-rolled typed `HttpClient` — **raw HTTP, no vendor SDK** | `net8.0` |
| `eShopOnWeb-Plugin/` | Same feature built against the `AsadAli.AdvancedBilling.Sdk` NuGet package (APIMatic-generated Maxio SDK) | `net8.0` |
| `eShopOnWeb-Direct/src/MaxioBillingTestApi/` | **Standalone Web API host** exposing Direct's `MaxioBillingClient` over `api/maxio` (no DB) | `net8.0` |
| `eShopOnWeb-Plugin/MaxioBillingTestApi/` | **Standalone Web API host** exposing Plugin's `MaxioBillingClient` over `api/maxio` (no DB) — at the **repo root**, not under `src/` | `net8.0` |
| `MaxioMockServer/` | Minimal-API mock of the Maxio API (generated from its OpenAPI spec), for local testing without real Maxio | `net10.0` |
| `MaxioPassthroughApiTests/` | Standalone xUnit black-box suite hitting the `/api/maxio` surface of **whichever** `MaxioBillingTestApi` host is running | `net9.0` |
| `docs/maxio-billing-controller-comparison.md` | Endpoint-by-endpoint Direct-vs-Plugin comparison | — |

Both integration repos are near-identical forks of eShopOnWeb; only the Maxio feature differs.
The two `.sln` files are `eShopOnWeb-Direct/eShopOnWeb.sln` and `eShopOnWeb-Plugin/eShopOnWeb.sln`.
`MaxioMockServer` and `MaxioPassthroughApiTests` are **outside** both solutions (no project references).

> **Structural change (important).** The `api/maxio` HTTP surface no longer lives in
> `PublicApi/MaxioBilling/` — that folder/controller was **removed from both repos**. Each integration now
> exposes its `MaxioBillingClient` from a dedicated standalone **`MaxioBillingTestApi`** host so the
> black-box suite can drive the real client directly. PublicApi still *wires* the billing client (its
> `SubscriptionService` and the `api/subscriptions*` minimal-API endpoints under
> `src/PublicApi/SubscriptionEndpoints/` depend on it) but exposes **no `api/maxio` routes** anymore.

---

## Shared architecture (both integrations)

```
MaxioBillingTestApi/Controllers/MaxioBillingController.cs   route prefix: api/maxio
        │  binds request → forwards to one IBillingClient method → maps exceptions to HTTP in its own catch
ApplicationCore/Interfaces/IBillingClient.cs                provider-agnostic seam
        │  implemented by ↓
Infrastructure/Services/MaxioBillingClient.cs               the only place that talks to Maxio
        │  failures surface as ApplicationCore exceptions (BillingProviderException / BillingConfigurationException)
```

- `IBillingClient` is the single provider-agnostic contract. **No Maxio/SDK type ever crosses it** — the
  controller and ApplicationCore have no compile-time dependency on the SDK or wire DTOs.
- The `MaxioBillingController` in each host exposes **one HTTP endpoint per `IBillingClient` method** under
  `api/maxio`, returns the client's typed result untouched on success, and maps failures to an HTTP status
  **inline in each action's `catch`** — there is **no `ExceptionMiddleware`** in these hosts (it existed only
  in the old PublicApi controller). The two hosts' error-mapping policies differ sharply — see below.
- **Request DTOs mirror the Maxio API input shape** (snake_case `[JsonPropertyName]`, envelope wrappers like
  `{ "customer": {...} }`) but the client forwards only the subset it supports; path params fixed by config
  (`product_family_id`, `component_id`) are accepted-but-ignored. Direct: `Models/MaxioRequests.cs`; Plugin:
  `MaxioRequests.cs`.
- There is a `SubscriptionService` (`ISubscriptionService`) above `IBillingClient` for the Web app and
  PublicApi's `api/subscriptions*` endpoints, but the `MaxioBillingController` **bypasses it and calls
  `IBillingClient` directly.**

### The endpoints (both hosts serve the same ~15 routes under `api/maxio`)

Same route + same underlying Maxio op on both, except where noted. **Route constraints diverge:** Plugin binds
ids as `{id:int}` (a non-numeric id route-misses → empty-body 404 → the suite auto-skips); Direct uses no
constraint (an `int` action parameter → ASP.NET model-binding **400** on a non-numeric id).

| Operation | Route (under `api/maxio`) | Underlying Maxio call |
|---|---|---|
| List plans | `GET product-families/{productFamilyId}/products` | `GET /product_families/{id}/products.json` |
| Find-or-create customer | `POST customers` | `GET /customers/lookup.json` → `POST /customers.json` |
| Customer lookup (read-only) | `GET customers/lookup?reference=` | `GET /customers/lookup.json` |
| List customer subscriptions | `GET customers/{customerId}/subscriptions` | `GET /customers/{id}/subscriptions.json` |
| Read subscription | `GET subscriptions/{subscriptionId}` | `GET /subscriptions/{id}.json` |
| Preview plan change | `POST subscriptions/{id}/migrations/preview` | `POST /subscriptions/{id}/migrations/preview.json` |
| Commit plan change (immediate) | `POST subscriptions/{id}/migrations` | `POST /subscriptions/{id}/migrations.json` (`preserve_period=true`) |
| Pause | `POST subscriptions/{id}/hold` | `POST /subscriptions/{id}/hold.json` |
| Resume | `POST subscriptions/{id}/resume` | `POST /subscriptions/{id}/resume.json` |
| Reactivate | `PUT subscriptions/{id}/reactivate` | `PUT /subscriptions/{id}/reactivate.json` |
| Create subscription | `POST subscriptions` | `POST /subscriptions.json` |
| Cancel (immediate) | `DELETE subscriptions/{id}` | `DELETE /subscriptions/{id}.json` |
| Record usage | `POST subscriptions/{id}/components/{componentId}/usages` | `POST /subscriptions/{id}/components/{comp}/usages.json` |
| Metered-component verify | `GET metered-component/verify` | see divergence below |
| Usage read | Direct `GET subscriptions/{id}/component-balance` · Plugin `GET subscriptions/{id}/components/{componentId}/summary` | see divergence below |

Both `metered-component/verify` and the record-usage route now line up route-for-route (the record-usage
`{componentId}` segment is inert on both — always the server-configured component). **Create-subscription
returns `200 OK` on both** (the old Plugin `201` is gone).

### Key divergences (the interesting part)

- **Error-status mapping — the headline difference.**
  - **Direct** throws a single `BillingProviderException` carrying the origin `int? StatusCode` (+ inner
    exception), and the controller maps it **richly, per Maxio status**, in every action's catch:
    `StatusCode 400`→400, `401/403`→502, `404`→**404**, `422`→**422**, `429`→429, else→502; and when
    `StatusCode` is null (transport/parse) it switches on the inner exception:
    `HttpRequestException`→503, `TaskCanceledException`(timeout)→504, `JsonException`→502, else→404. So a Maxio
    business error (404/422/400) surfaces as the **matching 4xx**.
  - **Plugin** uses a flat central `MapError`: `BillingConfigurationException`→**422**,
    `BillingProviderException`→**502** (regardless of the underlying Maxio status),
    `OperationCanceledException`→499, else→500. So virtually every provider/business error surfaces as **502**.
  - **Consequence:** the passthrough suite's failure cases expect a **4xx** (or a clean 5xx for injected
    upstream faults). Direct's status-preserving mapping satisfies the 4xx cases; Plugin's flat 502 **fails**
    them. Not-found is the one path Plugin gets as 4xx: both clients return `null` on a Maxio 404 for the read
    ops (`GetSubscription`, customer lookup) so those controllers return **404** — but every *other* Plugin op
    on an unknown id becomes a `BillingProviderException`→502.
- **`IBillingClient` shape differs** (same domain types, different method surface — see
  `docs/maxio-billing-controller-comparison.md`):
  - Direct: `ApplyLifecycleActionAsync(id, SubscriptionLifecycleAction, reason)` for pause/resume/cancel/
    reactivate, and `PlanChangeTiming` enum on preview/change; `FindCustomerIdByReferenceAsync`,
    `GetSubscriptionsForCustomerAsync`, `CreateSubscriptionAsync`, `GetMeteredComponentAsync`,
    `GetUsageTotalAsync`.
  - Plugin: separate `PauseAsync`/`ResumeAsync`/`CancelAsync(id, endOfPeriod, reason)`/`ReactivateAsync`, an
    `applyNow` bool on preview/change; `FindCustomerIdAsync`, `ListCustomerSubscriptionsAsync`,
    `SubscribeAsync`, `EnsureMeteredComponentAsync`, `GetPeriodToDateUsageAsync`, plus `FindPlanAsync`.
- **Metered-component verify.** Direct `GET metered-component/verify` → `GetMeteredComponentAsync` returns the
  full `BillingComponent` (**200 + data**). Plugin `GET metered-component/verify` → `EnsureMeteredComponentAsync`
  which is a `void` validation → the controller returns **200 with an empty body** (nothing to describe).
- **Usage read.** Direct `GET .../component-balance` → `GetUsageTotalAsync` → a **bare int**. Plugin
  `GET .../components/{id}/summary` → `GetPeriodToDateUsageAsync` (period-to-date total).
- **Response shapes differ** (both flattened domain DTOs, not Maxio envelopes): Direct tends to snake_case /
  dollars / raw provider `state` string; Plugin camelCase / cents. Body *fields* are compared by the suite's
  AI verifier (by meaning), not by exact key — so shape drift doesn't break the suite, only status does.

---

## eShopOnWeb-Direct specifics (raw HTTP)

- **Standalone host:** `src/MaxioBillingTestApi/` (`Microsoft.NET.Sdk.Web`, net8.0, `ImplicitUsings` **disabled**,
  references `ApplicationCore` + `Infrastructure`). `Program.cs` wires the client **inline** (no DI extension):
  `Configure<MaxioSettings>` + `AddHttpClient<IBillingClient, MaxioBillingClient>(...)` with HTTP **Basic** auth
  `Base64("{ApiKey}:x")` + `Accept: application/json`, BaseAddress from `settings.ResolveBaseUrl()`. No DB, no
  MediatR, no `ISubscriptionService`, **no launchSettings** (default Kestrel `:5000` unless `ASPNETCORE_URLS`).
- **Controller:** `src/MaxioBillingTestApi/Controllers/MaxioBillingController.cs`, `[Route("api/maxio")]`. No
  centralized mapper — each action repeats the same inline `switch (ex.StatusCode)` in its catch (see the
  Direct error-mapping bullet above). Request DTOs in `src/MaxioBillingTestApi/Models/MaxioRequests.cs`.
- **Client:** `src/Infrastructure/Services/MaxioBillingClient.cs`, namespace
  `Microsoft.eShopWeb.Infrastructure.Services`. Ctor: `(HttpClient, IOptions<MaxioSettings>)`. **Flattened
  layout** — just `MaxioBillingClient.cs` (client + an inline error-body reader handling `{errors:[...]}`,
  `{errors:{field:...}}`, bare string) and `MaxioDtos.cs`. **No `Services/Maxio/` subfolder, no separate
  `MaxioJson.cs`/`MaxioErrorReader.cs`.** Central helper `SendAsync`/`TrySendAsync<T>`: transport failures →
  `BillingProviderException` with `StatusCode==null` + inner exception; a Maxio 404 is **not thrown**
  (`TrySendAsync` returns NotFound so `FindCustomerIdByReferenceAsync` can return `null`; ops via `SendAsync`
  get a null-StatusCode "did not return" exception → the controller's null→404 arm); any other non-success →
  `BillingProviderException(msg, (int)statusCode)`.
- **Exceptions:** `src/ApplicationCore/Exceptions/` has exactly `BillingProviderException` (with `int? StatusCode`,
  the **only** billing exception), `DuplicateException`, `BasketNotFoundException`, `EmptyBasketOnCheckoutException`.
  There is **no `BillingConfigurationException`** and no dedicated not-found/validation exception.
- **DI:** there is **no `AddMaxio*` extension**. The `AddHttpClient<IBillingClient, MaxioBillingClient>` block is
  duplicated in three composition roots: `src/Web/Configuration/ConfigureCoreServices.cs` (`AddCoreServices`),
  `src/PublicApi/Program.cs`, and `src/MaxioBillingTestApi/Program.cs`. **No Polly/resilience anywhere** (plain
  `AddHttpClient`, HttpClient default timeout).
- **Settings:** `src/Infrastructure/Configuration/MaxioSettings.cs`, section `"Maxio"`. **No `[Required]`, no
  `ValidateOnStart`, no `SkipStartupValidation` property** (the `SkipStartupValidation` key in the host's
  appsettings.json is dead — binds to nothing). `ResolveBaseUrl()`: explicit `BaseUrl` wins (trimmed), else
  requires `Subdomain` and derives EU `https://{sub}.ebilling.maxio.com` / US `https://{sub}.chargify.com`.
  `IBillingClient`: `src/ApplicationCore/Interfaces/IBillingClient.cs` (domain types in
  `ApplicationCore/Entities/SubscriptionAggregate`).

## eShopOnWeb-Plugin specifics (generated SDK)

- **SDK:** `AsadAli.AdvancedBilling.Sdk` **1.0.2** (central `Directory.Packages.props`; assembly/root namespace
  `MaxioAdvancedBilling`).
- **Standalone host:** `MaxioBillingTestApi/` at the **repo root** (`Microsoft.NET.Sdk.Web`, net8.0, not in the
  solution). `Program.cs`: `AddControllers`, registers `IAppLogger<>`→`LoggerAdapter<>`, calls
  `builder.Services.AddMaxioBillingServices(builder.Configuration)`, `MapControllers`. No DB. No launchSettings
  (default Kestrel `:5000` unless `ASPNETCORE_URLS`).
- **Controller:** `MaxioBillingTestApi/Controllers/MaxioBillingController.cs`, `[Route("api/maxio")]`. Flat
  central `MapError`: `BillingConfigurationException`→422, `BillingProviderException`→502,
  `OperationCanceledException`→499, else→500. `{id:int}` route constraints. Request DTOs in
  `MaxioBillingTestApi/MaxioRequests.cs`.
- **Client:** `src/Infrastructure/Services/MaxioBillingClient.cs`, namespace
  `Microsoft.eShopWeb.Infrastructure.Services`. Ctor: `(HttpClient, IOptions<MaxioSettings>,
  IAppLogger<MaxioBillingClient>)` — it takes the **raw typed HttpClient** and constructs the SDK
  `MaxioAdvancedBillingClient` itself (Basic auth `Username=ApiKey`/`Password="x"`, `Environment` Us/Eu, base
  URL fed from `ResolveBaseUrl()`). Uses SDK sub-controllers (`ProductFamilies`, `Customers`, `Subscriptions`,
  `Components`, …). **Errors:** wraps `SdkException<TError>` (typed per-op) and `SdkException<RawError>`
  (read/list/find/delete — read `StatusCode`/`ReadAsString()` directly, no TryGet); Maxio 404 → `null` for
  reads, → `BillingConfigurationException` in `EnsureMeteredComponentAsync`; everything else →
  `BillingProviderException` via `Fail(...)`. A final `catch (Exception ex) when (Wrappable(ex))` (excludes only
  `OperationCanceled`/`BillingProvider`/`BillingConfiguration`) means **transport errors
  (`HttpRequestException`, timeouts) ARE wrapped** into `BillingProviderException` — the old "Plugin leaks
  transport as raw 500" gap is **closed**.
- **Exceptions:** `src/ApplicationCore/Exceptions/` = `BillingConfigurationException`, `BillingProviderException`,
  `DuplicateException`, plus `BasketNotFoundException`/`EmptyBasketOnCheckoutException`. (The former
  `SubscriptionNotFoundException`, `PaymentVerificationRequiredException`, `IllegalSubscriptionTransition`,
  `StalePreview`, `MeteredComponentMisconfigured` are **gone**.)
- **DI:** `src/Infrastructure/MaxioBillingServiceExtensions.cs` → `AddMaxioBillingServices(IServiceCollection,
  IConfiguration)`: `Configure<MaxioSettings>` + typed `AddHttpClient<IBillingClient, MaxioBillingClient>` +
  `AddScoped<ISubscriptionService, SubscriptionService>`. **No `IIdempotencyCache` (doesn't exist), no Polly, no
  logging DelegatingHandler.** Called from both `src/PublicApi/Program.cs` and the standalone host.
- **Settings:** `src/Infrastructure/Configuration/MaxioSettings.cs`. Same section `"Maxio"`. No `[Required]`, no
  validation, no `SkipStartupValidation`. Code defaults differ from Direct: `ProductFamilyHandle="eshop-subscribe"`,
  `DefaultProductHandle="eshop-pro"`, `AlternateProductHandle="basic-plan"`, `MeteredComponentHandle="api-call"`
  (the host's appsettings.json overrides these to the mock's `acme-projects`/`gold`/`api-calls`).
  `IBillingClient`: `src/ApplicationCore/Interfaces/IBillingClient.cs`.

> **Verifying SDK endpoint mappings:** SDK method names describe intent, not always the wire path — e.g.
> `SubscriptionStatus.PauseSubscription()` actually hits `POST /subscriptions/{id}/hold.json`. To get ground
> truth, extract UTF-16LE string literals from `MaxioAdvancedBilling.dll` (path templates are stored as .NET
> string constants — grep for strings containing `/` and `.json`). Don't trust a subagent's inference from
> method names alone.

### Config values (both hosts)

- Each `MaxioBillingTestApi/appsettings.json` carries a **full `Maxio` section already pointing `BaseUrl` at the
  mock** (`http://localhost:8080`), plus `ApiKey=test-key`, `Subdomain=test`, `ProductFamilyId=527890`,
  `ProductFamilyHandle=acme-projects`, `DefaultProductHandle=gold`, `MeteredComponentHandle=api-calls`,
  `MeteredComponentId=641814`. So the host needs no env vars to run against the mock — just an
  `ASPNETCORE_URLS` to pin the port. (`SkipStartupValidation:true` is present but dead — binds to nothing.)
- The main `src/PublicApi/appsettings.json` still has **no** `Maxio` section (dev credentials come from
  user-secrets `UserSecretsId 5b662463-1efd-4bae-bde4-befe0be3e8ff`); it is not what the suite targets.

---

## MaxioMockServer

Tiny ASP.NET Core **Minimal API** (`Program.cs`, no controllers) that stands in for the Maxio API.

- **Run:** `cd MaxioMockServer && dotnet run` → listens on **`http://localhost:8080`** (hard-coded via
  `UseUrls`). No auth enforced; `RequestResponseLoggingMiddleware` logs each request/response.
- **Spec-shaped routes** for products, customer lookup/create, customer subscriptions, subscription
  read/create, hold/resume/reactivate, migrations (+ preview), delete/cancel, component usage, and family
  component read. Everything else → 404 via `MapFallback`.
- **Canned data** (`MockData/*.json` via `MockStore.cs` singleton; stateless patched-copy mutations):
  - Product family **527890** (handle `acme-projects`); products `gold` (1000¢) and `zero-dollar-product` (0¢).
  - Customer reference **`cust_12345`** → id **98765**; fresh reference on create → fixed id; known reference → 422.
  - Subscriptions **15100121** (active), **15100210** (on_hold), **15100299** (canceled), **15100377**
    (`assessing` — plausible-unknown state), **98700** → empty subscriptions list.
  - Metered component **641814** / handle **`api-calls`**.
- **Strict request-body validation** (`Middleware/StrictValidationMiddleware.cs`): validates mutating bodies
  against the OpenAPI contract (required wrapper key + required attrs; only `createCustomer` requires
  first_name/last_name/email), returning a spec-shaped `{"errors":[...]}` **422**. Permissive on unknown fields.
- **Fault-injection fixtures** the contract-robustness suite relies on (see `MaxioPassthroughApiTests/TestSettings.cs`
  for the authoritative list):
  - **Persistent** faults: subscription ids `59990500`→500, `59990503`→503, `59990429`→429,
    `59990900`→200-with-malformed-body, `59990204`→200-with-empty-body; product handles `server-error-500`,
    `malformed-response`; customer-reference prefixes `fault500_`/`fault503_`/`fault429_`/`malformed_`/`emptybody_`/`objmaperr_`.
  - **Transient** (first attempt fails, retry succeeds): reference prefixes `retry_` (503), `ratelimit_` (429),
    `connbreak_` (transport reset). Not a Direct-vs-Plugin differentiator (both retry idempotent GETs).
  - Payment-failure product handles `card-required`/`threeds-required`/`card-declined` → 422 with card/payment
    messages. `race_`-prefixed reference → concurrent-create race (lookup 404 → create 422 → re-lookup 200).

---

## MaxioPassthroughApiTests

Standalone **xUnit** black-box HTTP suite (no project references, not in either solution). It runs against
whichever `MaxioBillingTestApi` host is up — point `PUBLICAPI_BASEURL` at it. This is the **contract-driven
robustness suite** (OpenAPI-as-truth + mock fault injection), not the older dual-integration suite.

- **How it asserts:** each test checks the **HTTP status deterministically in C#** (`Expect.Status` exact,
  `Expect.StatusInRange(400,500)` any-4xx, `Expect.NotSuccess` any-non-2xx, `Expect.ServerError` any-5xx), then
  sends the response **body** + plain-English rules to an **AI verifier** (`Ai/OpenAIApiService`) that judges by
  meaning (so differing field names/casing across integrations still pass). Safety-net tests also run a
  deterministic `NoInternalLeak` forbidden-substring sweep.
- **Route-divergence auto-skip:** a bare empty-body 404 (route not exposed on this host) → `Skip`, not fail.
  This is how genuinely divergent routes (Direct's `/component-balance` vs Plugin's `/components/{id}/summary`;
  Plugin's `metered-component/verify` vs a `/metered-component`; a non-numeric id under a `{id:int}` constraint)
  surface as **Skipped** rather than a misleading fail.
- **AI verifier:** reads **`AI_API_KEY`** (model `AI_MODEL`, default `gpt-5.5`). Content tests **hard-fail**
  (not skip) if no key is resolvable. Note: the code reads `AI_API_KEY` **only** — the `OPENAI_API_KEY` fallback
  the docs/messages mention is **not implemented** in `TestSettings.AiApiKey`.
- **`Tests/` — ~19 files, ~64 cases.** Categories via `[Trait]`: `endpoint` (per-endpoint success + failure),
  `safety-net` (`ServerFaultTests` persistent 5xx/429, `MalformedResponseTests` malformed/empty body,
  `ErrorHygieneTests` no-leak theory). Success cases assert `200`+AI-verified body; failure cases assert a 4xx
  (or 5xx for injected upstream faults). **There is no `PluginAdvantageTests.cs`** in the current tree (the
  README still describes one — README is ahead of the code).
- **`TestSettings.cs`** — every value is an env override; **default `PUBLICAPI_BASEURL` is `http://localhost:5000`**
  (the README's `5099` is stale). Route-template overrides exist (`RECORD_USAGE_PATH_TEMPLATE`,
  `USAGE_SUMMARY_PATH_TEMPLATE`, `METERED_COMPONENT_PATH`, …) defaulting to Plugin-ish forms — but see the run
  convention below: prefer **not** to set them.
- **`ApiClient.cs`** — thin `HttpClient` wrapper; trusts any dev HTTPS cert; DELETE builds an explicit request
  and awaits inside `using` (fixing an earlier body-disposal race). Clear failure message if the host is down.

### Last verified (this workspace)

Against the **Plugin** `MaxioBillingTestApi` host + mock: **31 pass / 32 fail / 1 skip** (64 total). The 32
fails are dominated by the flat **502-vs-expected-4xx** mapping (23), plus malformed/empty-upstream-body
responses leaking raw `System.Text.Json` parser diagnostics (`BytePositionInLine`, …) through the error
`message` (4), and a few content/functional cases (usage summary 502, empty-bodied metered verify, blank-email/
missing-envelope not validated client-side, a usage-body field mismatch). Direct's status-preserving mapping is
expected to pass most of the 4xx failure cases.

---

## End-to-end run recipe (the actionable bit)

The suite starts nothing — it assumes the mock and one host are already running.

**1. Start the mock** (from `MaxioMockServer/`): `dotnet run` → `http://localhost:8080`.

**2. Start exactly ONE `MaxioBillingTestApi` host** — Direct **or** Plugin, not both. Its appsettings.json
already points at the mock, so only pin the URL:
```
# Direct:  from eShopOnWeb-Direct/   → dotnet run --project src/MaxioBillingTestApi
# Plugin:  from eShopOnWeb-Plugin/   → dotnet run --project MaxioBillingTestApi
ASPNETCORE_URLS=http://localhost:5223 dotnet run --project <the host csproj>
```
(No DB, no Maxio__* env vars needed — the host reads its own appsettings.json.)

**3. Run the suite** (from `MaxioPassthroughApiTests/`):
```
PUBLICAPI_BASEURL=http://localhost:5223 dotnet test
```

### Test-run convention (follow this)

- **Only set `PUBLICAPI_BASEURL`.** Do **not** override the route-template env vars
  (`RECORD_USAGE_PATH_TEMPLATE`, `USAGE_SUMMARY_PATH_TEMPLATE`, `METERED_COMPONENT_PATH`, …). Let routes the
  host doesn't expose **auto-skip** — a skip is the correct, honest signal for route divergence, not something
  to paper over.
- **`AI_API_KEY` is already provided as a system environment variable** — do **not** check for it or pass it.
- On **Git Bash**, do not pass leading-slash values via the `VAR=val cmd` prefix (MSYS rewrites `/api/...` into
  a Windows/`file://` path). Set `PUBLICAPI_BASEURL` in PowerShell `$env:` or run `dotnet test` after exporting
  it — a plain `http://localhost:...` base URL is unaffected, but this is why route overrides in particular
  broke before.

---

## Gotchas / lessons learned

- **Files get edited concurrently by the user during long sessions.** Treat controller/route/client files as
  ground truth via a **fresh Read**, not memory. Confirm routes with a live `curl` before trusting any doc.
- **Two apps can be running on similar ports.** A prior session left a *different* app on `:5199` once; a fresh
  host that fails to bind still lets the stale one answer. Pick a clean port and verify a divergent route (e.g.
  Plugin's `.../components/1/summary`) returns the *expected* shape before trusting the run.
- **`docs/maxio-billing-controller-comparison.md` and the README drift** from the code. Good maps, but verify
  against source when it matters.
- **Compile-only verification misses real bugs here.** Booting a host live against the mock and running the
  suite catches bugs that compiled fine. Prefer end-to-end verification for anything touching this feature.
