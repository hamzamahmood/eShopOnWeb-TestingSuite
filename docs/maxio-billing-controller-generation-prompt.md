# Task: Expose MaxioBillingClient via a standalone Web API microservice, then verify it end-to-end

You are working in the eShopOnWeb + Maxio integration base repo
(`D:\work\eshop-integration\prompt\eShopOnWeb-Maxio`). This repo is the **Plugin (SDK) flavor** of the
integration (it wires Maxio via `ConfigureMaxioServices` / `MaxioRequestLoggingHandler`, and its
`IBillingClient` uses `string` ids and `timing` enums). It has **no `MaxioBillingController` yet** — you
will generate one.

Your job has three phases: (1) generate a controller that exposes the existing `MaxioBillingClient` over
HTTP in a **separate** web project, (2) run the mock server + that web project wired together, and
(3) run the black-box test suite against it and produce a pass/fail report.

Do the phases in order. Do not skip Phase 2/3 — compile-only verification is not sufficient for this
feature; behavior must be confirmed live.

---

## Inputs (read these first, treat as ground truth)

- `@src/Infrastructure/Services/MaxioBillingClient.cs` — the service to expose. Every method maps 1:1 to
  an upstream Maxio Advanced Billing API call.
- `@src/ApplicationCore/Interfaces/IBillingClient.cs` — the provider-agnostic seam it implements. This is
  the **method set** you must expose (one endpoint per method).
- `@src/PublicApi/Middleware/ExceptionMiddleware.cs` and `@src/BlazorShared/Models/ErrorDetails.cs` — the
  existing exception→HTTP-status middleware to reuse (see 1d).
- **`docs/maxio-endpoint-path-mapping.md` — the authoritative routing table.** Use its **Plugin (SDK)
  section**. (See Phase 1 prerequisite: this file must be copied into this repo first.)
- `@MaxioMockServer/` — mock Maxio API (listens on http://localhost:8080).
- `@MaxioPassthroughApiTests/` — xUnit black-box HTTP suite that exercises the controller.

---

## Phase 1 — Generate the controller in a separate Web API project

Create a **new, standalone ASP.NET Core Web API project** (separate from the existing PublicApi) that
hosts a single `MaxioBillingController` exposing `MaxioBillingClient`. Reference the existing
`Infrastructure`, `ApplicationCore`, and `BlazorShared` projects so you reuse the real client and error
types — do not fork or reimplement them.

### Prerequisite — make the route table reachable
The routing source of truth is `docs/maxio-endpoint-path-mapping.md`, but this repo has no `docs/` folder.
**Before generating routes, copy `maxio-endpoint-path-mapping.md` into this repo** (e.g. `./docs/`) so the
reference resolves, then read its **Plugin (SDK) section**.

### 1a. Endpoint mapping (one endpoint per client method, routes from the table)
- Expose **one controller endpoint per `IBillingClient` / `MaxioBillingClient` method** (1:1 mapping).
- Route prefix: `api/maxio`. For **every** route, use the **Plugin (SDK) section** of
  `docs/maxio-endpoint-path-mapping.md` as the source of truth — copy the "Exposed controller route"
  column verbatim (HTTP verb + path). Do **not** invent routes or hand-declare `[Http*]` attributes that
  disagree with the table.
- Each method maps to exactly **one** row in the Plugin section; wire only the rows whose method exists in
  this repo's `IBillingClient`.
- **Design intent (why the table, not your own routing):** operations that exist in *both* integrations
  (Direct and Plugin) are given *identical* routes by design, so the shared test suite reaches the same
  paths deterministically regardless of integration. A handful of operations are *integration-specific*
  (they only appear in one flavor's section, e.g. Plugin's `metered-component/verify`, `.../summary`,
  `customers/lookup`) — that is expected, not drift.

### 1b. Input contract (passthrough shaped to the Maxio API, not the client)
- Each endpoint takes a **dedicated request DTO** whose shape mirrors the **corresponding Maxio API
  operation's input** (route params, query params, request body / envelope wrappers, snake_case field
  names) — NOT merely whatever the current `MaxioBillingClient` method signature accepts. This keeps the
  external contract stable for microservice callers.
- Map each DTO onto the `MaxioBillingClient` method's parameters for the fields the client actually
  supports. Fields present in the Maxio API but not consumed by the client may be accepted and ignored,
  but must not break binding.

### 1c. Configuration (point the client at the mock)
- Add a **separate Maxio configuration** for this project's `MaxioBillingClient`. Use the block below, and
  ensure `BaseUrl` overrides the client to target the mock at http://localhost:8080:

  ```json
  "Maxio": {
    "ApiKey": "test-key",
    "Subdomain": "test",
    "Environment": "US",
    "BaseUrl": "http://localhost:8080",
    "ProductFamilyId": 527890,
    "ProductFamilyHandle": "acme-projects",
    "DefaultProductHandle": "gold",
    "MeteredComponentHandle": "api-calls",
    "MeteredComponentId": 641814
  }
  ```
- Wire the Maxio services with the existing `ConfigureMaxioServices` extension. (`MeteredComponentId=641814`
  is required or the usage endpoint 404s at its component-verify step.)

### 1d. Error handling (reuse the PublicApi exception middleware)
- **Reuse / replicate `src/PublicApi/Middleware/ExceptionMiddleware.cs`** and its `ErrorDetails` type
  (`src/BlazorShared/Models/ErrorDetails.cs` — reference `BlazorShared` or copy the type). Do **not**
  hand-author a new mapping table.
- Register it **first** in the pipeline (`app.UseMiddleware<ExceptionMiddleware>()` before routing /
  controllers) so ApplicationCore exceptions (`SubscriptionNotFoundException`, `BillingProviderException`,
  etc.) map to proper HTTP statuses instead of leaking raw 500s.
- **One required alignment:** ensure `BillingProviderException` maps to **422 (Unprocessable Entity)**.
  This repo's copy of the middleware currently returns **502** for it; the tightened test suite and the
  working integrations expect **422**. Change only that branch — keep every other branch as the existing
  middleware defines it.

**Phase 1 exit criteria:** the new project builds, and every method in this repo's `IBillingClient`
resolves to exactly one controller action whose route matches the Plugin section of the table.

---

## Phase 2 — Run the mock server + the new web project, wired together

1. Start the mock server from `@MaxioMockServer/` and confirm it logs
   "listening on http://localhost:8080".
2. Start the **new web project** on a fixed URL (e.g. http://localhost:5199), pointed at the mock via the
   Maxio config above.
   - This standalone microservice only needs the **Maxio services** wired (`ConfigureMaxioServices` + the
     reused `ExceptionMiddleware`) plus the Maxio config. It does **not** need the eShop catalog/identity
     database — do not wire EF/LocalDB or go hunting for eShop DB config.
   - On Windows/PowerShell, `VAR=value cmd` prefixes are bash-only — set env vars with `$env:VAR='value'`
     first, or use the Bash tool.
3. Smoke-test with a live request (e.g. `curl` the list-plans route) before proceeding, to confirm the
   controller reaches the mock.

**Phase 2 exit criteria:** both processes are up and a real HTTP call through the controller returns mock
data.

---

## Phase 3 — Run the test suite and generate a report

1. From `@MaxioPassthroughApiTests/`, run `dotnet test` with `PUBLICAPI_BASEURL` pointed at the running
   web project. Keep `RECORD_USAGE_PATH_TEMPLATE` at its **default** (the Plugin form,
   `/api/maxio/subscriptions/{subscriptionId}/components/1/usages`) — do **not** override it for this repo.

2. **Skip rule (do not let it mask route bugs):**
   - A test may be recorded **"skipped — not found"** *only if* its route is **absent from the Plugin
     section** of `docs/maxio-endpoint-path-mapping.md` (a genuinely non-exposed endpoint).
   - A 404 / route-not-found on a route the Plugin section **lists** is a **FAILURE** (a generation bug),
     never a skip.
   - For every skip, the report must show the route and the table-membership check that justifies it.

3. **Expected results (Plugin base repo) — use to separate real defects from designed behavior:**
   - The suite is tightened to **Plugin** expectations; against this repo expect **~37/38 passing**.
   - Known non-defect: `RecordUsageTests.Unknown_subscription` asserts 404 but Plugin returns 422 — list it
     as an *expected* failure, not a regression.
   - *(Aside, N/A to this repo:)* a Direct-flavored repo would fail 6 facts by design and would need the
     knob set to `/api/maxio/subscriptions/{subscriptionId}/usages`.

4. Produce a **report** containing:
   - Total passed / failed / skipped counts.
   - A table of each test → status (Passed / Failed / Skipped-NotFound).
   - For each failure: the assertion that failed and actual vs. expected behavior, and whether it is an
     *expected* failure (per step 3) or a real defect.
   - For each skip: the route + table-membership justification (per step 2).
   - A short summary of which Maxio operations are correctly exposed and any gaps.

**Deliverable:** the generated report (markdown), plus a one-line statement of whether the controller
correctly fronts `MaxioBillingClient` as a microservice.
