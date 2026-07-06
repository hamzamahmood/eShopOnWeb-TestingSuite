# Task: Expose MaxioBillingClient via a standalone Web API microservice, then verify it end-to-end

You are working in an eShopOnWeb + Maxio integration base repo. This repo contains a **working
`MaxioBillingClient`** (an implementation of the provider-agnostic `IBillingClient` seam) but has **no
`MaxioBillingController` yet** — you will generate one.

**Do not assume the shape of this repo.** Integration bases differ: the id types, the exact `IBillingClient`
method set, whether plan-change/cancel take a `timing` argument or use dedicated methods, how the client
communicates with Maxio, the name/signature of the Maxio DI registration method, and how the client resolves
its outbound base URL **all vary between bases**. Discover every one of these by **reading the files** listed under Inputs — never hard-code an assumption
about which flavor this is.

Your job has three phases: (1) generate a controller that exposes the existing `MaxioBillingClient` over
HTTP in a **separate** web project, (2) run the mock server + that web project wired together, and
(3) run the black-box test suite against it and produce a pass/fail/skip report.

Do the phases in order. Do not skip Phase 2/3 — compile-only verification is not sufficient for this
feature; behavior must be confirmed live.

---

## Inputs (read these first, treat as ground truth)

- **`src/Infrastructure/Services/**/MaxioBillingClient.cs`** — the service to expose. It lives under
  `src/Infrastructure/Services/` but may be inside a subfolder; **locate the file** rather than assuming an
  exact path. Every method maps 1:1 to an upstream Maxio Advanced Billing API call — read the method bodies
  to learn which upstream endpoint each one actually calls.
- **`src/ApplicationCore/Interfaces/IBillingClient.cs`** — the provider-agnostic seam it implements. This is
  the **method set** you must expose (one endpoint per method). Read it to learn the exact methods, their
  parameters, and id types in THIS repo.
- **`src/PublicApi/Middleware/ExceptionMiddleware.cs`** and **`src/BlazorShared/Models/ErrorDetails.cs`** —
  the existing exception→HTTP-status middleware to reuse (see 1d).
- **The routing table** — `docs/maxio-billing-service-route-map.md`. This is the authoritative source of
  truth for controller routes (see the Phase 1 prerequisite for its exact shape and how to read it).
- **`MaxioMockServer/`** — mock Maxio API (listens on http://localhost:8080).
- **`MaxioPassthroughApiTests/`** — xUnit black-box HTTP suite that exercises the controller.

> **Note on layout:** `docs/`, `MaxioMockServer/`, and `MaxioPassthroughApiTests/` live **beside** this repo
> (in the parent folder), not inside it. Resolve them at their real location.

---

## Phase 1 — Generate the controller in a separate Web API project

Create a **new, standalone ASP.NET Core Web API project** (separate from the existing PublicApi) that
hosts a single `MaxioBillingController` exposing `MaxioBillingClient`. Reference the existing
`Infrastructure`, `ApplicationCore`, and `BlazorShared` projects so you reuse the real client and error
types — do not fork or reimplement them.

### Prerequisite — read the route table correctly
The routing source of truth is `docs/maxio-billing-service-route-map.md`. Read it before generating routes.
Its actual shape:

- It is a **single table** with two columns: **`Maxio endpoint route`** and **`Controller endpoint route`**.
- Its rows are a **union across multiple integration variants** — it contains rows for operations that only
  some bases have, and sometimes **more than one alternative controller route for the same goal** (e.g. two
  different usage routes, or a dedicated route vs. a single route that takes a `timing` argument). This is
  expected; it is not drift.
- Some rows carry `†`/`+` annotation marks. **Ignore those marks** — they are not part of the route.

### 1a. Endpoint mapping (one endpoint per client method, routes from the table)
- Expose **one controller endpoint per `IBillingClient` / `MaxioBillingClient` method** (1:1 mapping).
- Route prefix: `api/maxio`. Derive every route from `docs/maxio-billing-service-route-map.md` using this
  selection rule:
  1. For each method on **THIS repo's** `IBillingClient`, read the client to see which upstream Maxio
     endpoint(s) that method actually calls.
  2. Find the table row whose `Maxio endpoint route` matches that upstream call, and expose its
     `Controller endpoint route` **verbatim** (HTTP verb + path).
  3. **Wire only rows that correspond to a method present in this repo.** Rows for operations this repo's
     `IBillingClient` does not have are simply not exposed.
  4. When more than one row could serve a single method, prefer the route that most fully mirrors the
     underlying Maxio API operation (consistent with 1b). Two known cases:
     a. **Component-scoped operation** — the Maxio path includes a component id → expose the route that
        carries `{componentId}`, treated as an **ignored path param**. Keep it even if this client fixes the
        component from configuration instead of taking it as an argument.
     b. **Immediate/deferred variant unified behind one call** (e.g. a `timing` argument) → expose the
        **immediate-variant** route (`POST .../migrations`, `DELETE subscriptions/{id}`), not the deferred one
        (`PUT subscriptions/{id}`, `POST .../delayed_cancel`).
     c. **Neither applies** → pick the route whose path params the method's params can all supply.
- Do **not** invent routes or hand-declare `[Http*]` attributes that disagree with the table.
- **Design intent (why the table, not your own routing):** operations shared by multiple bases are given
  *identical* routes by design, so the shared test suite reaches the same paths deterministically. A handful
  of operations are base-specific (they appear only for some variants) — exposing only what this repo's
  method set supports is correct, not a gap.

### 1b. Input contract (passthrough shaped to the Maxio API, not the client)
- Each endpoint takes a **dedicated request DTO** whose shape mirrors the **corresponding Maxio API
  operation's input** (route params, query params, request body / envelope wrappers, snake_case field
  names) — NOT merely whatever the current `MaxioBillingClient` method signature accepts. This keeps the
  external contract stable for microservice callers.
- Map each DTO onto the `MaxioBillingClient` method's parameters for the fields the client actually
  supports. Fields present in the Maxio API but not consumed by the client may be accepted and ignored,
  but must not break binding.

### 1c. Configuration and wiring (point the service at the mock)
- Add a **separate Maxio configuration** for this project. Start from the block below:

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
    "MeteredComponentId": 641814,
    "SkipStartupValidation": true
  }
  ```

- **Add any required configuration that is missing.** Some bases validate settings at startup (e.g.
  `AddOptions<MaxioSettings>().ValidateDataAnnotations().ValidateOnStart()` with `[Required]` fields) and
  will **fail to boot** if a required key is absent. If the app fails to start citing a missing/invalid
  setting, add the missing key(s) to the `Maxio` section with mock-compatible values. `SkipStartupValidation`
  is included above because some bases make a real outbound Maxio call at startup unless it is set; it is a
  harmless no-op on bases that don't have it.

- **Wire the Maxio services using the repo's existing registration method — do not reimplement it.** Locate
  it in `Infrastructure` (its **name, signature, and argument order vary** between bases — it may be an
  `IServiceCollection` extension method, or a plain static method taking `(IConfiguration, IServiceCollection)`,
  and it may return `void`). **Call it exactly as defined.**

- **After wiring, register any additional services the client needs that the registration didn't provide.**
  Read the `MaxioBillingClient` constructor and satisfy every dependency (for example: a custom logger
  adapter such as `IAppLogger<T>`, an idempotency cache, `AddMemoryCache()`, or a logging `DelegatingHandler`).
  This standalone microservice needs **only the Maxio services** (plus the reused `ExceptionMiddleware`) — it
  does **not** need the eShop catalog/identity database; do not wire EF/LocalDB.

- **Route all outbound Maxio traffic to the mock (this is not automatic).** A base may derive its Maxio host
  from `Subdomain`/`Environment` or from a preconfigured server template, and may **ignore a `BaseUrl` setting entirely**
  — in which case the `BaseUrl` value above does nothing on its own. Determine how THIS repo resolves its
  outbound base URL, then ensure every outbound Maxio call from this service targets
  **http://localhost:8080**. Prefer to do this in the **new project's composition root** (e.g. override the
  registered client's base address / server configuration after calling the repo's registration method); do **not**
  modify the shared `Infrastructure` project unless redirection is genuinely impossible any other way. You
  will **prove** the redirect works with the Phase 2 smoke test before continuing.

### 1d. Error handling (reuse the PublicApi exception middleware)
- **Reuse / replicate `src/PublicApi/Middleware/ExceptionMiddleware.cs`** and its `ErrorDetails` type
  (`src/BlazorShared/Models/ErrorDetails.cs` — reference `BlazorShared` or copy the type). Do **not**
  hand-author a new mapping table.
- Register it **first** in the pipeline (`app.UseMiddleware<ExceptionMiddleware>()` before routing /
  controllers) so ApplicationCore exceptions (`SubscriptionNotFoundException`, `BillingProviderException`,
  etc.) map to proper HTTP statuses instead of leaking raw 500s.
- **One required alignment:** ensure `BillingProviderException` results in **422 (Unprocessable Entity)**.
  Inspect the existing mapping first: if it already yields 422 for the relevant provider errors, leave it.
  If it maps `BillingProviderException` to another status (e.g. 502), change **only** that mapping to 422
  and preserve every other branch. In particular, do **not** change how `MeteredComponentMisconfiguredException`
  (or any other exception type) maps, even if it currently shares a branch with `BillingProviderException` —
  split the branch if needed.

**Phase 1 exit criteria:** the new project builds, and every method in this repo's `IBillingClient`
resolves to exactly one controller action whose route matches the corresponding row in the table.

---

## Phase 2 — Run the mock server + the new web project, wired together

1. Start the mock server from `MaxioMockServer/` and confirm it logs
   "listening on http://localhost:8080".
2. Start the **new web project** on a fixed URL (e.g. http://localhost:5199), pointed at the mock.
   - On Windows/PowerShell, `VAR=value cmd` prefixes are bash-only — set env vars with `$env:VAR='value'`
     first, or use the Bash tool.
3. **Smoke-test with a live request** (e.g. `curl` the list-plans route) before proceeding. This is the
   **gate for the base-URL redirect from 1c**: the response must contain **mock data** (the mock's canned
   plans), not a connection error or a call that escaped to a real Maxio host. If it does not return mock
   data, the outbound redirect is wrong — fix it before Phase 3.

**Phase 2 exit criteria:** both processes are up and a real HTTP call through the controller returns mock
data.

---

## Phase 3 — Run the test suite and generate a report

1. From `MaxioPassthroughApiTests/`, run `dotnet test` with `PUBLICAPI_BASEURL` pointed at the running
   web project. **Leave `RECORD_USAGE_PATH_TEMPLATE` at its default** — do not override it.

2. **Classify every test by static route mapping first, then by result.** Decide skip-vs-run **before**
   interpreting any status code (a routing 404 for a non-exposed route must never be mistaken for a
   business 404 "pass"):
   - Build the set of routes THIS controller exposes (from 1a). For each test, compute the route it targets
     from the suite's `TestSettings` defaults.
   - **Skipped — RouteDivergence:** the test's target route is **not among the exposed routes** — because
     this repo has no `IBillingClient` method for it, or this repo achieves the same goal via a **different
     route** (for example, a test whose path this repo simply does not expose, or a record-usage test whose
     `RECORD_USAGE_PATH_TEMPLATE` does not match the route this repo generated). A missing/different route or
     method is **never** a failure. For every skip, the report must show the route and the exposed-route
     membership check that justifies it.
   - **Passed / Failed:** the route **is** exposed → run the test and report its own verdict honestly. A
     status/shape mismatch on a route that this repo **does** expose is a **Failure** — do not relabel it a
     skip or an "expected" result. (Behavioral differences between integrations on a shared route — e.g. a
     create returning 200 vs 201, or an unknown-id read returning 422 vs 404 — surface here as real failures;
     that is intended.)

3. **Do not assume a target pass rate or pre-label any test.** Different bases legitimately produce different
   pass/fail counts on shared routes; report the **actual** results and let the numbers stand.

4. Produce a **report** (markdown) containing:
   - Total **Passed / Failed / Skipped-RouteDivergence** counts.
   - A table of each test → status (Passed / Failed / Skipped-RouteDivergence).
   - For each **failure**: the assertion that failed, actual vs. expected behavior, and a one-line note on
     whether it looks like a **generation defect** (the controller is wired wrong) or a **behavioral
     divergence** of this integration on a shared route.
   - For each **skip**: the route + the membership check that justifies it (per step 2).
   - A short summary of which Maxio operations this repo correctly exposes and any genuine gaps.

**Deliverable:** the generated report (markdown), plus a one-line statement of whether the controller
correctly fronts `MaxioBillingClient` as a microservice.
