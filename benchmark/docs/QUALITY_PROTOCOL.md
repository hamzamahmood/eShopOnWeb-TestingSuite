# Quality-Measurement Protocol — SDK-vs-Spec Benchmark (Stage 2Q)

> **Status:** PRE-REGISTRATION DRAFT · v0.1 · 2026-07-14 · **to be LOCKED before any arm is scored**
> **Companion to** `PRODUCTION_READINESS.md` (LOCKED v0.3), `TASK_SPEC.md` (LOCKED v0.4),
> `PROTOCOL.md`, and `FINDINGS.md`. This document defines **what "overall quality" means** in this
> experiment and how it is measured — the axes Stage 1 deliberately could not see. It is *additive*:
> it does **not** modify the Stage-1 token gate or its locked docs.
>
> Like the other docs, this is **pre-registered**: dimensions, instruments, normalization anchors,
> weights, and the discrimination-validation plan are frozen (git-committed + content-hashed) **before**
> either arm's artifacts are scored. Changes after seeing quality scores are disallowed (§13) — this
> blocks metric-shopping / HARKing, which matters doubly here because the SDK's author runs the study.

---

## 1. Why this exists

Stage 1 measured **tokens-to-production-ready** and found hand-rolling from the OpenAPI spec (Arm B)
consistently cheaper than the APIMatic SDK (Arm A) at what `FINDINGS.md` §5 calls "quality parity."
**That parity is an artifact of the gate, not a finding about quality.** The Stage-1 gate is, by
deliberate design (`gate/Checks.cs:15-20`), *"status ranges, value-presence, mock request-counts,
coarse timing, and forbidden-substring hygiene. No LLM judge, no code inspection."* The Fairness
Principle (`PRODUCTION_READINESS.md` §5) **bans code inspection** to keep the binary pass/fail
arm-agnostic. That is correct for a *pass/fail* gate — and it makes the gate **structurally blind** to
every axis on which a typed SDK might earn its extra tokens.

`FINDINGS.md` §7 names those blind axes precisely:

> *"The SDK's real value axes are untested here: correctness under API drift, long-term
> maintainability, human developer speed/ergonomics, and safety of the generated code. A fair case
> **for** the SDK would measure those, not one-shot agent tokens."*

This protocol measures those axes.

### 1.1 The result must be two-sided

The point is an **honest** measurement, reported whichever way it lands — not a search for an SDK win.
Inspection of the produced trees (`runs/scope22-arm{A,B}/workspace/`) already establishes the
differences are **genuinely two-sided**:

| Signal | Arm A (SDK) | Arm B (hand-rolled) |
|---|---|---|
| Type safety | `int` IDs + `{:int}` route constraints, string-enums, `TryGet` unions | stringly-typed IDs, raw-string enums |
| Wire-contract code owned | ~0 lines | ~590 lines (DTOs + URL/query literals) |
| Field-rename tolerance | absorbed by SDK/union types | silent mis-map to `null` |
| **Cancellation precision** | **blanket → HTTP 504 (loses)** | **client-abort → HTTP 499 (wins)** |
| Convention fit | implements eShopOnWeb `IEndpoint` | one ad-hoc 220-line file |
| Supply-chain surface | adds a dependency graph (loses) | none |

The **cancellation-precision** row (Arm B wins) is the protocol's **litmus test**: any instrument that
*cannot* surface a genuine Arm-B advantage is biased and must be reworked (§9, verification #2).

---

## 2. Design principles (inherited from the benchmark, adapted for quality)

1. **Deterministic-first; LLM-judge as a labelled, low-trust supplement.** Every dimension that can be
   a tool-computed number is (D1–D6). The subjective judge (D7) is never the headline. (Mirrors
   `PRODUCTION_READINESS.md` §5 corollary "deterministic-first verification.")
2. **Discrimination-validated instruments.** Every instrument must be proven to score the known-good
   `reference/` **high** and a deliberately-defective `BREAK=` variant **low** *before* any arm is
   scored — the exact self-test discipline that validates the Stage-1 gate ("green on reference,
   reddens on injected defects", `FINDINGS.md` §1). An instrument that can't separate good from bad is
   not shipped.
3. **Two-sided fairness (re-cast).** Stage 1 banned code inspection to keep pass/fail arm-agnostic.
   Quality measurement *requires* inspecting the artifact, so the principle becomes: **measure genuine,
   universal quality properties any good integration has — never "did you use the SDK."** See §1.1.
4. **Blind LLM judging.** Judges are a **different model family than the arm under test** (both arms are
   Claude ⇒ judges are non-Claude), **blind to arm identity**, artifacts **anonymized** (SDK package
   refs / namespaces stripped where feasible), **order-randomized**, an **ensemble of ≥2 families**,
   with a **chain-of-thought rubric** and **human calibration** on a sample. Standard mitigations for
   position, verbosity, and self-preference bias (§8, refs [4][5][6]).
5. **Pre-registration + scorecard-first.** Lock this doc before scoring. Report a **per-dimension
   scorecard/radar** as the headline; a weighted composite is **secondary** and always shipped with a
   **weight-sensitivity analysis** — never a single false-precision number.
6. **Reuse, don't reinvent.** Extend the existing mock, gate check-patterns, reference, harness, and
   the `PROTOCOL.md` §8 statistics.

---

## 3. The quality model

Anchored to **ISO/IEC 25010:2023** system/software product-quality characteristics [1][2]. Seven
dimensions in three tiers.

| Dim | ISO 25010 characteristic | Tier | Oracle | Runs on |
|---|---|---|---|---|
| **D1** Correctness depth | Functional Suitability (completeness, correctness) | 1 | deterministic | existing trees |
| **D2** API-drift resilience | Reliability (fault tolerance) + Flexibility | 1 · **crown jewel** | deterministic | existing trees |
| **D3** Maintainability | Maintainability (analysability, modifiability, modularity) | 1 | deterministic (static) | source |
| **D4** Security depth | Security | 1 | deterministic (scanners) | source + running app |
| **D5** Modifiability / dev-speed | Maintainability (modifiability) | 2 (expensive) | empirical (agent runs) | fresh runs |
| **D6** Test quality | Maintainability (testability) | 2 (conditional) | deterministic (mutation) | source + tests |
| **D7** Readability / idiomaticity | — (subjective) | 3 (low-trust) | LLM-judge ensemble | anonymized source |

### D1 — Correctness depth  ·  Functional Suitability
**Gap:** Stage-1 `C1` asserts only that a required value appears *somewhere* in the body
(`Checks.cs:43` `Has`). It never checks the value sits in a semantically-right field, numeric **units**,
all documented fields, or list/empty/pagination behavior.
**Instrument:** `quality/deep-correctness/` — a "C1-deep" check-set reusing `gate/Clients.cs`
(`AppClient`/`MockClient`). Stays field-name-agnostic (fairness) but adds:
- **Right value, right place** — the created-subscription id appears in an id-shaped position, not
  merely anywhere; the state value co-locates with the subscription it belongs to.
- **Numeric units/magnitude** — Pro plan is `price_in_cents = 29900` (fixture); the app must surface a
  value consistent with **$299.00** — neither a raw `29900` dollars nor a double-divided `2.99`. This
  is the real cents-vs-dollars trap, tested as an internal-consistency range, not an exact encoding.
- **List cardinality** — plans returns **2** items (700001 **and** 700002), not 1; components returns 2.
- **Empty & unknown** — a list op on an id with no children returns an empty collection (not an error);
  an unknown id returns a 4xx of the right shape.
**Metric:** deep-correctness pass-rate ∈ [0,1] + a per-op defect list. **Underpins D2**: a precise
oracle is what lets D2 detect *silent* corruption (2xx-but-wrong).
**Discrimination fixture:** new reference toggle `BREAK=shallowmap` (surfaces a right-typed but wrong
field / drops list items) must score low; clean reference scores 1.0.

### D2 — API-drift resilience  ·  Reliability + Flexibility  ·  **crown jewel**
**Hypothesis under test:** a typed SDK absorbs upstream schema evolution that breaks hand-mapped JSON —
the strongest candidate for a genuine SDK win. **Also capable of the reverse** (a `StringEnum` may
*reject* a new enum value the hand-rolled string passes through — an Arm B win).
**Instrument:** extend `mock/FaultEngine.cs` + `mock/Program.cs` with **response-mutation "drift
profiles"** — deterministic transforms applied to the mock's *outgoing* Maxio JSON (after the route
builds it, before send), scoped per op, installed via `/__mock/config` exactly like today's faults.
**No arm code is touched**: we replay the already-produced trees against a drifted upstream. See §4 for
the profile catalogue and per-op targets.
**Metric:** for each `(op × profile)` cell, classify the app's response:

| Class | Meaning | Score |
|---|---|---:|
| **CORRECT** | right data still returned | 1.0 |
| **GRACEFUL** | clean 4xx or empty, no crash, no leak | 0.5 |
| **BROKEN** | 5xx / crash / hang / internals leaked | 0.0 |
| **SILENT-WRONG** | 2xx but blank/incorrect data (caught by D1's oracle) | 0.0 ⚠ |

**Drift-survival score** = mean cell score. Always reported **with the full 4-way confusion matrix**;
**SILENT-WRONG is the worst outcome** (a corrupt bill that looks fine) and is called out separately, not
folded silently into 0.
**Discrimination fixture:** `BREAK=brittlemap` (a reference variant that reads one renamed field by a
hardcoded key) must drop specific cells to SILENT-WRONG; clean reference sets the achievable ceiling.

### D3 — Maintainability  ·  Maintainability
**Instrument:** `quality/static-metrics/` over each tree's integration files only (the `Billing`/`Maxio`
folders + the `/api/billing` endpoints), *not* the vanilla eShopOnWeb baseline:
- **Cyclomatic complexity + Maintainability Index** — `Microsoft.CodeAnalysis.Metrics` (Roslyn;
  `dotnet build /t:Metrics`) or **Roslynator CLI**. MI 0–100; CC flagged >10 (analyzer CA1502) [3].
- **Wire-coupling count** (the sharpest, most objective signal) — a scripted count of hand-maintained
  wire artifacts: literal `*.json` URL fragments, snake_case envelope field names in code, query-param
  spellings, manual base64-auth construction. Arm B ≈ 223 DTO lines + ~30 URL/query literals + 2
  field-ambiguity fallbacks (`(c.Id ?? c.ComponentId)`); Arm A ≈ 0.
- **Integrator-owned LOC** of the integration layer.
- **Code-smell density** — Roslyn/.NET analyzers via a pinned `.editorconfig` + `dotnet build`, and
  **SonarScanner for .NET** (SonarCloud community, free) [7]. Recent evidence links LLM-generated code to
  elevated code-smell rates [8], making smell density a first-class maintainability proxy.
**Metric:** each metric reported individually + a normalized maintainability sub-score (§5).
**Two-sided:** Arm A's verbose positional list-call arg lists raise its CC; Arm B's owned wire-LOC and
manual ambiguity fallbacks raise its coupling/smell counts.
**Discrimination fixture:** `BREAK=complex` (reference variant with an artificially convoluted method)
must score low on CC/MI.

### D4 — Security depth  ·  Security
**Gap:** gate has only S1 (secret-not-logged), S2 (auth-applied), S3 (fail-fast).
**Instrument:** `quality/security/`:
- **Supply-chain surface** — `dotnet list package --vulnerable --include-transitive` + transitive
  dependency count. **The SDK adds a dependency graph Arm B doesn't** — a real, measurable *con* for
  Arm A. Two-sided by construction.
- **Static security scan** — Security Code Scan analyzer and/or **CodeQL** (C#) for CWE patterns;
  SonarCloud security hotspots [9]. (Frameworks like CWEval show most LLM code is functionally correct
  yet a meaningful fraction is insecure [9] — worth checking on both arms.)
- **Expanded leak / auth** — a larger forbidden-substring set across more error paths than the gate's
  11-item list (`Checks.cs:29-33`); assert the auth *scheme* is correct (Basic `Base64("{key}:x")`),
  not merely present.
**Metric:** vulnerability count by severity + CWE hits + hotspot count (lower is better), per category.
**Discrimination fixtures:** reuse `BREAK=logsecret` (secret leak) + new `BREAK=vulndep` (pins a known-
CVE package) — both must raise the security count; clean reference scores 0 findings.

### D5 — Modifiability / human-dev-speed  ·  Maintainability (modifiability)  ·  Tier-2
**Operationalizes** "human developer speed/ergonomics" as **agent-effort-to-extend**: give a *fresh*
Claude Code agent each produced tree + a small, pre-registered extension task (e.g. **"add op #23: apply
an existing coupon to a subscription — `POST /subscriptions/{id}/apply-coupon`"**) and measure
**cost/output-tokens/turns/success** to make a small extension gate green. Reuses the Stage-1 token rig
(`harness/run-arm.ps1` pattern, `-p --output-format json`). Interleaved A/B, same model/effort, per
`PROTOCOL.md` §3/§6.
**Metric:** extension cost (USD + output tokens) and success rate per arm. Tests whether the SDK's
structure makes *the next change* cheaper — a different question than one-shot build cost, and the
fairest place for the SDK to win.
**Note:** this axis *does* spend agent tokens; it is the one place quality and cost re-converge.

### D6 — Test quality  ·  Maintainability (testability)  ·  Tier-2 (conditional)
Did the arm write tests for its own integration? If yes: **mutation score via Stryker.NET**
(`dotnet stryker`) [10] over the integration + line/branch coverage. If neither arm wrote tests (likely —
`TASK_SPEC.md` does not require them), report coverage = 0 for both as a **finding**, not a forced
metric. Conditional on there being tests to measure.

### D7 — Readability / idiomaticity / design  ·  subjective  ·  Tier-3 (low-trust)
For what objective metrics can't capture (naming, cohesion, idiomatic C#, comment quality). Full
bias-control apparatus (§8). **Explicitly the lowest-trust dimension** — reported with inter-judge
variance and wide error bars, and **never** the headline.

---

## 4. Drift-profile catalogue (D2)

Each profile is a deterministic JSON transform the mock applies to a matched op's outgoing body. The
**method** is locked here; the **exact per-op field targets** are finalized against `mock/MockStore.cs`
at Phase-1 build time and frozen in **Appendix A of this doc before any arm is scored** (§13).

| # | Profile | Transform | Concrete example target (from `MockStore.cs`) | Primarily tests |
|---|---|---|---|---|
| P1 | additive-field | inject unknown scalar + nested object | add `experimental_flags:{}` to `product` | forward-compat (⊇ Stage-1 C2) |
| P2 | field-rename | rename a leaf the app reads | `state` → `sub_state` on subscription (op 4); `allocated_quantity` → `allocated_qty` (op 17) | silent mis-map vs tolerance |
| P3 | retype-scalar | change a scalar's JSON type | `id` int → `"700001"` string on products (op 1) | deserialization tolerance |
| P4 | scalar→union | a scalar becomes an object/union | `quantity` number → `{"value":5}` on usage/allocation | union handling (`TryGet` vs throw) |
| P5 | new-enum-value | emit an unmodeled enum value | subscription `state` → `paused_pending` (ops 3,4) | **StringEnum brittleness** (candidate Arm B win) |
| P6 | envelope-rename | rename/re-nest the wrapper key | `{"product":…}` → `{"plan":…}` in products list (op 1) | envelope coupling |
| P7 | error-shape | change the error body shape on a failure path | field-map `{"errors":{"base":[…]}}` / bare string / `<html>` (the reverted v0.4 mutations, repurposed) | error-parse robustness |
| P8 | field-removal | drop a non-critical field | remove `updated_at` / an optional field | graceful degradation |

Profiles apply **one at a time** (isolated cells) so a failure attributes to a single drift. The op set
is the 22 of `TASK_SPEC.md` §3; not every profile applies to every op (e.g. P5 only where an enum
exists) — the applicable `(op × profile)` matrix is enumerated in Appendix A.

---

## 5. Scoring & normalization

- **Per-dimension normalization to [0,1]** against **pre-registered anchors**: the known-good
  `reference` (no BREAK) sets the **high anchor (1.0)**; the matching `BREAK` variant sets the **low
  anchor (0.0)**. A raw Maintainability Index of 72 becomes an interpretable fraction of the achievable
  range, not a bare number. Anchors are computed and **frozen before** scoring any arm.
- **Direction handling:** for "lower-is-better" metrics (CC, vuln count, wire-coupling) the normalization
  inverts so 1.0 always means "better." Stated per metric in Appendix B.
- **Scorecard-first.** The headline is a **radar/table of the 7 dimensions per arm**. A **weighted
  composite** (default equal weights across the four Tier-1 dimensions; Tier-2/3 reported but excluded
  from the default composite) is **secondary** and always accompanied by a **weight-sensitivity
  analysis** — the arm ranking shown under several defensible weightings.
- **No single number as the headline.** If one is quoted at all, it carries its sensitivity band.

---

## 6. Statistics (reuse `PROTOCOL.md` §8)

- **Per dimension, per arm:** median + IQR with a **bootstrap BCa 95% CI**; mean ± SD secondary.
- **Arm comparison, per dimension:** **Mann–Whitney U** (primary) + **Cliff's delta** effect size
  (|δ| bands: <0.147 negligible / <0.33 small / <0.474 medium / ≥0.474 large).
- **Applicability:** deterministic dimensions (D1–D4, D6) are **fixed given a tree** — their only
  variance is *between* the N produced trees per arm, so statistics need the Phase-4 N-run set. On the
  existing 8 trees (n=1/cell) report **point scores, directional only** (mirrors the Stage-1 pilot
  stance). D5/D7 carry their own run/judge variance.
- **Small-n honesty preserved:** direction over magnitude until Phase 4 supplies CIs.

---

## 7. Instrument discrimination-validation (the "gate on the gate")

Before **any** arm tree is scored, each deterministic instrument must be shown to separate good from bad
on the reference, and every self-test committed:

| Instrument | High anchor (must score ≈1.0) | Low anchor (must score low) |
|---|---|---|
| D1 deep-correctness | `reference` no-BREAK | `BREAK=shallowmap` |
| D2 drift-survival | `reference` no-BREAK (ceiling) | `BREAK=brittlemap` (specific SILENT-WRONG cells) |
| D3 static-metrics | `reference` no-BREAK | `BREAK=complex` |
| D4 security | `reference` no-BREAK (0 findings) | `BREAK=logsecret`, `BREAK=vulndep` |

New reference toggles (`shallowmap`, `brittlemap`, `complex`, `vulndep`) are added to
`reference/MaxioClient.cs`'s `Breaks` record and `reference/Program.cs`, mirroring the existing 7-flag
catalogue (`leak`/`retrywrite`/`notimeout`/`raw500`/`noauth`/`logsecret`/`hardcode`).

---

## 8. Blinding & LLM-judge protocol (D7)

- **Judge family ≠ arm family.** Both arms are built by Claude ⇒ judges are **non-Claude**, an
  **ensemble of ≥2 distinct families**, to blunt self-preference bias [4]. Final D7 = median across judges.
- **Blind to arm.** Judges never learn which arm produced the code; artifacts are **anonymized** (SDK
  package references, `AsadAli.*`/`MaxioAdvancedBilling` namespaces, and tell-tale using-directives
  neutralized where feasible). Residual structural tells are acknowledged as a limitation.
- **Position bias** [5]: every pairwise comparison is run **both orders** and averaged (swap-and-average).
- **Verbosity bias** [6]: rubric scores dimensions explicitly (naming, cohesion, idiom, comments), and
  length is reported alongside so a "longer ⇒ better" artifact is visible.
- **Chain-of-thought rubric:** the judge must justify each sub-score before emitting it.
- **Human calibration:** a small sample of artifacts is human-rated; report judge–human correlation.
- **Reporting:** inter-judge agreement + variance always shown; D7 never drives the headline.

---

## 9. Verification (before trusting any arm score)

1. **Discrimination self-test** (§7) green for every instrument.
2. **Two-sidedness check:** on the existing trees, confirm the instruments surface **both** known facts —
   Arm A wins D2 rename/retype cells and D3 wire-coupling; **Arm B wins D1's cancellation-precision cell**
   and D4's supply-chain (fewer deps). An instrument that can only favor one arm is biased and reworked.
3. **Determinism:** D1–D4/D6 give identical scores on repeated runs of the same tree (pin tool versions;
   commit the protocol content-hash + Appendix A/B).
4. **End-to-end dry run** of Phase 2 on `runs/scope22-arm{A,B}` producing a real scorecard before
   committing Phase-4 tokens.
5. **Judge calibration** (§8) reported.

---

## 10. Execution phases

| Phase | What | Cost | Output |
|---|---|---|---|
| 0 | Write + **lock** this doc (incl. Appendix A/B) | tiny | pre-registered protocol |
| 1 | Build + discrimination-validate D1–D4 instruments; extend mock (drift) + reference (BREAK) | low (no agent) | validated instruments |
| 2 | Score the 8 existing trees on D1–D4 | ~zero agent | directional scorecard |
| 3 | D7 (blind judge) + D5 (extend-runs) + D6 (mutation, if any) | moderate (D5 agent) | full dimension set |
| 4 | N≈15–30 fresh interleaved runs; all dimensions + CIs | high (agent) | statistically-powered result |

Each phase is independently valuable; the Phase-2 scorecard already answers *"does the SDK win on
quality anywhere?"* before Phase-4 spend.

---

## 11. Credibility safeguards & threats to validity

- **Pre-registration** (this doc + Appendices, hashed before scoring) — blocks metric-shopping/HARKing.
- **Two-sided by construction** (§1.1, §9#2) — the design must be able to show either arm winning.
- **Discrimination-validated instruments** (§7) — no unvalidated metric ships.
- **Blinding** for the only subjective dimension (§8).
- **COI disclosed:** APIMatic authors the `maxio-sdk` plugin **and** runs this study — mitigated by the
  above and by full artifact release (scores, instrument source, produced trees).
- **Threats:** (a) **anonymization is imperfect** — the SDK's structural fingerprint may survive, biasing
  D7; deterministic D1–D6 are immune. (b) **static metrics reward brevity** — Arm A's SDK-hidden wire
  code isn't "free maintenance," it's *deferred to the SDK version*; D2 (drift) and D4 (supply-chain) are
  the counterweights that price that deferral. (c) **mock ≠ live Maxio** — drift profiles model plausible,
  not observed, evolution. (d) **n=1 per cell** on existing trees until Phase 4. (e) **single model/
  harness** — a type-server-assisted harness could change Arm A's economics (`FINDINGS.md` §6).

---

## 12. Data recorded per scored tree (quality manifest schema)

`tree_id · arm · commit/tree_hash · tool_versions{roslyn,sonar,stryker,dotnet} · protocol_hash ·
D1{pass_rate,defects[]} · D2{survival,confusion{correct,graceful,broken,silent_wrong}, cells[]} ·
D3{cc,mi,wire_coupling,owned_loc,smell_density} · D4{vuln_by_severity,cwe_hits,hotspots,dep_count} ·
D5{cost_usd,output_tokens,turns,success}? · D6{mutation_score,coverage}? · D7{per_judge[],median,variance} ·
normalized{d1..d7} · composite{value,weight_sensitivity[]}`

---

## 13. Change control

Frozen at lock time. Any change requires a version bump + a dated rationale entry here. Changes after
seeing quality scores are disallowed unless they *loosen*/neutralize an instrument symmetrically for both
arms and are disclosed. **Appendix A** (per-op × per-profile drift targets) and **Appendix B**
(per-metric normalization directions + anchor values) are authored in Phase 1 and **locked before Phase
2 scoring**.

- v0.1 — 2026-07-14 — initial pre-registration draft — pending Phase-1 appendices, then lock.

---

## 14. Companion documents & references

**Companions:** `PRODUCTION_READINESS.md`, `TASK_SPEC.md`, `PROTOCOL.md`, `FINDINGS.md`; the new
`quality/` instrument suite; extended `mock/` (drift) and `reference/` (quality BREAK fixtures).

**References (methodology grounding):**
1. ISO/IEC 25010:2023 — Product quality model. https://www.iso.org/obp/ui/en/#!iso:std:78176:en
2. ISO 25010 characteristics overview — arc42 Quality Model. https://quality.arc42.org/standards/iso-25010
3. Code metrics — Cyclomatic complexity (Microsoft Learn). https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-cyclomatic-complexity
4. Self-Preference Bias in LLM-as-a-Judge (arXiv 2410.21819). https://arxiv.org/abs/2410.21819
5. Position bias in rubric-based LLM-as-a-judge (arXiv). https://arxiv.org/abs/2602.02219
6. Justice or Prejudice? Quantifying Biases in LLM-as-a-Judge (arXiv 2410.02736). https://arxiv.org/abs/2410.02736
7. SonarScanner for .NET / cyclomatic complexity (Sonar). https://www.sonarsource.com/resources/library/cyclomatic-complexity/
8. Investigating the Smells of LLM-Generated Code (arXiv 2510.03029). https://arxiv.org/abs/2510.03029
9. CWEval — outcome-driven functionality+security eval of LLM code. https://arxiv.org/abs/2508.18106 (A.S.E, repo-level security)
10. Stryker.NET — mutation testing for .NET. https://stryker-mutator.io/docs/stryker-net/introduction/
