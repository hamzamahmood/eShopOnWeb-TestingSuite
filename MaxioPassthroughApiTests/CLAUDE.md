# MaxioPassthroughApiTests — black-box HTTP suite for both integrations

Part of the Maxio integration comparison workspace (`../CLAUDE.md` has the big picture). This is its own
git repo: a **standalone** xUnit suite (TFM **`net9.0`**) that runs the **same** tests against **either**
the Direct or the Plugin PublicApi, purely over HTTP.

**Standalone by design** (`.csproj`): `IsPackable=false`, `ManagePackageVersionsCentrally=false`, **no
project references** — it lives outside both `eShopOnWeb-Direct.sln` and `eShopOnWeb-Plugin.sln` and talks
to the running PublicApi like `curl -k`. Stack: `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2,
`Microsoft.NET.Test.Sdk` 17.12.0; JSON via framework `System.Text.Json` (no JSON package).

## Why one suite validates both integrations

Both PublicApis expose an identical `/api/*` passthrough controller that returns Maxio's **exact** body +
status code (no DTO flattening, no error remap). So the assertions — specific fields **and exact `404`s** —
hold for both. You never change code to switch targets; you just repoint `PUBLICAPI_BASEURL`.

> **Two test flavors.** The **5 shared tests** assert behavior common to both integrations (they pass
> against Direct *and* Plugin). The **2 differentiator tests** (`Category=Differentiator`, see below)
> instead assert a production-readiness requirement only the Plugin meets — they **pass against the Plugin
> and fail against Direct by design**, and that red result *is* the measurement. Keep them out of the
> "both must be green" default run with `--filter "Category!=Differentiator"` when validating Direct.

## Files

- **`ApiClient.cs`** — sealed wrapper over `HttpClient`. Uses
  `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` (the `curl -k` equivalent for the local
  dev HTTPS cert); `BaseAddress = TestSettings.BaseUrl`, 30s timeout. Only `GetAsync(path)`; on
  `HttpRequestException` throws an `XunitException` pointing to the README + the mock at `:8080` (so a
  not-running server yields a clear assertion, not an opaque crash). Returns an `ApiResponse` record
  (`StatusCode`, raw `Body`, `ContentType`).
- **`TestSettings.cs`** — all config via env vars with mock-matching defaults:

  | Env var | Default | Meaning |
  |---|---|---|
  | `PUBLICAPI_BASEURL` | `https://localhost:5099` | PublicApi under test (trailing slash trimmed) |
  | `KNOWN_CUSTOMER_REFERENCE` | `cust_12345` | reference the mock resolves |
  | `KNOWN_CUSTOMER_ID` | `98765` | customer id with subscriptions |
  | `UNKNOWN_CUSTOMER_REFERENCE` | `no_such_customer_ref` | reference → 404 |
  | `UNKNOWN_CUSTOMER_ID` | `99999999` | well-formed but unknown numeric id |
  | `TRANSIENT_5XX_REFERENCE_PREFIX` | `retry_` | prefix; mock returns 503 then 200 (differentiator) |
  | `RATE_LIMIT_REFERENCE_PREFIX` | `ratelimit_` | prefix; mock returns 429 then 200 (differentiator) |

  `TestSettings.NewTransient5xxReference()` / `NewRateLimitReference()` append a fresh `Guid` nonce to the
  respective prefix so each run's mock attempt-counter starts at zero (order-independent).

- **`tests.runsettings`** — sets `PUBLICAPI_BASEURL` run-wide via `<EnvironmentVariables>`
  (default `https://localhost:5099`; the four customer overrides are present but commented out). In Rider:
  "Use specific .runsettings file". Change this value to target Direct vs Plugin vs a different port.
- **`Tests/`** — the 5 shared tests + the 2 differentiator tests below.

## The 5 shared tests (`Tests/`)

| File | Test | Route | Asserts |
|---|---|---|---|
| `ListPlansTests` | success (only) | `GET /api/listplans` | 200 + `application/json`; JSON array of 2 `{ "product": {...} }`; ids include `3801242`, `3858146`; Gold Plan `id 3858146`, `name "Gold Plan"`, `price_in_cents 1000`, `interval_unit "month"`, `product_family.id 527890` |
| `CustomerLookupTests` | success | `GET /api/customer?reference={known}` | 200; `customer.id 98765`, `reference cust_12345`, `first_name "John"`, `email john.doe@example.com` |
| `CustomerLookupTests` | failure | `?reference={unknown}` | **exact `404`** + `errors` array containing a case-insensitive "not found" |
| `SubscriptionTests` | success | `GET /api/subscription?customerId={known}` | 200; array of 1 `{ "subscription": {...} }`; `id 15100121`, `state "active"`, `customer.id 98765`, `product.handle "gold"`, `product.name "Gold Plan"` |
| `SubscriptionTests` | failure | `?customerId={unknown}` | **exact `404`** + `errors` "not found" (numeric id so both integrations behave identically) |

> `/api/listplans` has no request input, so only a success test is meaningful. The two `404` tests exist
> specifically to prove **exact-status passthrough** of Maxio's `{ "errors": ["Customer not found."] }` —
> guarding against the app's old `4xx → 422` remap.

## The 2 differentiator tests (`Tests/PluginDifferentiatorTests.cs`, `Category=Differentiator`)

These show the **Plugin is more production-ready than Direct**. Both hit the same `GET /api/customer`
route but with a special reference whose **first** mock request fails transiently and whose **retry**
succeeds (see `../MaxioMockServer/CLAUDE.md` → "Comparison-harness transient behaviors"). Seeing a `200`
therefore *proves* the client retried — no need to talk to the mock directly.

| Test | Reference | Plugin (SDK retry) | Direct (no resilience) |
|---|---|---|---|
| `Transient_5xx_is_recovered_…` | `retry_{nonce}` | retries GET on 503 → **200** ✅ | one request → **503** ❌ |
| `Rate_limit_429_is_recovered_…` | `ratelimit_{nonce}` | retries GET on 429 → **200** ✅ | one request → **429** ❌ |

Both assert `200` + `customer.id == 98765`. **Why Plugin wins:** the SDK client is configured once with
`RetryOptions.Default()` (retries idempotent GETs on `408/429/500/502/503/504`, 3×) and the passthrough
*inherits* it; Direct's passthrough (`MaxioPassthroughClient`) is a separate hand-rolled `new HttpClient()`
that never got a resilience pipeline — the exact drift the SDK-behind-a-seam design avoids. Expected result:
**green on Plugin, red on Direct** (that red is the point). Isolated behind `[Trait("Category",
"Differentiator")]` so the shared 5 stay green on both.

## Running

Needs three things up (see `../CLAUDE.md` → "Running the harness locally"): the mock on `:8080`, one
PublicApi (Direct **or** Plugin) on `https://localhost:5099` with its Maxio calls routed to the mock, then:

```sh
dotnet test                                             # all 7 tests; green only against the Plugin
PUBLICAPI_BASEURL=https://localhost:5099 dotnet test    # or target a specific instance/port
dotnet test --filter "Category=Differentiator"          # just the 2 Plugin-vs-Direct differentiators
dotnet test --filter "Category!=Differentiator"         # just the 5 shared tests (green on BOTH)
```

> Validating Direct? Run the **shared** filter (all 5 pass). The full run intentionally shows the 2
> differentiators failing against Direct — that is the measured resilience gap, not a regression.

## Adding tests for a new endpoint

Add a `Tests/*.cs` file with a success case and, where a bad input can be expressed in the request, a
`404` exact-passthrough case. Add any new known/unknown identifiers to `TestSettings.cs` (with defaults
matching `../MaxioMockServer/MockStore.cs`). Then wire the mock route + both integrations — see
`../CLAUDE.md` → "Adding a 4th+ endpoint".
