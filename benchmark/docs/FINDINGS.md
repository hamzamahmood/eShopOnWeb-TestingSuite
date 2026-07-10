# Findings — SDK-vs-Spec Token Benchmark

> **Status:** Stage-1 complete · 2026-07-10 · **n = 1 per cell (directional, not statistically powered)**
> Companion to the pre-registered `PRODUCTION_READINESS.md`, `TASK_SPEC.md`, `PROTOCOL.md`.

---

## TL;DR

We tested whether an AI coding agent, given APIMatic's **Maxio SDK** (delivered as a Claude plugin),
reaches a *production-ready* integration in **fewer tokens** than the same agent given only the **Maxio
OpenAPI spec** (hand-rolled `HttpClient`). Both arms were forced to clear an identical, external,
deterministic production-readiness gate; the measured quantity is **tokens (cost) to reach DONE + ROBUST**.

**The thesis inverted.** Across six production-verified runs, **hand-rolling from the spec was consistently
cheaper — roughly 1.5–1.9× — at identical quality** (every arm reached DONE + ROBUST). We then tried three
principled ways to tilt the result toward the SDK — a harder/fairer gate, a larger integration, and leaner
SDK delivery (two formats). **All three failed; two made the SDK arm *relatively more* expensive.** The SDK
arm's token cost appears **intrinsic** to consuming the SDK's surface across a long agentic session, not an
artifact of how the SDK is delivered.

**This is n = 1 per cell with real variance** — treat it as a strong directional signal, not proof, and note
the strict scope: *one-shot agent token cost*, on *this API at this size*, with *this agent/model*. It says
nothing about the SDK's value on other axes (correctness under API drift, maintainability, human developer
speed), which this experiment did not measure.

---

## 1. What was measured

- **Both arms build the same thing:** a 11-op (Stage-1 pilot) then 22-op billing integration into
  eShopOnWeb, under pinned `/api/billing` routes (`TASK_SPEC.md`).
- **Same bar:** an external, deterministic gate encodes the production-readiness definition
  (`PRODUCTION_READINESS.md` §6): happy-path correctness with *verified upstream calls* (anti-hardcoding),
  resilience (retry/timeout/transport-fault/bounded-retry/no-duplicate-writes), error mapping + no-leak
  hygiene, and security/config (secret never logged, auth applied, fail-fast config). **DONE** = public
  checks pass; **ROBUST** = a hidden holdout also passes.
- **One variable:** the only difference between arms is the *material* — Arm A gets the `maxio-sdk` plugin
  (skills + the NuGet package); Arm B gets the exact OpenAPI spec APIMatic used to generate that SDK.
  Everything else (task, gate, mock, model `claude-opus-4-8`, effort `high`, isolated workspace) is held
  constant.
- **Cost is the proxy for tokens.** `total_cost_usd` from the headless `claude -p` result; it is dominated
  by **cache-read** input tokens (context re-read across turns), which we report alongside.

Fairness safeguards (see `PROTOCOL.md`): the gate is self-tested **green on a known-good hand-rolled
reference** (so every check is achievable without the SDK) and **reddens on injected defects** (so it
discriminates). The gate asserts *properties/values/ranges*, never one arm's exact output. Infra stalls
(mid-stream API errors) are excluded and re-run per PROTOCOL §7.1 (2 runs excluded here).

---

## 2. All verified runs

| Run | Ops | Gate | Arm | Material | Cost | Turns | Cache-read | Output | DONE | ROBUST |
|---|---|---|---|---|---:|---:|---:|---:|:--:|:--:|
| pilot1 | 11 | v0.3 | B | spec | **$7.77** † | 78 | — † | — † | ✅ | ✅ |
| pilot1 | 11 | v0.3 | A | SDK (full source) | $8.50 | 106 | 8.47M | 76.9k | ✅ | ✅ |
| pilot2 | 11 | v0.4 ‡ | B | spec | **$6.73** | 78 | 6.11M | 66.8k | ✅ | ✅ |
| pilot2 | 11 | v0.4 ‡ | A | SDK (full source) | $13.13 | 123 | 14.46M | 106.5k | ✅ | ✅ |
| scope22 | 22 | v0.3-fam | B | spec | **$8.61** | 55 | 4.20M | 65.4k | ✅ | ✅ |
| scope22 | 22 | v0.3-fam | A | SDK (full source) | $13.18 | 91 | 9.10M | 79.4k | ✅ | ✅ |
| scope22 | 22 | v0.3-fam | A′ | SDK (monolith ref) | $15.94 | 95 | 15.19M | 133.8k | ✅ | ✅ |
| scope22 | 22 | v0.3-fam | A″ | SDK (split per-op ref) | $16.39 | 123 | 19.38M | 129.2k | ✅ | ✅ |

† pilot1 Arm B's token/cost fields were lost to a harness stderr-parse bug (since fixed); $7.77 was recovered
manually from the run log. ‡ v0.4 was a deliberate gate-strengthening intervention, later **reverted** (see
§3, Lever 1). *Excluded (infra stalls, `apiError=true`, re-run per §7.1): pilot2-armA ($4.62, 68t) and
pilot2r-armA ($0.19, 4t).*

**In every head-to-head, the spec arm is cheaper**, by 1.1× (11-op v0.3) up to ~2× (11-op v0.4), settling
at ~1.5× at 22 ops full-source and ~1.9× against the leanest SDK delivery.

---

## 3. Three levers to favor the SDK — three refutations

### Lever 1 — a harder, fairer gate (v0.4) → **backfired; reverted**
We added genuine production checks the SDK handles natively (polymorphic provider-error-shape parsing;
model forward-compatibility). Prediction: the spec arm would have to write more defensive code, widening the
gap in the SDK's favor. **Opposite result:** Arm A rose $8.50 → **$13.13** while Arm B fell $7.77 → **$6.73**
— the SDK arm paid *more* to navigate the SDK's rich typed-error system (`SdkException<TError>`, ~163 error
types, `AnyOf` unions, `RawError`). A fair gate check asserts a property the *agent* must implement; for the
SDK that means more surface navigation, which cost more than the spec arm's few extra parse branches. v0.4
was reverted to the neutral, pre-pilot-locked v0.3 gate (`git 6053d43` + its revert).

### Lever 2 — a larger integration (11 → 22 ops) → **gap widened**
If the SDK's cost were a fixed "learn-the-SDK" tax, spreading it over 2× the operations should shrink the
gap. Instead the gap grew from **+9%** (11 ops, $8.50 vs $7.77) to **+53%** (22 ops, $13.18 vs $8.61). The
**marginal** cost of the 11 added operations tells the story:

| | added 11 ops | per marginal op |
|---|---:|---:|
| spec | **+$0.84** | $0.08 |
| SDK  | **+$4.68** | $0.43 |

Each additional operation cost **~5.6× more** via the SDK. The spec arm reuses one lean HTTP+`JsonDocument`
pattern across all ops for pennies; the SDK arm re-navigates surface (new controllers/models/error types) for
each new resource family.

### Lever 3 — leaner SDK delivery → **did not help (trended worse)**
The full-source delivery has the getting-started skill clone the whole SDK repo (33 `Api/` files + **743**
`Models/` files) and grep it — the dominant cost (cache-read 9.1M). We tried two leaner deliveries, holding
the SDK package + all 7 companion skills constant and changing only the *surface reference*:

- **A′ — one compact `api-reference.md`** (514 KB / ~128k tokens, one grep-able file, no source clone):
  cost **rose** to $15.94, cache-read **15.2M**. The "compact" monolith is still large; the agent re-loaded
  big chunks across 95 turns.
- **A″ — split per-operation** (a ~15k-token `INDEX.md` + 247 tiny ~2KB files, read on demand): cost **rose
  again** to $16.39, cache-read **19.4M**, 123 turns. Rather than narrow lookups, the agent opened **163 of
  247** op files, ran 74 grep-sweeps over the reference dir, and did 24 gate + 12 build cycles. More-granular
  delivery *invited* broader exploration.

Both lean runs verifiably honored the no-clone rule (0 transcript references to the source clone). Making
delivery leaner did not reduce cost; if anything cost/turns/cache-read trended up (the ordering *among* the
three SDK variants is within n=1 variance, so the safe claim is: **leaner delivery did not help; all SDK
variants cluster well above the spec arm**).

---

## 4. Mechanism

The spec arm's cost is low and stable because it works from **one contract document** plus **one reusable
HTTP + `JsonDocument` pattern**, with little build/gate iteration.

The SDK arm's cost is **dominated by cache-read** (context re-read across turns) and **scales with
turns × context**. It is driven by the agent pulling the SDK's *surface* — types, models, `OneOf/AnyOf`
unions, `StringEnum<T>`, the `SdkException<TError>`/`RawError` error system — into context and iterating
(builds, gate runs) to get the C# right. Crucially this was **invariant to delivery format** across three
tries: full source, compact monolith, and split per-op all landed $13–16. That points to the cost being
**intrinsic to consuming the SDK surface with an agent**, not a packaging problem a better doc fixes.

---

## 5. Quality was at parity

The spec arm's cheapness is **not** bought with lower quality. **All eight verified arm-runs reached DONE +
ROBUST** — every arm passed the same public checks *and* the hidden holdout, including resilience (retry,
timeout, bounded retries, no-duplicate writes), error-mapping hygiene (no leaked internals), and security
(secret never logged, auth applied, fail-fast config). Both approaches produced genuinely production-ready
integrations; they differ only in the tokens spent getting there.

---

## 6. Caveats & threats to validity

- **n = 1 per cell, real variance.** Arm A (full-source) alone ranged $8.50–$13.18 across runs. Treat
  *direction* as the signal, not the exact magnitudes; the ordering among the three SDK delivery variants is
  within noise.
- **Scope of the claim.** This measures **one-shot agent token cost** to first production-ready, on **this
  API (Maxio) at this size (11–22 ops)**, with **one agent/model** (`claude-opus-4-8`, effort high). It is
  **not** a verdict on SDKs generally, on APIMatic, or on the SDK's value on other axes.
- **Naming contamination (both arms).** The provider is named concretely ("Maxio"), so the agent may draw on
  latent Maxio/Chargify training knowledge. This applies to both arms (not a directional bias) but means the
  spec-only number is a lower bound relative to a truly-unknown API.
- **Single model, single agent harness.** A different model or a coding harness with an LSP/type-server
  (letting the SDK arm discover signatures without reading source) could change the SDK arm's economics.
- **Mock fidelity.** The upstream is a spec-faithful mock, not the live Maxio API; some real-world SDK value
  (handling quirks the spec omits) is out of scope by construction.

---

## 7. What this does *not* settle (open questions)

- **The SDK's real value axes are untested here:** correctness under API drift, long-term maintainability,
  human developer speed/ergonomics, and safety of the generated code. A fair case *for* the SDK would measure
  those, not one-shot agent tokens.
- **No statistical power.** Stage 2 (N ≈ 30 interleaved trials with confidence intervals, per `PROTOCOL.md`)
  was not run; the ~1.5× gap has no CI yet.
- **A genuinely minimal, task-scoped reference** (only the ~22 needed operations, curated) was not tried —
  though A″ suggests the agent explores broadly regardless of granularity.
- **Different / larger APIs** and **harness with type-server assistance** may shift the result.

---

## 8. Reproducibility

- **Pre-registration:** `PRODUCTION_READINESS.md` (v0.3), `TASK_SPEC.md` (v0.4, 22-op), `PROTOCOL.md`.
- **Harness:** `benchmark/mock` (spec-faithful fault-injecting mock), `benchmark/gate` (deterministic
  public+holdout gate; self-tests green on `benchmark/reference`), `benchmark/harness/run-arm.ps1`
  (`-Arm A|B [-Lean|-Split]`).
- **Arm-A delivery variants:** full source = `maxio-sdk` plugin; `-Lean` = `maxio-sdk-lean` +
  `harness/lean/maxio-api-reference.md`; `-Split` = `maxio-sdk-split` + `harness/lean/ref-split/`.
- **Run artifacts** (git-ignored): `benchmark/runs/<runId>-arm<A|B>/manifest.json` + `claude-result.json`
  + gate logs + the produced `workspace/`.
- **Key commits:** 22-op scale-up (`9da700d`), v0.4 gate + revert (`6053d43` + revert `13c6070`), lean A′
  (`ccdacbe`), split A″ (`b89344e`).
