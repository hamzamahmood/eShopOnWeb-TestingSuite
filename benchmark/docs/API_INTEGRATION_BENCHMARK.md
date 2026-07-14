# API Integration Benchmark — Criteria & Methodology

> **What this is.** A single, reusable bar for judging the quality of *any* API integration —
> whether an AI agent or a human wrote it, whether it was built from an SDK, a client library, or a raw
> OpenAPI spec. It answers two questions about one integration, standalone (no comparison build
> required): **(1) Is it production-ready?** (a pass/fail gate) and **(2) How good is it, in depth?** (a
> seven-dimension quality scorecard).
>
> **Why it exists / how to aim it at SDK & client-library shortcomings.** The dimensions below are the
> axes on which a generated SDK or an SDK-delivery plugin most often *underperforms* a lean hand-rolled
> integration — drift brittleness, dependency surface, navigation/learning cost, error-surface
> ergonomics. Run any SDK-based integration through this bar and the dimensions where it scores poorly
> **are the shortcomings to fix.** §7 maps each failure mode to the dimension that catches it and the
> remediation lever. You do not need a baseline build to use the bar — but if you have one, every
> dimension is comparative too.
>
> **Self-contained.** This document is a complete methodology on its own. It describes each instrument
> abstractly — what it must measure, its oracle, its pass bar — so you can implement it in any language
> or toolchain and point it at any API. Nothing here depends on a particular provider, app, or prior
> study.

---

## 0. How to use this

| You want to… | Use | Cost |
|---|---|---|
| Decide if an integration is shippable | **Part A** — the readiness gate (DONE / ROBUST) | deterministic, cheap |
| Judge how good it is beyond "it passes" | **Part B** — the 7-dimension scorecard | mostly deterministic |
| Run it rigorously / repeatably / at N | **Part C** — methodology, stats, instruments | scales with N |
| Find what to fix in an SDK or client library | **§7** — the shortcomings diagnostic | reads Parts A+B |

**Minimum viable evaluation** (one integration, one afternoon): run Part A to green, then score D1–D4
from Part B. That already tells you production-readiness + the four deterministic quality axes. D5–D7
(dev-speed, tests, readability) and the statistics (Part C) are the deeper, more expensive layers.

**The bar is honest by construction, not by intention.** Every rule in §1 exists to stop the evaluator
(especially an evaluator with a stake in the outcome) from shaping the test toward a preferred answer.
Adopt §1 first; the rest is application.

---

## 1. Principles (adopt these before writing a single check)

These are the load-bearing ideas. They are what make a score defensible; they port to any API,
language, and toolchain.

1. **Assert properties, never one implementation's output.** A check must state a property *any* good
   integration has — not the idiom of the implementation you happen to be looking at. This is what
   keeps the bar portable and un-riggable.

   | ❌ Implementation-specific (rigged) | ✅ Property-based (fair & portable) |
   |---|---|
   | Create returns **exactly 201** | Create returns **any 2xx** |
   | Unknown id returns **exactly 404** | Unknown resource returns a **4xx meaning "not found"**, never a 5xx |
   | Response field is named `subscriptionId` | Body **semantically contains a usable identifier** (any field name) |
   | State equals `"Active"` (one library's enum casing) | State **semantically equals "active"** (case/format-insensitive) |
   | Retry policy calls a specific method | Under an injected 503 on a write, the provider **received the POST exactly once** |

2. **Verify behavior, not source — for the pass gate.** The readiness gate (Part A) inspects the
   running integration's *behavior* (status codes, recorded upstream calls, timing, logs, boot), never
   its code. Reading source for "did you call method X" is implementation-specific and unfair; a good
   integration can satisfy a property through a library's config instead of app code. **Source
   inspection is allowed only in Part B (quality)** — and there, only for *universal* properties
   (complexity, coupling, dependency count), never "did you use a particular library."

3. **Deterministic-first; LLM judgment is a labelled, low-trust fallback.** Prefer status checks,
   required/forbidden-substring sweeps, upstream-call counts (a recording mock), timing-liveness, and
   **value-presence matching** (assert the expected *value* appears in the body, regardless of field
   name). An identity-blind LLM judge is used only where a value cannot be matched literally, and it is
   never the headline. Non-determinism in the oracle is a cost charged to whatever you're measuring.

4. **Discrimination-validate every instrument — the "gate on the gate."** Before you trust a check,
   prove it scores a **known-good** reference **high** and a **deliberately-broken** variant **low**.
   An instrument that cannot separate good from bad is not shipped. Build a small library of injectable
   defects for exactly this. If a metric doesn't move when you break the thing it claims to measure, it
   measures nothing.

5. **Two-sided by construction.** An instrument that can only make one approach look good is biased.
   For every dimension, name a concrete case where the *other* kind of integration should win, and
   confirm the instrument surfaces it. (Example: a hand-rolled string parser tolerating a value a typed
   SDK rejects, or an SDK precisely surfacing an error a thin mapper flattens — a good drift/error
   instrument must be able to show *either* winning.)

6. **Pre-register and lock before you score.** Freeze the criteria, thresholds, normalization anchors,
   and defect fixtures — commit and content-hash them — *before* measuring the integration under test.
   Changes after seeing scores are disallowed unless they *loosen* a check symmetrically and are
   disclosed. This blocks metric-shopping / HARKing, which matters most when the evaluator authored the
   thing being evaluated.

7. **Scorecard-first — never a single number.** Report the per-dimension scorecard as the headline. A
   weighted composite, if quoted at all, is secondary and always ships with a **weight-sensitivity
   analysis** (does the ranking survive several defensible weightings?). Two integrations can tie on a
   composite and differ completely in shape; the shape is the finding.

8. **Report which way it lands.** State the result even when it's "no difference," "the thing I hoped
   would win lost," or "parity is real." A benchmark that can only confirm the answer you wanted is not
   a benchmark.

---

## 2. What counts as "done" — the two success tiers

The readiness gate (Part A) yields two tiers, recorded separately. This closes the "gaming the visible
checks looks like success" hole.

- **DONE** — every **public** check passes. The public checks are the ones the builder can see and
  iterate against; passing them defines shippable-on-paper.
- **ROBUST** — DONE **and** passes a **hidden holdout**: the *same property classes, different concrete
  instances*, never shown to the builder. An integration that coded to the visible checks reaches DONE
  but not ROBUST.

Always report both. A large DONE≫ROBUST gap means the integration overfit its visible tests. When
evaluating an *agent* build, the agent iterates against the public gate only; you (the evaluator) run
the holdout afterward.

---

## Part A — Production-readiness gate

The pass/fail bar. Every item is a **property**, phrased implementation-agnostically, verified by
behavior. Verification tags: **BB** = black-box HTTP; **OBS** = observational (recording-mock counts /
timing / log-scrape / boot test). Nothing here reads the integration's source.

The checklist is grouped in four families. Adapt the concrete op names to your API; the *properties*
are universal. "Write op" = any non-idempotent state-changing call (the ones a naive retry
double-executes); "safe op" = a read/GET.

### A.1 Resilience & transport

| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| R1 | Transient 5xx on a read recovers | reads | BB | upstream 503-then-200; call still succeeds (2xx) |
| R2 | Rate-limit (429) recovers | reads | BB | upstream 429-then-200; call succeeds. (Honoring `Retry-After` *timing* is desirable-not-gated — see A.5) |
| R3 | Transport fault is wrapped, not leaked | any op | BB | upstream resets the connection; integration returns a clean mapped 5xx-class error — **no** raw stack/exception text, **no** crash |
| R4 | A timeout exists (never hangs forever) | any op | OBS-coarse | upstream holds the socket open; integration **terminates with a mapped error** within a generous ceiling (e.g. ≤60s), not a hang. Coarse liveness, not a latency measurement |
| R5 | **A failed write is not duplicated** | write ops | OBS-count | upstream 503s the write and counts inbound requests; **count == 1** (no resend of an unsafe write) |
| R6 | Retries are bounded | reads | OBS-count | persistent upstream 503; integration gives up with a mapped error; recorded attempts ≤ a small bound |

### A.2 Error handling & hygiene

| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| E1 | Provider domain error → defensible client 4xx + clean body | write ops | BB | upstream 422 domain error; integration returns a **4xx** with a clean JSON error body |
| E2 | Unknown resource → client-error, not server-error | reads/acts on ids | BB | act on an unknown id; integration returns a **4xx** (404/422), never a 5xx or crash |
| E3 | No failure body leaks internals | all failure paths | BB | forbidden-substring sweep: no stack trace, internal type name, secret, or raw upstream body in any error response |
| E4 | Malformed upstream body tolerated | any op | BB | upstream returns garbage JSON; integration returns a mapped error, does not crash or leak |

### A.3 Correctness / contract

| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| C1 | Happy path returns success + required data | all ops | BB | 2xx + the required **values** appear in the body (value-presence, field-name-agnostic); LLM-judge fallback only for a value that can't be matched literally |
| C2 | Unknown extra response field tolerated | reads | BB | upstream adds an unexpected field; integration still succeeds (forward-compat) |
| C3 | Invalid request rejected locally, no upstream call | write ops | OBS-count | send a request missing a required field; integration returns a local 4xx; **upstream received zero calls** |

### A.4 Security / observability / lifecycle

| ID | Property | Applies to | Verify | Pass criterion |
|---|---|---|---|---|
| S1 | Secret never logged | whole run | OBS-logscrape | scrape emitted logs/stdout for the known credential value; **absent** |
| S2 | Auth is actually applied | any op | BB | upstream's auth-checking variant returns 401 when the auth header is absent/wrong; real calls are accepted |
| S3 | Missing required config fails fast at boot | startup | OBS-boot | boot with a required setting removed; it fails to start / returns a clear config error rather than 500ing at first request |

### A.5 Explicitly NOT gated (desirable, not required)

Hard to verify fairly black-box, or only via implementation-specific inspection — observe
qualitatively, never block on them:

- Exact backoff **jitter / exponential shape** (timing too noisy black-box).
- **Circuit-breaker thresholds** (failure ratio, sampling window) — hard to exercise reliably.
- Specific config values (exact total-vs-per-attempt timeout numbers).
- **`Retry-After` honoring** on 429/503 — asserting the client *waited* the advertised delay is
  timing-flaky, and many stacks don't honor it by default; gating it forces custom code and adds noise.

### A.6 Anti-gaming

Two mechanisms, both required, so a hardcoded happy-path can't pass:
1. **The recording mock asserts a genuine upstream call** (right method + path, count ≥ 1) for each op
   — an integration that fakes responses without calling the provider fails.
2. **The hidden holdout** (§2) — gaming the visible checks reaches DONE but fails ROBUST.

> **Reference instruments (Part C.1):** a **hermetic gate runner** (boots the mock + the app, runs the
> public and holdout checks, exits pass/fail), a **recording, fault-injecting mock** standing in for the
> provider, and a **known-good reference integration with injectable defects** used to
> discrimination-validate the gate.

---

## Part B — Quality scorecard

Seven dimensions anchored to **ISO/IEC 25010:2023**, in three trust tiers. D1–D4 are deterministic and
cheap (run them always). D5–D6 need agent runs or existing tests (conditional). D7 is subjective and
lowest-trust (never the headline).

For each dimension: **what it measures · oracle · the standalone reading (what "good" looks like) · the
SDK/library shortcoming it exposes.** Thresholds are starting defaults — adjust per API, but *lock them
before scoring* (§1.6).

### D1 — Correctness depth · Functional Suitability · Tier 1
- **Measures:** not just "a required value appears somewhere" (that's gate C1) but that it sits in a
  **semantically-right place**, in the **right numeric units/magnitude**, with correct **list
  cardinality** and **empty/unknown** handling.
- **Oracle:** deterministic. Value-in-right-position; units consistency (e.g. a value stored in cents
  must surface as the right dollar magnitude — neither raw cents nor double-divided); a list returns the
  expected count; an empty list ≠ an error; an unknown id → a right-shaped 4xx.
- **Good looks like:** ~100%. This dimension usually shows **parity** — most integrations that pass the
  gate are genuinely correct — so a *dip* here is a real red flag, not noise.
- **SDK/library lens:** rarely where an SDK wins or loses; if the SDK mis-maps a field or drops list
  items despite its types, that's a generator bug worth catching.

### D2 — API-drift resilience · Reliability + Flexibility · Tier 1 · **crown jewel**
- **Measures:** what happens when the provider's schema *evolves* under a shipped integration — the
  single most decision-relevant quality axis, and the one where typed SDKs and hand-rolled code diverge
  most.
- **Oracle:** deterministic **replay**. Without touching the integration's code, mutate the provider's
  *outgoing* JSON with one **drift profile** at a time (catalogue below) and classify the response:

  | Class | Meaning | Score |
  |---|---|---:|
  | **CORRECT** | right data still returned | 1.0 |
  | **GRACEFUL** | clean 4xx/empty, no crash, no leak | 0.5 |
  | **BROKEN** | 5xx / crash / hang / internals leaked | 0.0 |
  | **SILENT-WRONG** | 2xx but blank/incorrect data | 0.0 ⚠ |

  Report the **4-way confusion matrix** (primary artifact) plus **two orthogonal lenses**:
  - **Resilience** = `(CORRECT + 0.5·GRACEFUL) / N` — did it keep delivering correct data?
  - **Safety** = `(N − SILENT-WRONG) / N` — did it fail **detectably** rather than silently corrupt?

  **SILENT-WRONG is the worst outcome** (a corrupt bill that looks fine). For anything money-touching, a
  loud 5xx beats a silent blank 2xx — the two lenses make that trade explicit instead of averaging it.
- **Good looks like:** high on *both* lenses. A common real result is a **trade**: tolerant
  deserialization scores higher Resilience (absorbs type/field drift) but risks SILENT-WRONG; strict
  typing scores higher Safety (fails loud) but lower Resilience (rejects drift it could have absorbed).
  Neither number alone is the verdict.
- **SDK/library lens:** the SDK's pitch is "types absorb drift." **Test it — it can be the reverse:**
  a strict typed deserializer may 502 on an `int→string` retype or an envelope rename that a
  string-tolerant hand-rolled parser shrugs off. Where the SDK is *less* drift-resilient, that's a
  concrete shortcoming (see §7).

#### Drift-profile catalogue (apply one at a time, isolated cells)
| # | Profile | Transform | Primarily tests |
|---|---|---|---|
| P1 | additive-field | inject an unknown scalar + nested object | forward-compat |
| P2 | field-rename | rename a leaf the integration reads | silent mis-map vs tolerance |
| P3 | retype-scalar | change a scalar's JSON type (`int`→`"str"`) | deserialization tolerance |
| P4 | scalar→union | a scalar becomes an object/union | union handling vs throw |
| P5 | new-enum-value | emit an unmodeled enum value | enum brittleness (strict enums reject; strings pass) |
| P6 | envelope-rename | rename/re-nest the wrapper key | envelope coupling |
| P7 | error-shape | change the error body shape on a failure path (field-map / bare string / HTML) | error-parse robustness |
| P8 | field-removal | drop a non-critical field | graceful degradation |

> **Oracle limitation to disclose:** a whole-body value-presence oracle is *conservative* on renames —
> if a renamed field's value survives elsewhere in the body, a rich pass-through response can score
> CORRECT even if it dropped the primary field. This bias runs *toward* pass-through/SDK-style
> responses, so a thin-mapper's drift win measured this way is a **floor**, not a ceiling.

### D3 — Maintainability · Maintainability · Tier 1
- **Measures:** how hard the integration is to read and change. Over the **integration files only**, not
  the host app.
- **Oracle:** deterministic static analysis —
  - **Cyclomatic complexity + Maintainability Index** (e.g. Roslyn `Microsoft.CodeAnalysis.Metrics` /
    Roslynator for .NET, or the equivalent for your stack; CC flagged >10).
  - **Wire-coupling count** (the sharpest, most objective signal): a scripted count of hand-maintained
    wire artifacts — literal URL fragments, wire field-name string literals, query-param spellings,
    manual auth-string construction. This is what an SDK *removes* and a hand-rolled build *owns*.
  - **Integrator-owned LOC** of the integration layer.
  - **Code-smell density** (static analyzers via a pinned config; a hosted scanner if available).
- **Good looks like:** low CC (mean < ~3, max nesting ≤ 5), low smell density. Wire-coupling and owned
  LOC are **directional/relative** (an SDK approaches 0; a hand-rolled build owns tens–hundreds) — track
  them, but read them as "how much wire contract am I on the hook for," not a pass/fail.
- **SDK/library lens:** this is the SDK's **honest home win** — near-zero wire-coupling, less owned code,
  often shallower nesting. When benchmarking an SDK, *confirm* it delivers this; if generated call sites
  are noisy or deeply nested, the generator has a maintainability shortcoming even here.

### D4 — Security depth · Security · Tier 1
- **Measures:** supply-chain surface + code-level security, beyond the gate's S1–S3.
- **Oracle:** deterministic scanners —
  - **Supply-chain surface:** a vulnerable-package scan (e.g. `dotnet list package --vulnerable
    --include-transitive`, or the equivalent) + **transitive dependency count**. An SDK adds a
    dependency graph a hand-rolled build doesn't — a real, measurable con.
  - **Static security scan:** a CWE-pattern scanner (e.g. CodeQL, or an analyzer for your stack);
    security hotspots from a hosted scanner if available.
  - **Expanded leak/auth:** a larger forbidden-substring set across more error paths than the gate's;
    assert the auth *scheme* is correct (e.g. Basic `Base64("{key}:x")`), not merely present.
- **Good looks like:** **zero** source-level security findings (absolute pass bar); vulnerable-package
  count near zero (discount shared-baseline deps); dependency count as low as the approach allows.
- **SDK/library lens:** dependency **bloat** is a recurring SDK shortcoming — every transitive package is
  attack surface and an upgrade obligation. Count it explicitly.

### D5 — Modifiability / dev-speed · Maintainability (modifiability) · Tier 2 (expensive)
- **Measures:** the cost of the *next* change — does the integration's structure make extending it
  cheap? This is where quality and token-cost re-converge, and (for an SDK) the fairest place to win,
  because the up-front learning cost is already paid.
- **Oracle:** empirical. Give a *fresh* agent the finished integration + one small, pre-registered
  extension task (add one endpoint that composes existing operations), and measure **cost / output
  tokens / turns / success** to make a small extension gate green. Same token rig as Part C.
- **Good looks like:** low cost, high success, and — the mechanistic tell — **low context re-read**
  (cache-read tokens), meaning the structure was already legible.
- **SDK/library lens:** the amortization test. An SDK **front-loads** a learning cost and should
  **discount** the next change (types/wiring already in place). If your SDK does *not* discount the next
  change, its structure isn't earning its front-load. **Caveat:** measure a genuinely-new-resource
  extension too, not only compose-what-exists (the SDK's best case).

### D6 — Test quality · Maintainability (testability) · Tier 2 (conditional)
- **Measures:** did the builder test its own integration, and how well?
- **Oracle:** if tests exist — **mutation score** (e.g. Stryker) over the integration + line/branch
  coverage. If none exist, report coverage = 0 as a **finding**, not a forced metric.
- **Good looks like:** tests exist at all. (Agents frequently ship an *untested* integration unless
  explicitly told to test — worth flagging on any agent build, independent of SDK-vs-hand-rolled.)

### D7 — Readability / idiomaticity / design · subjective · Tier 3 (low-trust)
- **Measures:** what metrics can't — naming, cohesion, idiomatic style, comment quality.
- **Oracle:** a **blind LLM-judge ensemble** with the full bias-control apparatus (Part C.6). Reported
  with inter-judge variance and wide error bars; **never** the headline.
- **SDK/library lens:** judges can surface call-site noise, auth mistakes, and awkward generated
  ergonomics — useful signal, but treat it as a hypothesis to confirm deterministically, not a verdict.

### Scoring & normalization
- **Normalize each dimension to [0,1] against pre-registered anchors:** the known-good reference = high
  anchor (1.0); a matching injected-defect variant = low anchor (0.0). A raw MI of 72 becomes a fraction
  of the achievable range, not a bare number. Freeze anchors before scoring.
- **Direction handling:** for lower-is-better metrics (CC, vuln count, wire-coupling), invert so 1.0
  always means "better." State the direction per metric.
- **Headline = the radar/table of dimensions.** A composite (default equal weights across the four
  Tier-1 dims; Tier-2/3 reported but excluded from the default composite) is secondary and always ships
  with a weight-sensitivity analysis.

---

## Part C — Methodology of measuring

How to run the above rigorously and repeatably.

### C.1 Instruments
Build (or reuse) five instruments. Describe each abstractly so it ports to any stack:

| Instrument | Role |
|---|---|
| Recording, fault-injecting mock | serves the provider contract; injects 429/503/malformed/reset/hang; records inbound method + path + body + count |
| Drift engine | mutates the mock's outgoing JSON per drift profile (D2); a no-op unless installed |
| Hermetic gate runner | boots mock + app, runs public + holdout checks, exits pass/fail |
| Quality tool | computes D1–D4 + the D5 extend-check, reusing the gate's boot + HTTP clients |
| Known-good reference + injectable defects | the discrimination-validation anchors (§C.2) |

To point the suite at a new API: build the mock from that API's contract, restate the op table + pinned
request contracts, re-anchor the reference against the new fixtures, and re-lock (§1.6).

### C.2 Discrimination-validation (do this before trusting any score)
For each deterministic instrument, prove **high on reference, low on the matching defect**:

| Instrument | High anchor (≈1.0) | Low anchor (a defect that must drop the score) |
|---|---|---|
| D1 deep-correctness | clean reference | a variant that reads a wrong field / drops list items |
| D2 drift-survival | clean reference (ceiling) | a variant that reads a renamed field by a hardcoded key (→ SILENT-WRONG cells) |
| D3 static-metrics | clean reference | a variant with an artificially convoluted method |
| D4 security | clean reference (0 findings) | a variant that logs a secret / pins a known-CVE dependency |
| Readiness gate (Part A) | clean reference (all green) | variants that leak internals / resend an unsafe write / never time out / return a raw 500 / drop auth / hardcode responses (each reddens its target check) |

Also run the **two-sidedness check** (§1.5): confirm the instruments can surface a win for *each* kind
of integration on a case where it genuinely should win.

### C.3 Statistics (when you have N runs/trees)
A single tree gives a **point score — directional only**. For claims, produce N produced integrations
per condition and:
- **Per condition:** median + IQR with a **bootstrap BCa 95% CI**; mean ± SD secondary.
- **Comparison:** **Mann–Whitney U** (primary) + **Cliff's delta** effect size (|δ| bands: <0.147
  negligible / <0.33 small / <0.474 medium / ≥0.474 large). Welch's t only if a mean claim is made.
- **Reliability:** success rate with CI; **McNemar** on pass/fail; **pass@k / pass^k**.
- **Sample size:** power-analyze from a pilot's variance; **default N = 30** per condition if
  inconclusive. (Deterministic dims D1–D4/D6 are fixed given a tree — their only variance is *between*
  trees, so they need the N-run set; D5/D7 carry their own run/judge variance.)

### C.4 Cost measurement (for agent builds) & honest accounting
- **Never collapse token classes.** Report input / output / cache-read / cache-creation **separately**
  (cache-read bills a small fraction of input; cache-creation a premium — collapsing distorts cost by an
  order of magnitude). **Output tokens** is the least-gameable "work done" proxy and the
  cache-independent arbiter.
- **Capture from more than one source and reconcile:** the runtime's telemetry (e.g. an OpenTelemetry
  token-usage metric, split by class and by source), the run's structured result output, and an
  independent usage-accounting tool. >2% discrepancy on any class → investigate before including the run.
- **Cost-to-DONE = the whole session's cost**, verified green by the evaluator after the session ends —
  not the agent's self-claim. Apply one fixed price table to every condition; pin it in the manifest.
- **Effectiveness-aware cost-per-success = total tokens across ALL runs ÷ successful runs.** Report cost
  and success **jointly** (Pareto). Never average only the runs that reached green.

### C.5 Exclusion criteria (pre-register these)
- **Infrastructure failure → excluded + re-run** (bounded, each logged): registry/network unreachable,
  restore error, mock/collector crash, provider-API 429/5xx overload, or a **flaky** check (fails then
  passes on identical produced bytes).
- **Task failure → counted:** the build doesn't compile, doesn't reach the bar within budget, or the
  harness ran clean and the tree simply isn't green.
- **Ambiguous → counted.** Never silently drop a genuine failure. Rules symmetric across conditions;
  every exclusion reported. (This matters when conditions differ in infra dependence — e.g. one needs a
  package registry and another doesn't.)

### C.6 Blinding (for the D7 judge)
- **Judge family ≠ builder family** (blunts self-preference bias); ensemble of ≥2 families; final =
  median across judges.
- **Blind to which integration** produced the code; anonymize tell-tale package refs / namespaces /
  imports where feasible (acknowledge residual structural tells).
- **Swap-and-average** every pairwise comparison (position bias); report **length alongside** scores
  (verbosity bias); require a **chain-of-thought rubric** (justify each sub-score); **human-calibrate** a
  sample and report judge–human correlation.

### C.7 Manifest (record per run / per scored tree)
> `id · condition · timestamp · tree_hash · model_id+fingerprint · effort · tool_versions ·
> price_table_id · tokens{input,output,cache_read,cache_creation}×source · total_cost · num_turns ·
> wall_clock · DONE · ROBUST · public_results · holdout_results ·
> D1{rate,defects[]} · D2{resilience,safety,confusion,cells[]} · D3{cc,mi,wire_coupling,loc,smells} ·
> D4{vuln_by_severity,cwe,hotspots,dep_count} · D5{cost,output,turns,success}? · D6{mutation,coverage}? ·
> D7{per_judge[],median,variance} · normalized{d1..d7} · composite{value,weight_sensitivity[]} ·
> transcript_path · tree_path`

---

## §7 — SDK & client-library shortcomings this benchmark surfaces

The reason to run the bar against an SDK-based integration: the dimensions where it scores poorly are
the shortcomings to fix. These are the failure modes a generated SDK / SDK-delivery plugin most commonly
exhibits — each paired with the dimension that catches it and a remediation lever. Treat them as
hypotheses to confirm on *your* integration at N (§C.3), not as givens.

| # | Shortcoming | Caught by | How it shows up | Remediation lever (for the SDK/plugin author) |
|---|---|---|---|---|
| 1 | **Navigation / learning cost dominates one-shot build** | Part C cost (output tokens, cache-read) | high build cost driven by reading/searching the SDK surface; cache-read scales with turns × context | Reduce the surface pulled into context. Repackaging the *same* surface (fuller vs. more compact references) tends **not** to help — the cost tracks how much must be read, so shrink and simplify the surface (tighter types, type-server-assisted lookup) rather than re-wrap it. |
| 2 | **Drift brittleness on retype / envelope change** | D2 (Resilience) | a strict typed deserializer 5xxs on an `int→string` retype or an envelope rename that string-tolerant code absorbs | Tolerant deserialization for scalars (accept string-encoded numbers); union/fallback for unexpected shapes; don't hard-fail an unknown enum value. |
| 3 | **Dependency / supply-chain bloat** | D4 (dep count) | the SDK pulls in a transitive dependency graph a hand-rolled build doesn't | Trim transitive dependencies; prefer the standard library over pulled-in helpers; document the graph. |
| 4 | **Rich error system heavier to navigate than to hand-roll** | Part C cost + D7 | mapping errors via a typed error hierarchy (many error types, unions, raw-error fallback) costs the agent *more* than shape-tolerant hand-parsing | Simplify the error surface; ship a one-call "classify this error" helper + error-handling guidance that doesn't require touring the type tree. |
| 5 | **Noisy / awkward generated call sites; auth mistakes** | D7 (blind judge), D3 (nesting) | judges flag call-site noise or awkward ergonomics; auth-passthrough or default-scheme bugs surface | Cleaner call-site ergonomics; sane auth defaults; idiomatic host-framework integration in the calling-code guidance. |
| 6 | **Front-load not amortized** | D5 (extend cost) | the SDK cost more to build but does *not* make the next change cheaper | Make the *first* use cheap (lever #1) and confirm the discount holds for genuinely-new resources, not just recomposition. |

**And the wins to protect (don't regress these while fixing the above):**
- **D3 — cleaner surface:** near-zero wire-coupling, less owned code, shallower nesting. The SDK's
  honest, measurable home win.
- **D5 — cheaper next change:** once the surface is learned, extending should be cheaper than
  hand-rolled. Preserve it; the goal is to cut the front-load (lever #1) *without* losing the discount.

**The through-line:** an SDK's cost tends to be **intrinsic to consuming its surface** (learning the
types/models/errors and re-reading them across a long session), not a packaging artifact — so the
highest-leverage improvements shrink and simplify that surface and cut first-use cost, rather than
re-wrapping the same surface. Its dependable advantages are structural (surface cleanliness, cheaper
follow-on changes), not automatic robustness — so test drift, safety, and supply-chain explicitly rather
than assuming the types deliver them.

---

## §8 — Scope & caveats (state these with any result)
- **Directional, not powered,** until you run Part C.3 at N. Single trees give point scores; agent runs
  have large variance.
- **Single API / app / model / harness.** External validity is limited to what you actually ran; a
  type-server-assisted harness, a different API size, or a different model could shift SDK economics.
- **Naming contamination.** If the provider is named concretely, a "spec-only" or "from-scratch" build
  can draw on latent training knowledge — its cost is a *lower bound* vs a genuinely unknown API.
- **Static metrics reward brevity.** An SDK's hidden wire code isn't "free maintenance," it's *deferred
  to the SDK version* — D2 (drift) and D4 (supply-chain) are the counterweights that price that
  deferral. Read D3 alongside them.
- **The judge (D7) is imperfect.** Anonymization leaks structural tells; deterministic D1–D6 are immune
  and carry the weight.
- **Conflict of interest.** If you author the SDK *and* run the benchmark, §1 (pre-register, lock,
  two-sided, discrimination-validate, report-which-way-it-lands) is what makes the result credible —
  plus full artifact release. Invite independent replication.
