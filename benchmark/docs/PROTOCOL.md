# Experimental Protocol — SDK-vs-Spec Token Benchmark

> **Status:** PRE-REGISTRATION DRAFT · v0.2 · 2026-07-09
> **Companion to** `PRODUCTION_READINESS.md` (LOCKED v0.2) and `TASK_SPEC.md` (LOCKED v0.3). This
> document is the run mechanics + measurement + statistics + credibility plan. The three docs lock
> together (git-committed and hashed) **before any benchmark run**. Changes after seeing benchmark
> results are disallowed (§12). The Stage-1 pilot may only *calibrate* the pre-specified knobs in §7
> (N, budget cap) by the rule stated there.

---

## 1. Hypothesis (operational)

> Building the `TASK_SPEC.md` integration to the `PRODUCTION_READINESS.md` bar, **Arm A (SDK via the
> `maxio-sdk` plugin) costs fewer tokens to reach DONE than Arm B (OpenAPI spec only).**

Null: no difference in the tokens-to-DONE distributions. We report the result whichever way it
lands, including "no significant difference" and "Arm A also pays for property X."

## 2. Metrics

| | Metric | Definition |
|---|---|---|
| **Primary** | **Cost-to-DONE** | Total billable cost (USD, fixed price table §5) of the **whole headless session**; DONE = the produced tree passes the public gate, **verified by the experimenter after the session ends** (not the agent's self-claim) |
| **Primary (cache-independent cross-check)** | **Output-tokens-to-DONE** | Output tokens only — immune to cache-hit luck, the least gameable "work done" proxy |
| Secondary | Cost/Output-tokens **to-ROBUST** | The cost-to-DONE of the **subset of runs that also pass the holdout** — no extra "reaching" phase (the agent never sees the holdout); it filters the same session cost (§ `PRODUCTION_READINESS.md` 7/9) |
| Secondary | **Success rate** | Fraction of runs reaching DONE within the budget cap |
| Secondary | Turns, wall-clock, tool-calls | From the `-p` JSON result |
| Diagnostic | **Token split** | The four classes (input / output / cacheRead / cacheCreation) **reported separately**, and by `query_source` (main / subagent / plugin) |

**Never** collapse the four token classes into one number (cacheRead bills ~0.1×, cacheCreation
~1.25× — collapsing distorts cost ~10×). The material-ingestion cost counts and is *part of the
signal*: Arm A's on-demand plugin-skill tokens vs. Arm B's cost of ingesting the (large) OpenAPI spec.

## 3. Design: one variable, everything else held constant

**Independent variable (the only intended difference):** the per-arm material and its placement
(`TASK_SPEC.md` §2/§4).

**Held constant (byte-identical or pinned across both arms):**

| Held constant | How |
|---|---|
| Model | one **pinned dated snapshot** via `--model` — a **single frontier model** (Claude Code default Opus) for pilot + benchmark; log the model/fingerprint every run. A multi-model sweep is a post-pilot extension, not part of this pre-registration |
| Reasoning effort / thinking | pinned via `--effort`; identical both arms |
| Prompt scaffold | `TASK_SPEC.md` §5 verbatim; only the `{{ARM_MATERIAL}}` block differs |
| Task spec / routes / request contracts | `TASK_SPEC.md` §3, LOCKED |
| Definition of done / gate / holdout | `PRODUCTION_READINESS.md`, LOCKED; same gate binary both arms |
| Mock (faults + recording) + canned data + fault config | identical |
| Starting repo | same pristine eShopOnWeb clone at a **pinned commit**, zero billing code (`TASK_SPEC.md` §1.1) |
| Isolation | `claude --bare`; no CLAUDE.md / benchmark docs / prior integration in context |
| Budget cap, tooling, permission mode, env, concurrency | identical |

Confounds neutralized: **model drift** (pin dated snapshot); **prompt caching** (interleave A/B/A/B,
report cacheRead separately §6); **prompt wording** (shared scaffold identical); **scaffold** (same
headless harness, tool set, max-turns).

## 4. Run procedure (per trial)

1. **Fresh isolated workspace:** clone the pinned eShopOnWeb baseline into a clean directory (or a
   fresh `git worktree`). Place the per-arm material (Arm A: enable `maxio-sdk` plugin via
   `--plugin-dir`, no spec file; Arm B: drop the spec in the tree, plugin disabled).
2. **Launch headless**, one process per trial (no `--continue`/`--resume`):
   ```
   CLAUDE_CODE_ENABLE_TELEMETRY=1 OTEL_METRICS_EXPORTER=otlp \
   OTEL_EXPORTER_OTLP_PROTOCOL=grpc OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
   OTEL_METRIC_EXPORT_INTERVAL=5000 \
   claude -p "$(compose_prompt <arm>)" \
     --output-format json --model <pinned-snapshot> --effort <pinned> \
     --bare [--plugin-dir C:\repos\v4-plugins\plugins\maxio-sdk for Arm A] \
     --dangerously-skip-permissions
   ```
3. **Agent stops** when it believes the task is done (or the cap is hit). The **experimenter then
   runs the public gate on the produced tree**: green = **DONE**. **Cost-to-DONE = the whole
   session's cost**, regardless of when it first went green. Cap hit without green → **failure**;
   agent stopped early but tree not green → **failure**.
4. **Capture tokens** three ways (§5) and reconcile.
5. **Experimenter runs the holdout** + upstream-call/observational integrity checks → **ROBUST?**
6. **Snapshot** the produced integration tree + the full transcript for the artifact release.
7. **Tear down** the workspace.

## 5. Token capture rig

- **Primary:** OpenTelemetry metric `claude_code.token.usage`, grouped by `session.id`, split by
  `type` (input/output/cacheRead/cacheCreation) and `query_source` (main/subagent).
- **Cross-check A:** the `-p --output-format json` result (`usage` + `total_cost_usd`).
- **Cross-check B:** `ccusage session --json` (dedups the streaming JSONL correctly; a naive
  transcript sum over-counts output tokens).
- **Reconciliation:** OTel is **authoritative**; the two cross-checks flag discrepancies (>2% on any
  token class → investigate that run before including it).
- **Cost figure:** apply a **single fixed price table** to the four classes for *both* arms. Pin the
  price table in the run manifest.

## 6. Ordering & isolation

- **Interleave A/B/A/B…**, not all-A-then-all-B.
- One OS process per trial; reset the working tree between trials.
- Treat **cacheRead as the noisiest class**; output-tokens (§2) is the cache-independent arbiter.

## 7. Sample size & budget cap (calibrated at the pilot, by a pre-specified rule)

- **Stage 1 — pilot:** N=1 per arm end-to-end to validate the harness/prompt/gate (case study). Then
  **5 per arm** to estimate the tokens-to-DONE variance.
- **Budget-cap rule (pre-specified):** cap = **3× the max cost-to-DONE over all pilot runs that
  reached DONE** (rounded up). **Enforced live** via `--max-turns` + a wall-clock timeout; a run whose
  captured tokens exceed the cap's token-equivalent is classified a **failure** post-hoc. **Fallback**
  (if an arm rarely/never reaches DONE in the pilot, giving no basis for the max): a fixed high token
  ceiling, with that arm's high failure rate reported as a headline result. Fixed *before* the
  benchmark, untouched afterward.
- **N rule (pre-specified):** from the pilot variance, a power analysis for the target detectable
  effect sets N; **default N = 30 per arm** if the calculation is inconclusive. (Literature: ~30 reps
  for stable CIs; SWE-bench averages ~5/instance, METR 6–8 — 30 is conservative for a token-cost gap.)

### 7.1 Exclusion criteria (pre-registered)

A run is an **infrastructure failure** — excluded and **re-run** (≤3 re-runs; every exclusion logged
with cause) — iff it fails for a cause outside the agent's work: NuGet/registry unreachable or a
restore error, mock/Toxiproxy/OTel-collector crash, Anthropic API 429/529/5xx overload, network loss,
or a gate check that is **flaky** (fails then passes on identical produced bytes). Everything else is
a **task failure** (counted): the agent produced code that doesn't reach DONE within the budget,
doesn't compile, or the harness ran cleanly and the tree simply isn't green. **Ambiguous cases
default to counted** — never silently drop a genuine failure. Exclusion rules are symmetric across
arms and every exclusion is reported. (This matters because Arm A depends on NuGet/GitHub and Arm B
barely does; without this rule, infra noise would bias the comparison and post-hoc classification
would be a p-hacking vector.)

## 8. Statistics & reporting

- **Headline per arm:** **median + IQR** of cost-to-DONE, with a **bootstrap BCa 95% CI on the
  median**. Mean ± SD only as a secondary line. Same for output-tokens-to-DONE.
- **Arm comparison:** **Mann–Whitney U** (primary); **Cliff's delta** as effect size (|δ|: <0.147
  negligible / <0.33 small / <0.474 medium / ≥0.474 large). Welch's t only if a mean claim is made.
- **Reliability:** **success rate** with CI; **McNemar** on pass/fail rates; **pass@k and pass^k**.
- **Honest cost including failures:** **effectiveness-aware cost-per-success = total tokens across ALL
  runs ÷ number of successful runs.** Report cost and success **jointly** (Pareto); never average
  only the runs that reached green.
- **DONE vs ROBUST:** report both distributions; if an arm's DONE≫ROBUST it overfit the visible gate.

## 9. Analysis blinding

- Body assertions that need the LLM-judge fallback (`PRODUCTION_READINESS.md` §5) use a judge from a
  **different model family than the arm under test** (both arms built by Claude → judge is
  non-Claude), **blind to arm identity**, order randomized.
- Where feasible the analyst computing the statistics is blind to which arm is which.

## 10. Data recorded per run (manifest schema)

`run_id · arm · timestamp · model_id + fingerprint · effort · price_table_id · tokens{input,output,
cacheRead,cacheCreation} × query_source · total_cost_usd · num_turns · wall_clock_ms · tool_calls ·
DONE(bool) · cost_to_DONE · output_tokens_to_DONE · budget_cap_hit(bool) · ROBUST(bool) ·
public_check_results · holdout_check_results · transcript_path · produced_tree_path`

## 11. Credibility safeguards

1. **Pre-registration:** the three docs are git-committed and content-hashed **before** the benchmark;
   the hash is published. Blocks metric-shopping / HARKing.
2. **Implementation-agnostic gate** (`PRODUCTION_READINESS.md` §5 fairness principle).
3. **Public + hidden holdout** — detects gaming; both distributions reported.
4. **Full artifact release:** prompts, per-arm inputs, seeds/config, per-run transcripts, per-run token
   manifests, the gate + mock + Toxiproxy config, and the produced integration trees.
5. **COI disclosed:** APIMatic authors the `maxio-sdk` plugin **and** runs this experiment. Stated
   plainly; mitigated by 1–4 and by inviting **independent replication**.

## 12. Threats to validity (disclosed)

| Threat | Direction | Mitigation |
|---|---|---|
| **"Maxio" named concretely** — Arm B can draw on latent Maxio/Chargify training knowledge | lowers Arm B's apparent cost → spec-only cost is a **lower bound** | disclosed; non-directional; optional sensitivity re-run with a neutralized name |
| **APIMatic-authored plugin may be unusually well-tuned** | could favor Arm A | it *is* the product under test; disclosed; replication on other SDKs generalizes |
| **Both arms built by Claude** (self-consistency) | — | judge is non-Claude, blind (§9) |
| **Prompt-wording sensitivity** | either | shared scaffold identical; optional paraphrase-robustness check |
| **Model drift / caching** | noise | pinned snapshot + fingerprint; interleave; cacheRead separate; output-tokens arbiter |
| **Gate flakiness** | noise charged to an arm | deterministic-first gate; re-run flaky checks; log flakiness |
| **Single API / single app** | limits external validity | explicitly scoped; future replication on other APIs/apps |
| **Gate strengthened after the pilot** (v0.4) | could look like post-hoc tuning toward the SDK | pilot-driven revision (Stage-1 → pre-Stage-2), documented in `PRODUCTION_READINESS` v0.4. Additions assert genuine production properties (provider error-shape variety + model forward-compat); the competent hand-rolled reference still passes **all** of them (public 31/31, holdout 7/7), and a naive parser fails them; made and locked **before any Stage-2 run**, not after seeing Stage-2 results |

## 13. Lock status & companions

- `PRODUCTION_READINESS.md` — LOCKED v0.2.
- `TASK_SPEC.md` — LOCKED v0.3.
- `PROTOCOL.md` — this doc, DRAFT v0.2 → to lock with the other two before any benchmark run.
- **Build queue (post-lock):** the spec-faithful fault-injecting request-recording mock; Toxiproxy
  sidecar; `gate/` (public + holdout); the headless run harness + OTel collector + manifest writer;
  the pinned eShopOnWeb baseline; the `openAPI/` spec (present).
