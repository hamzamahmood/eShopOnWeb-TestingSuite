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

- **`tests.runsettings`** — sets `PUBLICAPI_BASEURL` run-wide via `<EnvironmentVariables>`
  (default `https://localhost:5099`; the four customer overrides are present but commented out). In Rider:
  "Use specific .runsettings file". Change this value to target Direct vs Plugin vs a different port.
- **`Tests/`** — the 5 tests below.

## The 5 tests (`Tests/`)

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

## Running

Needs three things up (see `../CLAUDE.md` → "Running the harness locally"): the mock on `:8080`, one
PublicApi (Direct **or** Plugin) on `https://localhost:5099` with its Maxio calls routed to the mock, then:

```sh
dotnet test                                        # default https://localhost:5099
PUBLICAPI_BASEURL=https://localhost:5099 dotnet test   # or target a specific instance/port
```

## Adding tests for a new endpoint

Add a `Tests/*.cs` file with a success case and, where a bad input can be expressed in the request, a
`404` exact-passthrough case. Add any new known/unknown identifiers to `TestSettings.cs` (with defaults
matching `../MaxioMockServer/MockStore.cs`). Then wire the mock route + both integrations — see
`../CLAUDE.md` → "Adding a 4th+ endpoint".
