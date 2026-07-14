# Quality Findings — SDK-vs-Spec Benchmark (Stage 2Q)

> **Status:** Stage-2Q complete (deterministic + judge + D5 extend axes) · 2026-07-14
> **n = 5 Arm A (SDK) / 3 Arm B (spec) produced trees; D5 = 1 paid extend run per arm** — directional, not statistically powered.
> Companion to `QUALITY_PROTOCOL.md` (the pre-registered method) and Stage-1 `FINDINGS.md` (token cost).

---

## TL;DR

Stage 1 showed the SDK arm costs **more tokens** to reach the production-ready bar, at what the binary
gate called "quality parity." That parity was an artifact: the gate is binary, black-box, and bans code
inspection, so it is blind to the axes where a typed SDK might earn its cost. Stage 2Q measures those
axes directly — correctness depth, API-drift resilience, maintainability, security — plus a blind
code-quality judge, on the **already-produced integration trees** (no new agent tokens).

**The result is a genuine split, not an SDK win.** Neither approach dominates:

- **The SDK (Arm A) wins, decisively, on structural cleanliness:** it hand-maintains **zero** wire
  contract (wire-coupling 0 vs 13–24) and has shallower nesting (max 4 vs 6).
- **Hand-rolling from the spec (Arm B) wins, decisively, on drift resilience** (survival 54% vs 41%)
  **and dependency leanness** (90 vs 96 transitive deps); and — per **two independent blind judges** —
  edges ahead on error-handling execution (4.75 vs 4.0 / 5).
- **Correctness depth, failure-safety, average complexity, source-security, and tests-written are at
  parity** (including: *both arms wrote zero tests for their integration*).
- **The SDK (Arm A) wins the one axis where cost and quality re-converge — extending the integration.**
  Asked to add one new endpoint to the already-built tree, the SDK arm was cheaper on every metric
  (**$0.83 vs $0.93**, 18 vs 20 turns, cacheRead **0.52M vs 0.78M**). This *reverses* the build-phase
  pattern (where the SDK arm cost +53% at 22 ops): expensive to learn, cheaper to extend once learned.
  The margin (~11%) sits inside the study's large run-to-run variance, so it is **directional, not
  proof** — but it is the first result that points the SDK's way, and it points exactly where the
  amortization thesis predicts.

So measuring "overall quality" does **not** rescue the SDK's case the way one might expect — it refines
it. The SDK's genuine, measurable value here is a **cleaner surface** (no wire code to maintain) and a
**cheaper next change** once the surface is learned — not correctness, robustness, or safety, on which
it is at parity or behind. The learning cost is front-loaded (Stage 1); whether it pays back turns on
how much you extend afterward, and on the non-token value (maintainability, drift behavior) the token
gate can't see. This is consistent with the whole Stage-1 arc: the SDK's advantages are real but
narrower than the pitch.

---

## 1. What was measured

An **additive** quality layer (it does not touch the Stage-1 token gate). Seven ISO/IEC 25010-anchored
dimensions, deterministic-first, run on the produced trees in `benchmark/runs/`:

| Dim | ISO 25010 | Instrument | Oracle |
|---|---|---|---|
| D1 correctness depth | Functional suitability | `quality/` deep-correctness (id+state+plan co-location, price faithfulness, list cardinality, unknown-id) | deterministic |
| D2 drift resilience | Reliability / flexibility | replay tree vs the **drift-mutating mock** over an (op×profile) matrix | deterministic |
| D3 maintainability | Maintainability | Roslyn CC/nesting/LOC + **wire-coupling count** | deterministic |
| D4 security | Security | transitive-dep + `dotnet list --vulnerable` + source scan | deterministic |
| D5 dev-speed | Maintainability (modifiability) | agent-effort-to-extend (fresh paid runs) | empirical — **executed, see §2/§3** |
| D6 test quality | Maintainability (testability) | mutation testing (if tests exist) | deterministic |
| D7 readability/design | (subjective) | blind ensemble code-quality judge | LLM-judge |

Instruments were **discrimination-validated** before scoring: D1 scores the known-good reference 100%
and a `BREAK=shallowmap` variant 90%; D2 classifies each cell CORRECT/GRACEFUL/SILENT-WRONG/BROKEN with
a verified per-cell mechanism; the two excluded Stage-1 infra-stall trees correctly score D1 ≈ 17%
(incomplete integrations), confirming the instruments are not blind.

---

## 2. The scorecard

Medians across DONE trees (Arm A/SDK n=5, Arm B/spec n=3); Cliff's delta as the small-n effect size
(magnitude bands per `PROTOCOL.md` §8). "Favors" accounts for higher-vs-lower-is-better direction.

| Dimension | Arm A (SDK) | Arm B (spec) | Effect (Cliff's δ) | Favors |
|---|---:|---:|:--:|:--:|
| D1 correctness-depth rate | 100% | 100% | small | ~parity |
| **D2 drift resilience** | 41% | **54%** | **large** | **Arm B** |
| D2 failure-safety | 64% | 62% | small | Arm A |
| D2 silent-wrong count (↓) | 8 | 5 | negligible | ~parity |
| **D3 wire-coupling (↓)** | **0** | 19 | **large** | **Arm A** |
| D3 avg cyclomatic (↓) | 2.33 | 2.16 | negligible | ~parity |
| **D3 max nesting (↓)** | **4** | 6 | **large** | **Arm A** |
| D3 owned integration LOC (↓) | 776 | 968 | — | Arm A |
| **D4 transitive deps (↓)** | 96 | **90** | **large** | **Arm B** |
| D4 vulnerable pkgs (↓, baseline-dominated) | 4 | 5 | — | ~parity |
| D4 source-security findings (↓) | 0 | 0 | — | parity |
| **D5 extend cost, USD (↓)** | **$0.83** | $0.93 | — (n=1/arm) | **Arm A** |
| D5 extend turns (↓) | 18 | 20 | — (n=1/arm) | Arm A |
| D5 extend cacheRead (↓) | 0.52M | 0.78M | — (n=1/arm) | Arm A |
| D6 integration tests written | 0 | 0 | — | parity |
| **D7 blind-judge design (1–5)** | 4.0 | **4.75** | — | **Arm B** |

Large-effect results (|δ| = 1.0, non-overlapping ranges) are robust to the small n: **Arm A always
wire-coupling 0 and nesting ≤ 5; Arm B always drift-resilience ≥ 50% and deps ≤ 90.** The **D5 rows are
n = 1 per arm** (one paid extend run each) — the ~11% cost gap is within the study's run-to-run variance
(Arm A build cost ranged $8.50–$15.94 across runs), so D5 is read as **directional**, not a robust
effect.

---

## 3. Per-dimension detail

**D1 — correctness depth (parity).** Every completed integration surfaces the right values in the right
place, faithful price magnitude (cents or dollars), and full list cardinality. Only `scope22lean-armA`
dipped to 90% (one op). Deepening the gate beyond "value appears somewhere" did **not** separate the
arms — both are genuinely correct.

**D2 — API-drift resilience (Arm B wins resilience; Arm A safer failure mode).** This is the axis the
SDK was *most* expected to win, and it does not. Replaying each tree against a schema-drifted upstream:
- **Arm B (hand-rolled) is more resilient** (median 54% vs 41%). Its `AllowReadingFromString`
  deserialization shrugs off `int→string` type drift that the SDK's strict typed `int` **rejects with a
  502**. Both tolerate additive fields and unknown enum values.
- **Arm A (SDK) has the safer failure mode** (64% vs 62%): when it breaks it fails **loud** (a 502 you
  can detect) rather than silently returning a blank `200`. Hand-rolling fails silent more often
  (nullable POCO → blank body) — dangerous for billing, but it *breaks* on fewer drifts overall.
- Net: **a tradeoff, not a win.** Neither is robust to a hard rename of a field it reads (both
  SILENT-WRONG). The SDK's strict typing is a double-edged sword: safer *mode*, lower *resilience*.

**D3 — maintainability (Arm A wins).** The SDK's clearest, most consistent advantage:
- **Wire-coupling 0 (Arm A) vs 13–24 (Arm B)** across every tree. The hand-rolled arm owns literal
  `.json` URLs, snake_case field names, and base64-auth construction — every one a maintenance point
  and a silent-drift risk; the SDK hides all of it.
- Shallower nesting (4 vs 6) and less owned code (≈776 vs ≈968 LOC). Average cyclomatic complexity is a
  wash (2.33 vs 2.16); the SDK's lean/split variants have a few high-CC methods (max 21) from verbose
  positional-argument call sites.

**D4 — security (Arm B leaner supply chain; else parity).** Neither arm has source-level security
findings (no hardcoded secrets, no disabled TLS). The SDK adds **~6 transitive dependencies** (96 vs 90)
— a real, if modest, supply-chain surface expansion. The vulnerable-package counts (4 vs 5) are
dominated by the *shared* pinned eShopOnWeb baseline, so they are not discriminating.

**D5 — dev-speed to extend (Arm A, directionally — the amortization signal).** A *fresh* Opus agent was
given each 22-op tree and asked to add exactly one new endpoint —
`GET /api/billing/subscriptions/{id}/summary`, composing the subscription-read and invoice-list ops the
arm already built — then iterate against a deterministic extend gate (200 + state `active` + invoice
`inv_abc001` + a genuine upstream sub-read and invoice-list call). Both passed cleanly on the first
gate check; the Arm A implementation genuinely composed its two existing SDK-backed service methods
(`GetSubscriptionAsync` + `ListSubscriptionInvoicesAsync`, each still wrapped by the tree's own
error-translating `CallAsync`), and Arm B reused its hand-rolled client the same way.

| | extended | cost | turns | output tok | cacheRead | cacheCreation |
|---|:--:|--:|--:|--:|--:|--:|
| **Arm A (SDK)** | ✅ | **$0.83** | **18** | **6,314** | **0.52M** | 41.1k |
| Arm B (spec) | ✅ | $0.93 | 20 | 6,634 | 0.78M | 37.3k |

The SDK arm was cheaper on every metric, led by **cacheRead −33%** — it re-read far less context to make
the change, because the types and wiring were already in the tree. This is the exact mirror of the build
phase, where the SDK arm carried ~2.2× the cacheRead and cost +53%. The reading: **the SDK front-loads a
learning cost (Stage 1) and discounts the next change (D5).** Two honest limits keep this directional,
not decisive: (1) **n = 1 per arm**, and ~11% is inside the study's run-to-run variance; (2) this is a
*compose-existing-ops* extension — the SDK's best case, since the surface was already learned. A
brand-new resource family (new controllers/models/error types to discover) could swing back toward the
build-phase pattern. Even taking the ~$0.10/change saving at face value against the ~$4.57 build premium
at 22 ops, on **pure agent tokens** it would take of order *tens* of such extensions to break even — so
D5 does not overturn Stage 1's cost verdict; it identifies the mechanism (amortization) and the one axis
where the SDK's structure demonstrably helps.

**D6 — test quality (parity at zero).** **Neither arm wrote any tests for its integration** (only the
stock eShop test projects exist). Mutation testing is therefore N/A. That both frontier-agent builds
shipped an untested billing integration is itself a finding — and orthogonal to SDK-vs-spec.

**D7 — readability/design (Arm B, per two blind judges).** A blind ensemble (two Claude judges,
**opposite label orders** to control position bias; anonymized-approach instructions) both scored the
**hand-rolled arm higher** — Judge 1 (Opus) 4.5 vs 4.0; Judge 2 (Sonnet) 5 vs 4. Both independently
cited the same concrete reasons: Arm B correctly maps upstream 401/403 to a 502 (not leaking a
server-side auth misconfig to callers) and distinguishes caller-cancellation from timeout, with quieter
call sites; Arm A's SDK call sites are noisier (positional-null walls, union-read helpers) and it has a
real error-mapping defect (401/403 passthrough; blanket `catch→Malformed`). Both were judged
"production-grade." *Caveat: judges are Claude; since both arms are Claude-built this is directionally
fair for the A-vs-B comparison but absolute scores may be inflated — non-Claude confirmation is deferred.*

---

## 4. Interpretation

The token gate's "quality parity" was too coarse. The real picture is a **split scorecard**:

- The SDK buys a **cleaner surface** — zero wire-contract code, shallower structure. That is genuine
  maintainability value a spec-only build cannot match, and it is the SDK's honest win.
- The SDK also buys a **cheaper next change** (D5): once the surface is learned, extending the
  integration cost less on every metric (cacheRead −33%), reversing the build-phase pattern. This is the
  amortization the SDK's pitch promises — real, but small in absolute terms (n=1, ~11%, inside variance)
  and only demonstrated for a compose-existing-ops extension.
- But it does **not** buy correctness, drift-robustness, failure-safety, or (per the judges) better
  error-handling — it is at parity or *behind* on those. Its strict typing even *reduces* drift
  resilience (loud 502s on type/envelope drift the hand-rolled arm absorbs), and it enlarges the
  dependency surface.

Combined with Stage 1 (SDK ≈ 1.5–1.9× the tokens to build): on this API, at this size, with this agent,
the SDK's value proposition is **"less wire code to maintain and a cheaper next change," not "more
correct, robust, or cheaper to stand up."** The learning cost is front-loaded; D5 shows it discounts
subsequent edits, but on pure agent tokens the discount is small relative to the build premium. Whether
that trade — cleaner surface + cheaper edits, at ~1.6× the build tokens + ~6 extra dependencies + lower
drift resilience — is worth it is a judgment call this benchmark now lets you make on evidence rather
than intuition.

---

## 5. Caveats & threats to validity

- **Small n, directional only.** 5 Arm A / 3 Arm B trees; no inferential p-values (Cliff's delta only).
  The large-effect results (wire-coupling, drift resilience, deps, nesting) have non-overlapping ranges
  and are robust; the small/negligible ones (D1, safety, avgCC, silent-wrong) are within noise.
- **Arm A pool includes delivery variants.** 3 of the 5 Arm A trees are the full/lean/split delivery
  variants from Stage 1 (same SDK approach, different reference delivery). They are correlated, not
  independent. The matched **scope22-armA vs scope22-armB** pair alone tells the same story
  (wire-coupling 0 vs 24; resilience 41% vs 50%; deps 96 vs 90).
- **Mock, not live Maxio.** Drift profiles model *plausible* schema evolution, not observed. The
  (op×profile) matrix is a designed sample; a different mix would shift the aggregate D2 number (the
  per-cell mechanism, reported alongside, does not depend on the mix).
- **Judge is Claude.** Directionally fair for A-vs-B (both arms Claude-built ⇒ self-preference is
  non-directional), but non-Claude confirmation and human calibration are deferred.
- **Naming contamination & single model/harness** — inherited from Stage 1 (`FINDINGS.md` §6).

### 5.1 Independent instrument audit

The instruments were independently audited for correctness and A-vs-B bias before this write-up was
finalized. It confirmed the core guarantees — drift is applied symmetrically at the mock/wire level and
classified on each arm's own response; the drift middleware is genuinely a no-op when idle (Stage-1 gate
unaffected); the headline wire-coupling signal is correctly measured and captures the SDK package on the
right project. Two fixes were applied in response: **DetectScope** now probes several extended endpoints
(a single broken endpoint can no longer misclassify a 22-op tree as 11-op), and **`price_in_cents` was
removed** from the D1/D2 leak markers (a snake_case field name is a defensible naming choice, and
forbidding it was asymmetric against a wire-style-naming arm). Re-scoring after both fixes reproduced the
scorecard unchanged — neither arm triggered either issue.

One residual limitation is worth stating precisely: **rename-drift detection is conservative.** Because
the oracle uses whole-body value-presence, a renamed field whose value also survives elsewhere (e.g. the
fixture's `previous_state="active"`, or `subtotal==total`) could be scored CORRECT even if the arm
dropped the primary field — but only for an arm that **echoes upstream fields it wasn't asked for**, which
a rich pass-through/SDK-model response is *more* likely to do than a thin hand-mapped one. So this latent
bias runs **toward Arm A (SDK)** — yet Arm B (spec) still won drift resilience. The drift result is
therefore **conservative**: correcting the limitation could only widen Arm B's lead, not reverse it.
(Verified inert here: both arms use thin DTOs, so drops were correctly detected.) Two lower-severity,
**symmetric** notes: `retype` drift cells measure crash-tolerance rather than value fidelity (the value
text is unchanged), and the D4 dependency count is direct+transitive and baseline-dominated (low
discriminating power; the SDK's added surface is a small fraction of the shared eShop closure).

---

## 6. What is deferred (honestly, as Stage 1 deferred its Stage 2)

- **D5 (dev-speed to extend) — EXECUTED (2026-07-14), n = 1 per arm.** One paid extend run each
  (`extend-d5-*-armA` / `-armB` under `benchmark/runs/`); results in §2/§3. Deferred here only is the
  **multi-run D5 campaign** that would put a confidence interval on the ~11% extend-cost gap and test a
  *new-resource-family* extension (not just compose-existing-ops).
- **Full-N statistics.** A pre-registered N≈15–30 fresh-run campaign per arm (per `PROTOCOL.md`) would
  put confidence intervals on every dimension. Infeasible in-session (~40 hrs, hundreds of dollars).
- **Non-Claude judge ensemble + human calibration** for D7.

---

## 7. Reproducibility

- **Method:** `benchmark/docs/QUALITY_PROTOCOL.md` (pre-registered, git `c59da65`).
- **Instruments:** `benchmark/quality/` (D1–D4 + extendcheck), `benchmark/mock/DriftEngine.cs` (drift),
  `benchmark/reference/` (`BREAK=shallowmap` anchor), `benchmark/harness/run-extend.ps1` (D5).
- **Run:** `dotnet run --project benchmark/quality -- --tree <run>/workspace --mode all --out x.json`;
  aggregate across trees for the scorecard.
- **Scored trees:** all 10 under `benchmark/runs/` (8 DONE + 2 excluded stalls).
- **D5 extend runs:** `benchmark/runs/extend-d5-*-armA` / `-armB` (paid; `run-extend.ps1 -Arm A|B
  -SourceRun scope22-armA|B`). Each holds `manifest.json` (tokens/cost/turns), `extendcheck.txt` (gate
  verdict), `prompt.txt`, and the produced `workspace/`.
