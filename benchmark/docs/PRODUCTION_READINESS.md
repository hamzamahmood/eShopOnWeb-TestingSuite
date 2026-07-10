# Production-Readiness Definition of Done — SDK-vs-Spec Token Benchmark

> **Status:** LOCKED · v0.3 · 2026-07-09
> **Purpose:** This document is the single source of truth for what "production-ready" means in
> this experiment. It is *pre-registered*: it must be finalized and frozen **before** either
> integration is built. Once locked, changes require a version bump + a dated rationale entry in
> §11 (Change control). The executable test gate (`gate/`) is nothing more than a faithful
> encoding of the checklist in §6.

---

## 1. Hypothesis

> Given the same task, an AI coding agent building against the **APIMatic-generated SDK** (via the
> `maxio-sdk` plugin) reaches the production-ready bar defined here using **fewer tokens** than an
> agent building against the **OpenAPI spec only** (hand-rolled `HttpClient`), because the spec-only
> arm must hand-write and debug resilience, error handling, auth, and hygiene that the SDK ships for
> free.

The experiment reports whichever way the data lands (see `PROTOCOL.md`). The value of the result is
entirely in the fairness of this gate; a gate shaped like either arm's output is worthless.

## 2. The two arms

Exactly **one** thing differs between arms: the **input material** describing the Maxio API.
Everything else is held byte-identical (base repo, task spec, model + effort, mock, gate, budget
cap, prompt scaffold, tooling). See `PROTOCOL.md` for the full held-constant list.

| Arm | Input material given to the agent |
|---|---|
| **A — SDK** | The `maxio-sdk` Claude plugin (skills → SDK source + `AsadAli.AdvancedBilling.Sdk` NuGet + calling-code guidance) |
| **B — Spec** | The Maxio Advanced Billing OpenAPI spec only |

Both arms build the **same public API surface** on eShopOnWeb (the routes in `TASK_SPEC.md`) and
talk to the **same mock** standing in for Maxio. Only the code *behind* the app's controller — SDK
calls vs. raw `HttpClient` — may differ.

## 3. Task scope — the 11 operations

The agent must expose one eShopOnWeb endpoint per capability below (exact app-side routes pinned in
`TASK_SPEC.md`). The "side effect" column drives which readiness checks are mandatory per op.

| # | Capability | Underlying Maxio call | HTTP safety | Billing side effect? |
|---|---|---|---|---|
| 1 | List plans | `GET /product_families/{id}/products.json` | safe | no |
| 2 | Find-or-create customer | `GET /customers/lookup.json` → `POST /customers.json` | mixed | no (idempotent by design) |
| 3 | List customer subscriptions | `GET /customers/{id}/subscriptions.json` | safe | no |
| 4 | Read subscription | `GET /subscriptions/{id}.json` | safe | no |
| 5 | Pause | `POST /subscriptions/{id}/hold.json` | unsafe | no |
| 6 | Resume | `POST /subscriptions/{id}/resume.json` | unsafe | no |
| 7 | Reactivate | `PUT /subscriptions/{id}/reactivate.json` | idempotent | no |
| 8 | **Create subscription** | `POST /subscriptions.json` | unsafe | **YES** |
| 9 | **Commit plan change** | `POST /subscriptions/{id}/migrations.json` | unsafe | **YES** |
| 10 | Cancel | `DELETE /subscriptions/{id}.json` | idempotent | yes (terminal) |
| 11 | **Record usage** | `POST /subscriptions/{id}/components/{c}/usages.json` | unsafe | **YES** |

The three **bold** ops (8, 9, 11) are the ones where a naive retry double-bills. The
"no-duplicate-write" property (R5) is mandatory on all three.

## 4. Definition of Done

> **The integration is "done" when the public gate (`gate/public`) reports all checks green.**

The agent is told this verbatim in its prompt and iterates against `gate/public` until green (or the
per-run token budget cap is hit → the run is recorded as a **failure**, see `PROTOCOL.md`). Tokens
consumed to reach green is the measured quantity.

After a run reaches green, the experimenter (not the agent) runs the **hidden holdout** (`gate/holdout`)
and the arm-agnostic observational checks. Passing public but failing holdout ⇒ the arm coded to the
visible tests rather than being genuinely robust (recorded, see §7 and `PROTOCOL.md`).

### 4.1 What the agent can see (gate visibility)

The agent works **checklist + run-only**:
- It is given this document's §6 properties (the prose requirements).
- It can **run the public gate** as a single command and see, per check, pass/fail plus the
  failure message.
- It **cannot read the gate's test source** — the magic references/handles, exact assertion
  values, and (obviously) the holdout are not in its working tree.

The gate must therefore be a **single, reliable, hermetic command** (spins up mock + Toxiproxy +
app, toggles faults, runs checks, tears down) so that "run the gate" is cheap and non-flaky — gate
setup cost and flakiness are measurement noise charged to whichever arm hits them.

## 5. The Fairness Principle (read before writing any test)

**Assert properties, ranges, and semantics — never one arm's exact output.** This is the rule that
keeps the gate implementation-agnostic and the result defensible.

| ❌ Rigged (asserts an arm's idioms) | ✅ Fair (asserts a property any good integration has) |
|---|---|
| Create returns **exactly 201** | Create returns **any 2xx** |
| Unknown subscription returns **exactly 404** | Unknown resource returns a **4xx that clearly means "not found"**, never a 5xx |
| Response field is named `subscriptionId` | Response body **semantically contains a usable subscription identifier** (any field name) |
| State string equals `"Active"` (SDK enum casing) | State **semantically equals "active"** (case/format-insensitive, judged by meaning) |
| Retry policy calls `DisableForUnsafeHttpMethods()` | Under an injected 503 on a create, the mock **received the POST exactly once** |

Corollaries:
- **No code-grep in the hard gate.** Inspecting source for a specific method call is arm-specific and
  unfair — the SDK arm satisfies the same property through the SDK's own retry config, not app code.
  Verify the *behavior* instead (request-counting mock, timing, log-scrape, boot-test).
- **Deterministic-first verification.** Prefer status-code checks, forbidden/required-substring
  sweeps, mock request-counts, timing-liveness, and **value-presence** matching (assert the
  expected *value* — an id, price, state string — appears in the body, regardless of field name)
  over any semantic judgment. An **arm-blind LLM judge** is a *fallback only*, used solely where a
  required value cannot be matched literally; it never learns which arm produced the body and — if
  used — comes from a different model family than the arm under test. Keeping the oracle
  deterministic matters because gate non-determinism is charged to the arms as wasted tokens.
- **The mock is the shared contract.** The mock must conform to the *same* Maxio OpenAPI spec that
  is handed to Arm B (and that the SDK in Arm A targets). A single contract source, or one arm is
  advantaged.

## 6. The readiness checklist (the gate)

Every item is a property, phrased arm-agnostically. Verification tag: **BB** = black-box HTTP;
**OBS** = observational (mock request-recording / timing / log-scrape / boot test) — still
arm-agnostic; nothing here reads the arm's source.

### Resilience & transport
| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| R1 | Transient 5xx on a safe GET recovers | ops 1,3,4 (+lookup in 2) | BB | mock returns 503-then-200; app call succeeds (2xx) |
| R2 | Rate-limit (429) recovers to success | safe GET | BB | mock returns 429-then-200; app call succeeds (2xx). No timing assertion — `Retry-After` honoring is desirable-not-gated (§10) |
| R3 | Transport fault is wrapped, not leaked | any op | BB | the mock resets the connection (`HttpContext.Abort`); app returns a clean mapped 5xx-class error, **no** raw stack/exception text, **no** crash |
| R4 | A timeout exists (client never hangs forever) | any op | OBS-coarse | the mock holds the connection open indefinitely (`Task.Delay`); app **terminates with a mapped error** within a generous ceiling (e.g. ≤ 60s) rather than hanging. Coarse liveness check, not a latency measurement |
| R5 | **A failed write is not duplicated** | ops 8,9,11 | OBS-count | mock returns 503 on the POST and records inbound count; **count == 1** (client did not resend the unsafe write) |
| R6 | Retries are bounded | safe GET | OBS-count | mock returns persistent 503; app gives up with a mapped error; recorded attempt count ≤ a small bound |

### Error handling & hygiene
| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| E1 | Provider domain error → defensible client 4xx + clean body | write ops | BB | mock returns a 422 domain error; app returns a **4xx** (range accepted) with a clean JSON error body |
| E2 | Unknown resource → client-error, not server-error | ops 3,4,5,6,7,9,10,11 | BB | read/act on an unknown id; app returns a **4xx** (404 or 422 both accepted), never a 5xx or a crash. Deterministic status check (hygiene of the body is covered by E3) |
| E3 | No failure body leaks internals | all failure paths | BB | forbidden-substring sweep: no stack trace, internal exception type, secret, or raw upstream body in any error response |
| E4 | Malformed upstream body tolerated | any op | BB | mock returns garbage JSON; app returns a mapped error, does not crash or leak |

### Correctness / contract
| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| C1 | Happy path returns success + required data | all 11 ops | BB | 2xx + the required **values** appear in the body (value-presence, field-name-agnostic — e.g. the created subscription's id/amount/state value is present); LLM-judge fallback only for a value that can't be matched literally |
| C2 | Unknown extra response field tolerated | any read op | BB | mock adds an unexpected field; app still succeeds (forward-compat) |
| C3 | Invalid request rejected locally, no upstream call | write ops | OBS-count | send a request missing a required field; app returns a local 4xx; **mock received zero calls** |

### Security / observability / lifecycle
| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| S1 | Secret never logged | whole run | OBS-logscrape | after a run, scrape the app's emitted logs/stdout for the known API key value; **absent** |
| S2 | Auth is actually applied | any op | BB | mock's auth-checking variant returns 401 when the auth header is absent/wrong; app's real calls are accepted |
| S3 | Missing required config fails fast at boot | startup | OBS-boot | boot the app with a required setting removed; it fails to start / returns a clear config error rather than 500ing at first request |

## 7. Public gate vs. hidden holdout

- **`gate/public`** — the checklist in §6, encoded. This is the agent's definition of done. Fair
  because both arms get the identical gate and iterate against it.
- **`gate/holdout`** — the *same property classes, different concrete instances*, never shown to the
  agent. Detects gaming. Minimum holdout set:
  - R1/R2 on a *different* endpoint than the public gate uses
  - R3 (transport reset) on a *read* op instead of the lookup
  - R2 with `Retry-After` as an **HTTP-date** rather than delta-seconds
  - E4 with a *different* malformed-body shape
  - S1 log-scrape for a *second* secret (the subdomain, not the API key)

A run's outcome is recorded as: **pass-public**, and separately **pass-holdout** (Y/N). The token
cost is "cost to reach pass-public"; holdout is a robustness/anti-gaming annotation.

## 8. Verification instruments

- **Mock (pure .NET, minimal-API)** — faithfully implements the Maxio OpenAPI contract (the shared
  contract, §5). Beyond canned data it can, keyed by magic references/handles in the request:
  return 429+`Retry-After`, 503-then-200, malformed JSON; **record inbound request counts, bodies,
  and timestamps** (this is what makes R2/R4/R5/R6/C3 black-box-observable); and expose an
  **auth-checking variant** for S2.
- **Transport faults are injected by the mock itself** (verified on Windows): connection reset via
  `HttpContext.Abort()` (the client sees a genuine transport error — curl exit 56 / .NET
  `HttpRequestException`), and hang-past-timeout via `Task.Delay`. No proxy layer is needed; the
  app's base URL points straight at the mock. (Toxiproxy was planned but proved unnecessary once
  in-proc `Abort()` was confirmed to yield a real client-side transport error.)
- **Log-scrape** — the harness captures the app's stdout/log file per run; S1 greps it for known
  secret values. Arm-agnostic (both arms emit logs).
- **Boot test** — the harness boots the app with a mutated config for S3.

Division of labor: the **mock** injects both transport-level faults (reset/hang) and
application-level faults (429/503/malformed), and records every inbound request.

## 9. Pass / fail criteria

Two success tiers are recorded per run (this closes the "gaming the visible tests looks like a
cheap win" hole):
- **DONE (pass-public)** = every check in `gate/public` passes. This defines when the agent stops,
  and "tokens to reach DONE" is the primary measured quantity.
- **ROBUST (pass-public AND pass-holdout)** = also passes the hidden holdout §7. Gaming the visible
  tests reaches DONE but not ROBUST.

We report **both** token distributions — cost-to-DONE and cost-to-ROBUST — so an arm cannot win by
overfitting the visible gate. (Final tiering rules live in `PROTOCOL.md`.)

Other rules:
- A run that does not reach DONE before the per-run **token budget cap** is a **failure**; its
  tokens still count (effectiveness-aware cost-per-success in `PROTOCOL.md`). Never average tokens
  over only the runs that reached green.
- Desirable-not-gated items (§10) never block DONE; they are qualitative annotations only.

## 10. Explicitly NOT gated (desirable, not required)

These are genuinely hard to verify fairly black-box, or only via arm-specific code inspection, so
they are **out of the hard gate** to preserve fairness. Note them as qualitative observations only:
- Exact backoff **jitter distribution / exponential shape** (timing too noisy black-box).
- **Circuit-breaker thresholds** (failure ratio, sampling window) — hard to exercise reliably.
- Specific config values (exact total vs per-attempt timeout numbers).
- **`Retry-After` honoring** on 429/503 (relaxed out of R2): asserting the client *waited* the
  advertised delay is timing-flaky, and neither Polly nor the SDK honor `Retry-After` by default —
  gating it would add noise and force custom code on both arms. Observe qualitatively instead.

## 11. Change control

This document is frozen at lock time. Any change after lock requires a new version and an entry
here: `vX.Y — YYYY-MM-DD — what changed — why`. Changes made after seeing run results are
disallowed (would constitute p-hacking) unless they *loosen* the gate for both arms symmetrically
and are disclosed.

- v0.1 — 2026-07-09 — initial draft — pending review before lock.
- v0.2 — 2026-07-09 — review pass — relaxed R2 to outcome-only (Retry-After → §10 desirable);
  R4 → coarse liveness check; deterministic-first verification with value-presence matching and
  LLM-judge as fallback only (§5, C1, E2); added §4.1 gate-visibility (checklist + run-only); added
  DONE vs ROBUST success tiers to close the holdout-gaming hole (§9). §6 coverage kept as-is.
- **v0.2 LOCKED — 2026-07-09** — frozen as the definition of done, before any integration is built.
  No further changes without a version bump + rationale here; changes after seeing run results are
  disallowed (see §11 rules).
- v0.3 — 2026-07-09 — instrument amendment (pre-run, benign): transport faults (R3 reset / R4 hang)
  are injected by the mock itself (`HttpContext.Abort()` / `Task.Delay`, verified on Windows to yield
  a real client-side transport error), so **Toxiproxy is dropped** and the app's base URL points
  straight at the mock. The R3/R4 *properties* and every other check are unchanged.

## 12. Companion documents

- `TASK_SPEC.md` — the neutral functional task: the exact eShopOnWeb routes to build, happy-path
  semantics per op, and the identical prompt scaffold given to both arms.
- `PROTOCOL.md` — the experimental protocol: held-constant list, N runs, budget cap, token capture
  rig, statistics, DONE/ROBUST tiering, and credibility safeguards.
- `gate/` — the executable encoding of §6 (public) and §7 (holdout).
- `mock/` — the spec-faithful, fault-injecting, request-recording Maxio mock.
