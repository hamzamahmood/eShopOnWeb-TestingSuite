# Verify the Maxio integration and fix-and-re-run until an authoritative zero-failure pass

## Your task
The Maxio Advanced Billing integration in this repo is already complete — the
MaxioBillingClient, its provider-agnostic seam, the DI registration, and the route
map all exist. Your job is not to build it. Your job is to prove it is correct and
drive it to correct, using the black-box verification test suite as the sole judge.

## The engine: the maxio-client-verifier skill
The `maxio-client-verifier` skill is your run engine. Invoking it will
generate/confirm the `MaxioBillingTestApi` project containing the MaxioBillingController and run the verification suite
once, producing the **raw JUnit XML** results and handing you its absolute path. The skill
does **not** interpret those results — reading them is your job (see *How to read the JUnit
XML* below). The skill deliberately stops at the raw XML — it never fixes and never
re-runs. You are the caller that reads the results and drives the fix-to-green loop around it.

## The non-negotiable loop: repeat until ZERO failures
This is a loop, not a single pass. You do not stop while the failure count is greater
than zero. Each round is:

run the suite (via the skill) → read the failures → fix the MaxioBillingClient and/or
MaxioBillingController → rebuild → run again.

After every run, if even one exposed-route test is failing, start another round:
re-diagnose, fix, rebuild, re-run. There is no round limit.

Before every round — including the very first run and every fix-and-re-run after it —
STOP and ask the user for explicit permission to proceed. Do not run the skill, apply
fixes, or re-run the suite until the user approves that round. When you pause, show
what you are about to do (for a fix round: the failures you intend to address, the
intent of each failing test, and the changes you plan to make) so the user can decide
with full context.

- Do not stop early, and do not conclude "this one is hard, leave it."
- Do not report a non-zero failure count as a final result — a round with failures is
  simply the signal to do another round.
- If a fix does not clear a test, that is not a reason to stop. Re-diagnose it from a
  different angle (the client method, the controller wiring, the DTO binding, the route
  map, the underlying Maxio API operation) and try again next round.

The only acceptable terminal state is a full run with **0 tests failing**, where every
remaining skip is a validated, legitimate route divergence.

## The authority rule
The test suite's results are the **sole** authority on whether the implementation is
correct. Nothing else counts — not "the code looks right," not a clean build, not your
own reasoning. The implementation is correct if and only if every test targeting an
exposed route passes and every skipped test is a legitimate route-divergence skip.
Until then, it is not done.

Because the results are authoritative, they must stay trustworthy. You may **never**:

- edit, recompile, weaken, or replace the test suite DLL, its `TestSettings`, or any
  test input to make a test pass;
- explore, browse, enumerate, open, decompile, or read the test suite DLL or any other
  file present in its folder — it is strictly a black box to be executed and nothing
  else. The only permitted interaction with it is running it via `dotnet vstest`;
- explore, browse, open, read, or inspect the mock server's implementation, source,
  or codebase (the service running at http://localhost:8080). Like the test DLL, it is
  strictly a black box: you may only send HTTP requests to it and read its live
  responses — never study how it decides those responses;
- override `RECORD_USAGE_PATH_TEMPLATE` or otherwise re-point a test to dodge a failure;
- relabel a real failure as a skip, or mark a test "expected to fail";
- fake, stub, or hardcode a controller response purely to satisfy an assertion — even
  when you can infer the expected outcome from the test's purpose. Knowing the
  intended behaviour is there to help you implement it for real, never to fabricate a
  matching response.

All fixes go into the real `MaxioBillingClient` and/or `MaxioBillingController` — the
actual behavior under test — and nowhere else. Zero failures must come from real fixes
to real behavior.

## Inputs
- The repo containing the completed Maxio integration (this working directory).
- The `maxio-client-verifier` skill (generate controller → run suite → hand back raw JUnit XML).
- The prebuilt verification test suite **DLL file** — path:
  `<<FILL IN THE TEST DLL FILE PATH HERE>>`. This is a single `.dll` file path, not a
  folder. Pass exactly this file to the skill and run only this file. Do not guess or
  substitute it. Treat it strictly as a black box: do not explore, browse, open,
  decompile, or read this DLL or any other file present in its folder — the only thing
  you ever do with it is execute it via `dotnet vstest`. (The runner will auto-load the
  JUnit logger and dependency assemblies sitting next to it at execution time — that is
  the runner's own doing and is expected; it does not license you to inspect that folder
  yourself.)
- The mock server, already running at `http://localhost:8080`. Treat it strictly as a
  black box: send requests and read live responses only. Do not explore, open, read, or
  inspect its implementation, source, or codebase to learn what it expects or how it
  responds.

## What the tests are checking (purpose of the suite)
The suite is a black-box behavioural contract for the Maxio billing microservice. Each
test verifies that the integration honours the Maxio API contract for the operation under
test — that its requests and responses conform to what the Maxio API expects and returns
— and that the relevant Maxio business rules are upheld. The suite also exercises API
resilience: how the integration holds up under error conditions and transient upstream
faults. The suite reports each test's intent alongside its result — for both passed and
failed tests — so you can see what a given test was checking without inferring it. The
test names are also self-describing. Use the reported intent (and the name) to establish
*what correct behaviour is expected*, then confirm the precise details (status codes,
field names, envelopes) against this repo's own Maxio API knowledge source, the route map,
and the real `MaxioBillingClient` method body.

Note: a few tests target variant-specific endpoints (e.g. a customer lookup only one
variant exposes). If THIS repo has no method for such a route, that is a legitimate route
divergence (a skip), not a failure — see Step 2.

## How to read the JUnit XML
The skill hands you the **absolute path to the raw JUnit XML** (from `JunitXml.TestLogger`),
not a pre-built report. Read that file and derive everything yourself — the console text is
not authoritative, the XML is.

- **Run totals (sanity cross-check):** the root `<testsuite>` element carries `tests`,
  `failures`, and `skipped` attributes. Use them only to confirm your per-test tally adds up.
- **One `<testcase>` per test**, with `classname` (`…Tests.<Class>`) and `name`
  (`<Method>`, plus `(argName: "…")` for a `[Theory]` case). Derive each verdict from its
  children:
  - has a `<failure …>` child ⇒ **Failed**;
  - has a `<skipped …>` child ⇒ **Skipped** — this is the suite's own route-divergence
    auto-skip (it marks the skip; you still validate it per Step 2);
  - neither child ⇒ **Passed**.
- **Where each test's intent is** (use it to state what correct behaviour was expected):
  - **Passed** → the `<system-out>` holds one `[intent] PASS — <detail>` line per
    assertion; the intent is the text inside the leading `[…]`.
  - **Failed** → the `<failure message="…">` **begins with** `[intent] — <reason>`; the
    intent is the leading bracketed text and the rest is the verbatim assertion result.
  - **Skipped** → the skip reason (the `<skipped>` message) carries the same `[intent] …`
    prefix.
- **The failed `<failure message="…">` is the verbatim assertion text to quote** in your
  report (actual vs. expected). For AI-payload content failures it is shaped
  `[intent] — Unit test failed due to payload verification. Field differences: - <field>: missing|mismatched`
  — the field(s) that diverged, nothing more. The `<failure>` element body is a stack
  trace; you can ignore it for the report.
- **Operation metadata:** each `<testcase>`'s `<properties>` carries `MaxioApi` (the Maxio
  operation, `METHOD /path.json`) and `Category` (`endpoint` | `plugin-advantage` |
  `safety-net`). Use these to name the operation a failure/skip belongs to.

## Procedure

### Step 1 — Baseline run (via the skill)
Before invoking the skill for the baseline run, ask the user for permission to proceed
and wait for their approval.

Invoke `maxio-client-verifier` end to end:
- **Phase 1:** confirm/generate the `MaxioBillingTestApi` controller, wired to the mock.
- **Phase 2:** run the suite against it (`dotnet vstest "<the DLL path above>"`, with
  `PUBLICAPI_BASEURL` pointed at the running `MaxioBillingTestApi`) and produce the raw
  JUnit XML.

Read the JUnit XML per *How to read the JUnit XML* and capture the baseline results
(per-test verdict + intent + `MaxioApi`/`Category`) verbatim. Do not fix anything yet.

### Step 2 — Validate every skip before trusting it
A skip is acceptable only if it is a genuine RouteDivergence: the test targets a route
this controller does not expose because this repo's billing seam has no method that
maps to it (or reaches the same Maxio operation through a different route the repo
legitimately chose). For each skipped test:
- Show the route the test targets and the exposed-route membership check.
- Confirm the route is genuinely absent from this repo's `IBillingClient` /
  `MaxioBillingClient` — not a route that should have been exposed but was missed or
  wired wrong during generation.
- Cross-check the test's purpose (see "What the tests are checking" above). Some tests
  target variant-specific endpoints (e.g. a lookup that only the plugin/SDK variant
  exposes); that helps you decide whether the route's absence is a legitimate divergence
  for THIS repo or a genuine gap you must expose in Step 3.
- If a "skip" is actually a route that ought to exist (the client has a matching
  method, or the route map clearly maps it), it is **not** a legitimate skip —
  reclassify it as a gap and fix it in Step 3.

Unexamined skips are not allowed. A test may remain skipped only once you have
positively justified it as route divergence.

### Step 3 — Diagnose and fix every failure (from intent, never by trial-and-error)
Do not touch a payload, DTO, field name, or status code until you can state, in one
sentence, what behaviour the test is checking and why it currently fails. For each
failing test:

- **Name its purpose first.** Take the test's intent from the XML (the `[intent] — …`
  prefix of its `<failure message>`; see *How to read the JUnit XML*), and read its
  self-describing name, to state the specific behaviour it
  verifies (e.g. `Missing_subscription_returns_404_not_found` → "reads a missing
  subscription and expects a REST-correct 404"; `Blank_email_is_rejected_before_reaching_the_billing_provider`
  → "rejects a blank email before the provider is ever called"). Where a name repeats
  across test classes, use the class to disambiguate.
- **State the failed assertion** and actual vs. expected (status code and/or body shape),
  then reconcile it with the intent. The gap between *what the intent says should happen*
  and *what actually happened* is your root-cause hypothesis.
- **Confirm the correct behaviour against the Maxio contract** — this repo's own Maxio API
  knowledge source, the route map, and the real `MaxioBillingClient` method body — so the
  fix reflects what Maxio and the test's intent actually require, not a shape
  reverse-engineered to satisfy the runner.
- **Make the fix in the real `MaxioBillingClient` and/or `MaxioBillingController`** so the
  behaviour the intent describes genuinely holds. Fix causes, not symptoms.
- Make any controller changes needed to expose routes for tests wrongly skipped in Step 2.
- Rebuild the affected projects.

**No trial-and-error.** Repeatedly mutating the payload, field names, or status code and
re-running in the hope that something turns green is prohibited — that is guessing, not
fixing, and it produces brittle or gamed passes. If you cannot articulate the test's
purpose and a concrete root-cause hypothesis, stop and re-read the suite's purpose above
and the Maxio contract before changing any code. Every change you make must be justified
by the test's intent and the real Maxio behaviour, and you must be able to explain, per
fix, why that change makes the intended behaviour correct.

### Step 4 — Re-run and iterate
Before starting each new round, pause and ask the user for permission to proceed with
that round's fixes and re-run; wait for approval before making any change or re-running
the suite. When you ask, summarize the planned fixes — and for each, the failing test's
intent and the root cause you identified.

Re-run the suite (Phase 2 of the skill) against the rebuilt controller and produce a
fresh report. Compare to the previous round:
- Confirm previously-failing tests now pass.
- Confirm no previously-passing test regressed.
- Re-validate skips (Step 2) if any routing changed.

Repeat Steps 2–4 for as many rounds as it takes. The only exit is a full run with
**Failed = 0**, reached entirely through real behavior fixes.

## Final deliverable
A single markdown report containing:
- **Final totals:** Passed / Failed (must be 0) / Skipped-RouteDivergence.
- **Per-test table:** test → final status.
- **For every skip:** the route + the membership check proving it is legitimate.
- **Change log:** each fix made, the failing test's intent, the file/method changed, and
  whether it was a generation defect (controller) or a behavioral divergence (client).
- **Round-by-round history:** how the counts changed each iteration.
- **Verdict:** a one-line statement — based solely on the authoritative test results —
  of whether the implementation correctly exposes and implements `MaxioBillingClient`
  as a Maxio microservice.
