---
name: maxio-client-verifier
description: >-
  Generate a standalone ASP.NET Core Web API controller that exposes an existing
  MaxioBillingClient, run the black-box verification test suite against it, and
  hand back the raw JUnit XML results (unprocessed) for the caller to interpret. Use this whenever you have completed a
  Maxio Advanced Billing integration in an eShopOnWeb-style base and want to
  expose it as a microservice and verify it end-to-end. Trigger it whenever the
  user mentions the Maxio client verification tests, the
  maxio-billing-service-route-map, the MaxioMockServer, a MaxioBillingController,
  or exposing/verifying the MaxioBillingClient, or says things like "verify my
  maxio client" or "run the maxio client verifier" — even if they do not say the
  word "skill". Applies whether the controller still needs generating or one
  already exists and only needs verifying. This skill stops at the raw JUnit XML; reading
  it and any fix-and-re-run loop are driven separately by the caller, not by this skill.
---

# Maxio Client Verifier — Generate, Run, and Return Raw Results

This skill takes a repo that already contains a working `MaxioBillingClient`
(an implementation of the provider-agnostic billing seam — commonly
`IBillingClient`, **but the interface may be named differently in this base**;
identify it by its role, not its name) and:

1. **Phase 1** — generates a standalone Web API controller. **If a testable controller already exists, skip Phase 1** (see the
   precondition check below) and go straight to running the tests.
2. **Phase 2** — runs the black-box verification test suite (from its prebuilt
   DLL) against the running controller and hands back the raw JUnit XML results.

Do the phases in order and **stop after Phase 2**. This skill's job ends with the
raw JUnit XML — it does **not** interpret those results, fix failures, or re-run in a
loop. If the caller wants an iterative test loop, they will instruct that
separately, using this skill as the run-and-hand-back step inside their own loop.
**Compile-only verification is never sufficient** — behavior must be confirmed live by
running the suite.

---

## The one rule that governs everything: discover, do not assume

Integration bases differ. The id types, **the name of the provider-agnostic seam
interface itself** (it is often `IBillingClient` but may be named something else —
find it by role: the interface `MaxioBillingClient` implements), the exact method
set on that seam, whether plan-change/cancel take a timing argument or use
dedicated methods, how the client talks to Maxio, the name/signature/argument-order
of the Maxio DI registration method, and how the client resolves its outbound base
URL **all vary between bases**. Never hard-code an assumption about which flavor
this is. Read the files below and treat them as ground truth.

**Inputs (read these first):**

- `src/Infrastructure/Services/**/MaxioBillingClient.cs` — the service to expose.
  It lives under `src/Infrastructure/Services/` but may be in a subfolder; locate
  it. Every method maps 1:1 to an upstream Maxio Advanced Billing API call — read
  the bodies to learn which upstream endpoint each one calls.
- `src/ApplicationCore/Interfaces/IBillingClient.cs` — the provider-agnostic seam
  it implements. **The file/interface may not be named `IBillingClient`** — locate
  whatever interface `MaxioBillingClient` implements (look at the class declaration:
  `class MaxioBillingClient : I<Something>`). That interface's method set is what
  you must expose (one endpoint per method). Read it for the exact methods,
  parameters, and id types in THIS repo.
- **The controller endpoints must expose the *exact behaviour* of the methods in
  `MaxioBillingClient`** — one controller endpoint per client method, surfacing
  what each method actually does verbatim.
- `docs/maxio-billing-service-route-map.md` — the authoritative route table.
- The compiled **verification test suite (a prebuilt DLL)**. Its exact filename is
  provided in the invoking prompt; do not assume a fixed name — use the one given. Its
  JUnit logger and dependency assemblies ship alongside it in the same folder.


---

## What the suite is checking (purpose)

Understand what the suite is for, so you can confirm the run is meaningful before handing
the XML back. It is a black-box behavioural contract for the Maxio billing microservice:
each test verifies that the integration honours the Maxio API contract for the operation
under test — that its requests and responses conform to what the Maxio API expects and
returns — and that the relevant Maxio business rules are upheld, and it also exercises API
resilience under error conditions and transient upstream faults. The suite records each
test's intent alongside its result **inside the JUnit XML** — for both passed and failed
tests (passed → the `[intent] PASS — …` lines in `<system-out>`; failed → the leading
`[intent] — …` of the `<failure message>`) — and the test names are self-describing, so
the caller can see what each test was checking without inferring it. This skill does not
interpret that intent; it simply produces the XML that carries it.

A few tests target variant-specific endpoints (e.g. a customer lookup only one variant
exposes). If this repo has no method for such a route, that is a legitimate
route-divergence skip, not a failure.


---

## Phase 1 — Generate the controller in a separate Web API project

### Precondition — is there already a testable controller?

**Before generating anything, check whether all maxio endpoints have been properly exposed through the MaxioBillingController.** (which is the requirement of this phase)
Look for a `MaxioBillingController` (or an equivalently-named controller that exposes
the billing client over `api/maxio`) in a standalone Web API project named `MaxioBillingTestApi`. If one is already present and buildable:


- **Skip the generation work in this phase entirely.** Do not regenerate,
  overwrite, or fork it.
- Do a quick sanity read to confirm it is wired to reach the mock (1c) — but do
  not rebuild it from scratch. If it builds and starts, proceed straight to
  **Phase 2** (run the tests and hand back the raw JUnit XML).
- Only fall back to generating (the steps below) if no such controller exists, or
  the existing one does not build / is clearly incomplete for the exposed method
  set. If it exists but has an obvious wiring gap (e.g. the base-URL redirect to
  the mock), flag it to the caller rather than silently rebuilding — fixing it is
  the caller's decision, not this skill's.

If no testable controller exists, generate one as follows.

Create a **new, standalone** ASP.NET Core Web API project named `MaxioBillingTestApi`
(separate from the existing `PublicApi`) hosting a single `MaxioBillingController`
that exposes `MaxioBillingClient` through the `IBillingClient` interface. Reference the existing `Infrastructure` and
`ApplicationCore` projects (and `BlazorShared` only if the client's own types
require it) so you reuse the real client — **do not fork or reimplement it.**

### Prerequisite — read the route table correctly

`docs/maxio-billing-service-route-map.md` is the routing source of truth. Its shape:

- A single table with two columns: *Maxio endpoint route* and *Controller endpoint route*.
- Its rows are a **union across multiple integration variants** — it contains rows
  for operations only some bases have, and sometimes more than one alternative
  controller route for the same goal. This is expected; it is not drift.
- Some rows carry `†`/`+` annotation marks. **Ignore those marks** — they are not
  part of the route.

### 1a. Endpoint mapping (one endpoint per client method)

Expose one controller endpoint per `IBillingClient` / `MaxioBillingClient` method
(1:1). Route prefix `api/maxio`. Derive every route from the table using this rule:

1. For each method on THIS repo's `IBillingClient`, read the client to see which
   upstream Maxio endpoint(s) it actually calls.
2. Find the table row whose *Maxio endpoint route* matches that upstream call and
   expose its *Controller endpoint route* verbatim (HTTP verb + path).
3. Wire only rows that correspond to a method present in this repo's `IBillingClient`.
4. The Controller endpoints should accurately expose the exact MaxioBillingClient behavior as endpoints.

When more than one row could serve one method, prefer the route that most fully mirrors
the underlying Maxio API operation (consistent with 1b). Two concrete cases, then a
fallback:

- **a. Component-scoped operation** — the Maxio path includes a component id. Expose the
  route that carries `{componentId}` and treat that segment as an **ignored path param**
  — keep it even when the client fixes the component from configuration rather than taking
  it as an argument. (Example: two rows front the same
  `.../components/{component_id}/usages.json` call — one with `{componentId}` in the
  controller path, one without; choose the one with it.)
- **b. A single method unifies an immediate and a deferred/at-renewal variant** (e.g. via
  a `timing` argument). Expose the **immediate**-variant route (`POST .../migrations`,
  `DELETE subscriptions/{id}`), not the deferred one (`PUT subscriptions/{id}`,
  `POST .../delayed_cancel`). This applies only when *one* method picks between the two at
  runtime; if the client has *separate* methods for the immediate and deferred variants,
  each maps 1:1 to its own route and there is no ambiguity to resolve.
- **c. Neither case applies** — pick the route whose path params the method's params can
  all supply.

Do not invent routes or hand-declare `[Http*]` attributes that disagree with the table.

### 1b. Input contract (shaped to the Maxio API, not the client)

Each endpoint takes a dedicated request DTO whose shape mirrors the corresponding
**Maxio API operation's input** (route params, query params, request body /
envelope wrappers, snake_case field names) — NOT merely whatever the current
`MaxioBillingClient` method signature accepts. This keeps the external contract
stable for microservice callers. Map the DTO onto the method's parameters for the
fields the client actually supports. Fields present in the Maxio API but unused by
the client may be accepted and ignored, but must not break binding.

### 1c. Configuration and wiring (point the service at the mock)

Add a separate Maxio configuration for this project, starting from:

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

If the app fails to boot citing a missing/invalid setting (some bases use
`AddOptions<MaxioSettings>().ValidateDataAnnotations().ValidateOnStart()` with
`[Required]` fields), add the missing key(s) with mock-compatible values.
`SkipStartupValidation` is included because some bases make a real outbound Maxio
call at startup unless it is set; it is a harmless no-op on bases that lack it.

Wire the Maxio services using **the repo's existing registration method** — do not
reimplement it. Locate it in Infrastructure; its name, signature, and argument
order vary (it may be an `IServiceCollection` extension, or a plain static method
taking `(IConfiguration, IServiceCollection)`, and may return void). Call it
exactly as defined. Then satisfy every remaining `MaxioBillingClient` constructor
dependency (e.g. a custom `IAppLogger<T>` adapter, an idempotency cache,
`AddMemoryCache()`, a logging `DelegatingHandler`). This microservice needs only
the Maxio services — **do not wire EF/LocalDB** or the eShop catalog/identity
database.

**Route all outbound Maxio traffic to the mock (not automatic).** A base may
derive its host from `Subdomain`/`Environment` or a preconfigured server template
and ignore `BaseUrl` entirely. Determine how THIS repo resolves its outbound base
URL, then ensure every outbound call targets the already-running mock at
`http://localhost:8080`. Prefer to do this in the new project's composition root
(e.g. override the registered client's base address / server config *after*
calling the repo's registration method); do not modify shared Infrastructure
unless redirection is genuinely impossible otherwise. Prove the redirect works
with a quick live call before running the suite: the response must contain the
mock's canned data, not a connection error or a call that escaped to a real Maxio
host.

**Phase 1 exit criteria:** the new project builds, and every method in this repo's
`IBillingClient` resolves to exactly one controller action whose route matches the
corresponding table row.

---

## Phase 2 — Run the verification test suite (from the DLL) and hand back the raw JUnit XML

**Assumptions at this point:** the mock server is **already running** (started
beforehand) at `http://localhost:8080`. Ensure the `MaxioBillingTestApi` project is also running on a fixed URL (e.g. `http://localhost:5199`), pointed at the mock. On Windows/PowerShell,
`VAR=value cmd` prefixes are bash-only — set env vars with `$env:VAR='value'`
first, or use the Bash tool.

**Run the tests from the prebuilt DLL, emitting a JUnit XML report.** The test
project's *source is not present in this environment* — only its compiled assembly
(DLL) is available, and its exact filename is supplied in the invoking prompt. You are not allowed to decompile the tests DLL. Only use it as a black box test. Do not
look for a test `.csproj` to `dotnet test`; instead run the compiled assembly directly
with the VSTest runner and the JUnit logger:

- Run the assembly with the JUnit logger (substitute the DLL filename given in the
  prompt), using an **absolute** `LogFilePath` so you know where to read the XML back:
  ```powershell
  $env:PUBLICAPI_BASEURL = 'http://localhost:5199'
  dotnet vstest "<path-to-test-dll>" --logger:"junit;LogFilePath=<abs-path>\maxio-results.xml"
  ```
  The JUnit logger DLLs ship next to the test DLL, so vstest auto-discovers the `junit`
  logger. Do **not** use `dotnet test` — that command is for project/solution sources,
  which are not present here.
- `PUBLICAPI_BASEURL` is the test suite's fixed env-var name for "the controller under
  test" — despite the `PUBLICAPI_` prefix it must point at `MaxioBillingTestApi`, **not**
  the eShop `PublicApi`. **Leave `RECORD_USAGE_PATH_TEMPLATE` at its default — do not
  override it.**

**Deliverable — the exact JUnit XML, unprocessed.** This skill does **not** parse,
summarize, classify, or build a report from the results. It writes the JUnit XML to the
absolute `LogFilePath` you passed above and hands **that path** back to the caller as the
sole result, stating plainly that the file is the raw, unmodified logger output and that
the caller must read and interpret it (verdicts, intent, route-divergence skips, and the
`MaxioApi`/`Category` operation metadata all live inside the XML). For the caller's
convenience, note where each test's **intent** sits in the XML: a **passed** test carries
its `[intent] PASS — …` lines in `<system-out>`, and a **failed** test's `<failure message="…">`
begins with `[intent] — …`. Do not compute counts, do not label tests, do not judge
pass rate. Then **stop** — do not attempt to fix failures or re-run. Interpreting the XML
and driving any fix loop is entirely the caller's job.

---

## What this skill deliberately does NOT do

- It does **not** parse, summarize, classify, or build a report from the JUnit XML.
  It hands the caller the exact, unprocessed XML (its absolute path) and nothing more —
  reading verdicts, counts, intent, and route-divergence skips out of it is the caller's job.
- It does **not** fix failing tests, edit `MaxioBillingClient`, or re-run the suite
  in a loop. It runs once and hands back the raw results.
