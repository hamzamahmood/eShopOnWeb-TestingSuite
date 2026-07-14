# Related Work & Prior Art — API Integration Benchmark

> **Purpose.** A verified survey of published benchmarks and studies adjacent to the *API Integration
> Benchmark* (see `API_INTEGRATION_BENCHMARK.md`), with an explicit contrast of each work's methodology
> against ours, and a synthesis of where the genuine novelty is.
>
> **How this was produced (2026-07-14).** A deep-research pass: fan-out web search across six themes →
> fetch ~24 primary sources → extract ~111 falsifiable claims → 3-vote adversarial verification (a claim
> needs 2/3 refutes to be dropped; 25 load-bearing claims confirmed 3-0, 0 refuted) → synthesis. Sources
> below prefer arXiv abstract pages, ACM/IEEE DOIs, and official leaderboards/repos.
>
> **Verification key.** ✅ = deep-verified in this run (3/3 adversarial votes on the load-bearing claim).
> ◦ = citation found via search index but **not** independently deep-verified here (URL resolves; exact
> claims not triple-checked). **PR** = peer-reviewed · **pre** = preprint.

---

## 1. API-integration *code generation* (the closest analog)

- ✅ **WAPIIBench — "Benchmarking Web API Integration Code Generation"** · Maninger, Chemnitz, Molzam
  Sharifloo, Brugger, Mezini (TU Darmstadt) · **AIware 2025** (pp. 240–248; co-located with ASE 2025,
  Seoul, Nov 19–20 2025) · **PR**.
  → https://arxiv.org/abs/2509.20172 · https://ieeexplore.ieee.org/document/11334492/
  *Measures:* whether an LLM can generate correct API **invocation** code, scored by element-wise
  comparison of the outgoing request (URL / method / headers / query / body) against a ground-truth
  config in a mock.
  *Contrast:* verified quote — *"we evaluate only the correctness of the outgoing request, not how the
  incoming response is handled."* Retries, timeouts, error mapping, idempotency, and drift are all out of
  scope. The nearest neighbor to our work, and it stops exactly where ours begins (whole-integration
  production-readiness + a scored drift dimension).

- ✅ **"Mitigating Errors in LLM-Generated Web API Invocations via Retrieval-Augmented Generation and
  Constrained Decoding"** · same group + Lamba · 2026 · **pre**.
  → https://arxiv.org/abs/2607.05936
  *Measures:* two mitigation techniques evaluated on WAPIIBench's synthetic set + a new GitHub-derived
  real-world dataset (11 APIs).
  *Contrast:* stays scoped to invocation code — confirming this entire line of work never grades a
  produced integration's robustness.

## 2. Agent / LLM API tool-use & function-calling benchmarks

- ✅ **Berkeley Function Calling Leaderboard (BFCL)** · Patil et al. (Gorilla, UC Berkeley) ·
  **ICML 2025 (PMLR v267)** · **PR**. → https://proceedings.mlr.press/v267/patil25a.html
  *Measures:* single / parallel / multi-step function calls via deterministic **AST-based** scoring.
  *Contrast:* grades the *call*, not a running integration's fault-handling or evolution — no
  production-readiness gate, no drift dimension.

- ✅ **τ-bench** · Yao, Shinn, Razavi, Narasimhan (Sierra Research) · 2024 · **pre**.
  → https://arxiv.org/abs/2406.12045
  *Measures:* tool-agent-user dialogues scored by **comparing the final database state to an annotated
  goal state**; introduces **pass^k** for reliability.
  *Contrast:* strongest precedent for our *behavior-not-source* oracle and DONE/ROBUST reliability
  framing — but it evaluates an agent's live task success, not the *quality of a produced integration
  artifact*, and has no drift / maintainability / security axes.

- ✅ **ToolLLM / ToolBench** · Qin et al. · **ICLR 2024** · **PR**. → https://arxiv.org/abs/2307.16789
  *Measures:* LLM tool use across 16,464 real RapidAPI APIs (planning + invocation).
  *Contrast:* invocation / planning correctness, not integration robustness.

- ◦ Same family (invocation correctness, not integration quality): **ComplexFuncBench**
  (https://arxiv.org/abs/2501.10132) · **API-Bank** (EMNLP 2023,
  https://aclanthology.org/2023.emnlp-main.187.pdf) · **API-BLEND** (https://arxiv.org/abs/2402.15491) ·
  **Gorilla APIBench** (https://arxiv.org/abs/2305.15334) · **Live API-Bench**
  (https://arxiv.org/abs/2506.11266).

## 3. Real-world code generation graded by hidden behavioral tests

- ✅ **SWE-bench / SWE-bench Verified** · Jimenez, Yang et al. (Princeton) + OpenAI Preparedness (Verified
  = 500 human-validated, Aug 2024) · **PR / primary**.
  → https://openai.com/index/introducing-swe-bench-verified/ · https://github.com/swe-bench/SWE-bench
  *Measures:* resolve real GitHub issues; success judged by a **hidden test suite not shown to the
  model**.
  *Contrast:* the closest precedent for our **hidden-holdout** discipline — but it's bug-fixing over
  arbitrary repos, not integration production-readiness, and has no drift / maintainability scorecard.

- ✅ **Multi-SWE-bench** · Zan, Huang, Liu et al. · 2025 · **pre**. → https://arxiv.org/abs/2504.02605
  *Measures:* multilingual issue resolution, verified against repository tests.
  *Contrast:* same "patch passes tests" paradigm; no API-integration or drift dimension.

- ◦ Related real-world / repo-level code-gen: **CoderEval** · **ComplexCodeEval**
  (https://arxiv.org/abs/2409.10280).

## 4. Security & quality of generated code (dual-oracle · ISO 25010 · judge validity)

- ✅ **CWEval — "Outcome-driven Evaluation on Functionality and Security of LLM Code Generation"** ·
  Peng et al. · **IEEE / LLM4Code 2025** · **PR**. → https://arxiv.org/abs/2501.08200
  *Measures:* **dual test oracles per task** (one verifying behavior, one verifying the code does *not*
  exhibit a target CWE); 119 tasks / 31 CWEs / 5 languages.
  *Contrast:* the single closest precedent for our *discrimination-validated, two-sided* instrument
  design — but it's isolated security-critical snippets, not a whole integration, with no drift or
  maintainability axis.

- ✅ **SecureAgentBench** · 2025 · **pre**. → https://arxiv.org/abs/2509.22097
  *Measures:* agent-generated code with a dual-oracle design combining functionality testing and a
  security check, under realistic vulnerability scenarios.
  *Contrast:* dual-oracle kinship, but security-only and not integration-scoped.

- ✅ **"Quality Assurance of LLM-generated Code: Addressing Non-Functional Quality Characteristics"** ·
  Sun, Ståhl, Sandahl, Kessler · **Journal of Systems & Software, 2026** · **PR**.
  → https://arxiv.org/abs/2511.10271
  *Measures:* non-functional quality (security, maintainability, performance) of code from three LLMs,
  via literature review + industry workshops + empirical patch analysis.
  *Contrast:* closest to our Part B ISO-25010 intent, but a study of code quality generally — not a
  scored, discrimination-validated benchmark over produced integrations, and no drift dimension.

- ✅ **"Measuring what Matters: Construct Validity in Large Language Model Benchmarks"** · Bean, Kearns,
  Romanou et al. · **NeurIPS 2025 Datasets & Benchmarks Track** · **PR**.
  → https://arxiv.org/abs/2511.04703
  *Measures:* expert review of construct validity across 445 LLM benchmarks; recommendations for better
  benchmark design.
  *Contrast:* not a competitor — the methodological grounding for our *discrimination-validation +
  pre-registration* principles (the "why" behind the gate-on-the-gate). A citation for our rigor.

- ✅ **"Reliability without Validity: A Systematic, Large-Scale Evaluation of LLM-as-a-Judge Models
  Across Agreement, Consistency, and Bias"** · Norman, Rivera, Hughes · **pre**.
  → https://arxiv.org/abs/2606.19544
  *Measures:* 541,000 judgments across 21 LLM judges; chance-corrected metrics (Cohen's κ) show
  exact-match agreement overstates judge quality.
  *Contrast:* direct empirical support for treating our **D7 LLM-judge as the lowest-trust dimension**,
  reported with inter-judge variance.

## 5. Automated REST API testing, fuzzing & robustness

- ✅ **"Open Problems in Fuzzing RESTful APIs: A Comparison of Tools"** · Zhang, Golmohammadi, Belhadi,
  Galeotti, Marculescu, Arcuri · **ACM TOSEM 2023** (preprint arXiv:2205.05325) · **PR**.
  → https://dl.acm.org/doi/10.1145/3597205
  *Measures:* empirical comparison of 7 fuzzers (EvoMaster, RESTler, Schemathesis, RestTestGen, RESTest,
  RestCT, bBOXRT) on the **provider** API.
  *Contrast:* tests whether *the API server* is robust; we test whether *a consumer's integration* is
  robust — the opposite side of the wire.

- ✅ **"Testing RESTful APIs: A Survey"** · Golmohammadi, Zhang, Arcuri · **pre** (extended TOSEM).
  → https://arxiv.org/abs/2212.14604 — landscape reference for the fuzzing / fault-injection methodology
  our Part A borrows.

- ✅ **LlamaRestTest** · 2025 · **pre**. → https://arxiv.org/abs/2501.08598
  *Measures:* LLM-assisted black-box REST **test generation** that grades the API **server under test**.
  *Contrast:* provider-side again; no consumer-integration artifact scored.

- ◦ Related: **APITestGenie** (https://arxiv.org/pdf/2409.03838) · **"Automated Test Generation for REST
  APIs: No Time to Rest Yet"** (ISSTA 2022, https://ar5iv.labs.arxiv.org/html/2204.08348).

## 6. API evolution, schema drift & contract testing (our "crown jewel")

- ✅ **"REST API Testing in DevOps: A Study on an Evolving Healthcare IoT Application"** · 2024 · **pre**.
  → https://arxiv.org/abs/2410.12547
  *Measures:* 5 tools (RESTest, EvoMaster, Schemathesis, RESTler, RestTestGen) for **regression** across
  14 releases of a real evolving API (17 APIs / 120 endpoints).
  *Contrast:* measures the *provider's* stability across versions; our D2 replays the *consumer's
  produced code* against mutated responses and classifies CORRECT / GRACEFUL / BROKEN / SILENT-WRONG.

- ✅ **"Differential Regression Testing for REST APIs"** · **ISSTA 2020** · **PR**.
  → https://dl.acm.org/doi/10.1145/3395363.3397374
  *Measures:* detects breaking changes across API versions (provider-side spec + implementation
  regressions).
  *Contrast:* finds where the provider broke; we score whether the integration *survives* a provider
  change.

- ✅ **oasdiff** · tool / primary. → https://www.oasdiff.com/
  *Measures:* static **spec-to-spec** breaking-change diff (500 change types); **never executes code**.
  *Contrast:* the precise inverse of D2 — it flags that a *schema* changed; we measure whether the
  *running integration* keeps working when it does.

- ✅ **"Microservice API Evolution in Practice"** · 2023 · **pre**. → https://arxiv.org/abs/2311.08175
  *Measures:* qualitative interview study (17 interviews / 11 companies) of real evolution practice.
  *Contrast:* characterizes practice; does not score integrations.

- ✅ **Pactflow — "Schemas are not contracts"** (industry; consumer-driven contract testing / Pact).
  → https://pactflow.io/blog/schemas-are-not-contracts/
  *Measures / argues:* a schema defines one system's shapes; a contract defines how two systems agree to
  communicate — schema conformance alone doesn't establish correct behavior.
  *Contrast:* contract tests confirm an *agreed* request/response shape at build time; they can't (per
  the Pact community itself) prove resilience / fault behavior or grade *unagreed* drift — exactly what
  our fault-injecting mock + drift replay add.

---

## Synthesis — where the genuine novelty is

Across **every** verified work, the field splits into four camps, and **none occupies our quadrant**:

1. **"Can the model call the API?"** — WAPIIBench, BFCL, τ-bench, ToolLLM. Graded at the *single call*,
   stopping at invocation correctness.
2. **"Can the model fix a repo?"** — SWE-bench / Multi-SWE-bench. Hidden-test-verified, but bug-fixing,
   not integration robustness.
3. **"Is the API server robust / did it break?"** — TOSEM fuzzing comparison, differential regression,
   oasdiff, the evolving-IoT study. All **provider-side**.
4. **"Is generated code secure / good?"** — CWEval, SecureAgentBench, the JSS non-functional study.
   Snippet-level, security-first, no drift.

**No prior work grades a whole produced API integration for production-readiness *and* API-drift
resilience as scored quality dimensions.** The two genuinely novel contributions, unmatched in the
verified literature:

- **(a) Drift-resilience as a scored dimension** — response-mutation replay of the *consumer* artifact
  with the CORRECT / GRACEFUL / BROKEN / SILENT-WRONG taxonomy and the Resilience-vs-Safety two-lens
  split. Everything adjacent is provider-side (§5, §6) or static spec-diff (oasdiff).
- **(b) A construction-agnostic whole-integration production-readiness gate** — behavior-not-source,
  property-based, with a DONE / ROBUST hidden holdout.

Strongest *methodological* precedents to cite in our own write-up: **CWEval** (dual-oracle),
**τ-bench** (behavioral end-state oracle + pass^k), **SWE-bench Verified** (hidden holdout), and
**"Measuring what Matters"** (construct validity → discrimination-validation).

---

## Could not verify from a primary source (honesty log)

- The deep-research run's automated synthesis step over-collapsed (it emitted a single final finding
  despite 25 confirmed 3-0 claims); this section was reconstructed from the verified per-source claims
  plus direct fetches. Treat the ◦-marked items in §2 / §3 / §5 — **ComplexFuncBench, API-Bank,
  API-BLEND, Gorilla APIBench, Live API-Bench, CoderEval, ComplexCodeEval, APITestGenie, "No Time to
  Rest Yet"** — as **found-but-not-adversarially-verified** in this run (URLs resolve; exact claims not
  triple-checked).
- **Author lists** for the ISSTA 2020 differential-regression paper and the Microservice-API-Evolution
  study were not individually confirmed (title + DOI / arXiv verified).
- **OpenAPI design-rule static analysis** (arXiv 2511.17836) and **API-first multi-agent** (arXiv
  2510.19274) from the initial sweep did not survive into the verified set — omitted rather than
  asserted.
