# MaxioMockServer — canned Maxio API for local testing

Part of the Maxio integration comparison workspace (`../CLAUDE.md` has the big picture). This is its own
git repo: a tiny ASP.NET Core **Minimal API** (TFM **`net10.0`**, SDK `Microsoft.NET.Sdk.Web`, no NuGet
deps) that mocks the Maxio Advanced Billing (Chargify) API so the Direct and Plugin PublicApis can be
tested offline against fixed data.

**Base URL: `http://localhost:8080`** — fixed authoritatively in `Program.cs:7` (`UseUrls`), which
overrides `Properties/launchSettings.json`.

## Files

- **`Program.cs`** (~72 lines) — Kestrel on `:8080`; `MockStore.Load(...)` once at startup (singleton);
  `RequestResponseLoggingMiddleware` first in the pipeline; route definitions; error-body helpers
  (`ProductFamilyNotFound()` → bare JSON string; `Errors(...)` → `{ "errors": [...] }`).
- **`MockStore.cs`** — sealed; holds the three raw JSON payloads + frozen sets of "known" ids; loaded
  from `MockData/` next to the assembly via `Load(contentRootPath)` (throws `FileNotFoundException` if a
  file is missing).
- **`MockData/*.json`** — `products.json`, `customer.json`, `subscriptions.json` (marked `Content`,
  `CopyToOutputDirectory=PreserveNewest` so they ship next to the build).
- **`Middleware/RequestResponseLoggingMiddleware.cs`** — dependency-free logger; buffers the response
  body, logs `--> {method} {path}` and `<-- {status} … {body}` to console + daily file
  `logs/requests-YYYY-MM-DD.log` (UTC, static `FileLock`); log body truncated at 2,000 chars (full body
  still returned). Purely observational — no auth, no mutation.
- **`appsettings.json`** — logging levels only. **`Properties/launchSettings.json`** — profile URL also
  `:8080` (but `Program.cs` wins).

## Routes (all GET)

| Purpose | Route | 200 when | else |
|---|---|---|---|
| List products (plans) | `GET /product_families/{product_family_id}/products.json` | id ∈ {`527890`, `handle:acme-projects`} (case-insensitive) | 404 bare string `"A valid product_family_id is required"` |
| Look up customer | `GET /customers/lookup.json?reference=...` | `reference=cust_12345` | missing → 404 `"A customer reference is required."`; unknown → 404 `"Customer not found."` |
| List subscriptions | `GET /customers/{customer_id:int}/subscriptions.json` | `customer_id=98765` | other int → 404 `"Customer not found."`; non-int → fallback |
| Fallback | `MapFallback("/{**path}")` | — | 404 `{ "errors": ["The requested resource was not found."] }` |

The `:int` route constraint on subscriptions is deliberate so non-integer ids fall through to the
body-returning fallback (not a bare 404). Products 404 is a **bare JSON string**; customer/subscription/
fallback 404s use the `{ "errors": [...] }` shape.

## Canned data (`MockData/` + `MockStore.cs`)

Internally consistent so a realistic flow works end-to-end:
`lookup cust_12345 → customer id 98765 → its one subscription (id 15100121, active, Gold Plan)`.

- **products.json** — 2 products: Free (`id 3801242`) and **Gold Plan** (`id 3858146`, handle `gold`,
  `price_in_cents 1000`, `interval_unit month`), both in family `527890` / `acme-projects`.
- **customer.json** — `{ "customer": {...} }`: John Doe, reference `cust_12345`, id `98765`, org
  "Acme Corporation".
- **subscriptions.json** — array with one `{ "subscription": {...} }`: id `15100121`, state `active`,
  Gold Plan, nested customer `98765` (`cust_12345`).

Known ids live in `MockStore.cs`: `KnownProductFamilyIds` = {`527890`, `handle:acme-projects`},
`KnownCustomerReferences` = {`cust_12345`}, `KnownCustomerIds` = {`98765`}. **These must stay in sync with
`../MaxioPassthroughApiTests/TestSettings.cs` defaults.**

## Notes / limitations

- **No auth enforced** — all requests accepted regardless of `Authorization` (real Maxio uses Basic auth,
  API key as username, password `x`).
- Query params (`page`, `per_page`, `filter[...]`, dates, `include`) are **accepted but ignored** —
  static payloads returned as-is.
- Only the three routes exist; everything else hits the 404 fallback.
- Response shapes follow `../openAPI/openapi.yaml` (`Product-Response`, `Customer-Response`,
  `Subscription-Response`, `{ "errors": [...] }`).

## Run

```sh
dotnet run   # from this folder; logs "Maxio mock server listening on http://localhost:8080"
```

## Adding a new mocked endpoint

Add the route in `Program.cs`, the payload JSON in `MockData/` (kept as `Content`/`PreserveNewest`), and
the known-id set in `MockStore.cs`. Keep new ids consistent with the test defaults and mirror the real
shape from `../openAPI/openapi.yaml`. Then wire the matching methods in both integration repos and add
tests — see `../CLAUDE.md` → "Adding a 4th+ endpoint".
