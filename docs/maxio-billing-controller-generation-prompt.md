# Task: Expose MaxioBillingClient via a standalone Web API microservice, then verify it end-to-end

You are working in the eShopOnWeb + Maxio integration base repo. Your job has three phases:
(1) generate a controller that exposes the existing `MaxioBillingClient` over HTTP in a
**separate** web project, (2) run the mock server + that web project wired together, and
(3) run the black-box test suite against it and produce a pass/fail report.

Do the phases in order. Do not skip Phase 2/3 — compile-only verification is not sufficient
for this feature; behavior must be confirmed live.

---

## Inputs (read these first, treat as ground truth)

- `@src/Infrastructure/Services/MaxioBillingClient.cs` — the service to expose. Every method
  maps 1:1 to an upstream Maxio Advanced Billing API call.
- `@src/ApplicationCore/Interfaces/IBillingClient.cs` — the provider-agnostic seam it implements.
- `@docs/maxio-billing-service-route-map.md` — the **authoritative routing table**. Each row
  pairs an upstream Maxio endpoint with the exact controller route to generate.
- `@MaxioMockServer/` — mock Maxio API (listens on http://localhost:8080).
- `@MaxioPassthroughApiTests/` — xUnit black-box HTTP suite that exercises the controller.

---

## Phase 1 — Generate the controller in a separate Web API project

Create a **new, standalone ASP.NET Core Web API project** (separate from the existing PublicApi)
that hosts a single `MaxioBillingController` exposing `MaxioBillingClient`. Reference the existing
Infrastructure/ApplicationCore projects so you reuse the real client — do not fork or reimplement it.

### 1a. Endpoint mapping (one endpoint per client method)
- Expose **one controller endpoint per `MaxioBillingClient` method** (1:1 mapping).
- Route prefix: `api/maxio`. For **every** route, use `@docs/maxio-billing-service-route-map.md`
  as the source of truth — copy the "Controller endpoint route" column verbatim (HTTP verb + path).
  Do **not** invent routes or hand-declare `[Http*]` attributes that disagree with the table.

### 1b. Input contract (passthrough shaped to the Maxio API, not the client)
- Each endpoint takes a **dedicated request DTO** whose shape mirrors the **corresponding Maxio
  API operation's input** (route params, query params, request body / envelope wrappers,
  snake_case field names) — NOT merely whatever the current `MaxioBillingClient` method signature
  accepts. This keeps the external contract stable for microservice callers.
- Map each DTO onto the `MaxioBillingClient` method's parameters for the fields the client actually
  supports. Fields present in the Maxio API but not consumed by the client may be accepted and
  ignored, but must not break binding.

### 1c. Configuration (point the client at the mock)
- Add a **separate Maxio configuration** for this project's `MaxioBillingClient`. Use the block
  below, and ensure `BaseUrl` overrides the client to target the mock at http://localhost:8080:

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
- Register `MaxioBillingClient` / `IBillingClient` and its typed HttpClient in this project's DI.

**Phase 1 exit criteria:** the new project builds, and every route in the route-map table resolves
to exactly one controller action.

---

## Phase 2 — Run the mock server + the new web project, wired together

1. Start the mock server from `@MaxioMockServer/` and confirm it logs
   "listening on http://localhost:8080".
2. Start the **new web project** with the in-memory DB and the Maxio config above, on a fixed URL
   (e.g. http://localhost:5199), pointed at the mock.
   - On Windows/PowerShell, `VAR=value cmd` prefixes are bash-only — set env vars with
     `$env:VAR='value'` first, or use the Bash tool.
3. Smoke-test with a live request (e.g. `curl` the list-plans route) before proceeding, to confirm
   the controller reaches the mock.

**Phase 2 exit criteria:** both processes are up and a real HTTP call through the controller returns
mock data.

---

## Phase 3 — Run the test suite and generate a report

1. From `@MaxioPassthroughApiTests/`, run `dotnet test` with `PUBLICAPI_BASEURL` pointed at the
   running web project.
2. **Skip / exclude any test that fails with a Not Found (404 / route-not-found)** — those indicate
   an endpoint this integration doesn't expose, not a defect. Exclude them from the pass/fail totals
   (list them separately as "skipped — not found").
3. Produce a **report** containing:
   - Total passed / failed / skipped counts.
   - A table of each test → status (Passed / Failed / Skipped-NotFound).
   - For each failure: the assertion that failed and the actual vs. expected behavior.
   - A short summary of which Maxio operations are correctly exposed and any gaps.

**Deliverable:** the generated report (markdown), plus a one-line statement of whether the
controller correctly fronts `MaxioBillingClient` as a microservice.
