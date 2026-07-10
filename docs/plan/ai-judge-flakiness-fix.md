# Plan — Make the AI payload judge reliable (keep AI, stop the flip-flops)

## Context

The black-box suite (`MaxioApiTests`) verifies response **bodies** with an LLM
judge (`Ai/OpenAIApiService.VerifyAsync` → `Expect.AiPassed`) so it can match on *meaning*
across the two integrations' differing shapes (camelCase vs snake_case, nesting, cents vs
dollars, id string vs number). We want to **keep** that AI judge — it is the right tool for
comparing responses that differ only in case/shape.

The problem is flakiness: during a Plugin run the `RecordUsage` success test flipped from
pass to fail **with no code change**. Root cause was verified live (mock + Plugin, real AI
key):

- The response body is **byte-identical** across calls (sha256 identical ×5):
  `{"id":138522957,"quantity":42,"memo":"black-box test run","periodToDateTotal":42}`.
- 45 consecutive real-AI runs all passed → the response/mock/controller are **not** the
  variable; the only run-to-run variable is the AI judge.
- Inducing an AI-endpoint blip (same code, same body) flipped the identical test to **FAIL**
  with `Retry failed after 4 tries … connection refused`.

So there are **two independent flip causes**, both intrinsic to the judge:

1. **Sampling variance** — GPT-5 runs at temperature 1 (it rejects temperature 0 → HTTP 400,
   see the comment in `VerifyAsync`) and there is no seed, so an identical `(payload, rules)`
   can be judged pass on one run and fail on another. Rare for a clean payload, higher for
   messier real-run shapes and the two-`42` ambiguity.
2. **Transient API failure** — `VerifyAsync` makes a live network call with **no app-level
   error handling**; a 429 / timeout / 5xx that outlives the SDK's own retries throws and the
   test hard-fails. Far more likely under a full-suite run (20+ back-to-back AI calls =
   rate-limit territory) than in a spaced single-test loop.

**Goal:** keep the AI judge for every content check (including case/shape matching), but make
its verdict stable and its transient failures non-fatal — so a byte-identical body can never
produce a false regression. Do **not** move any assertion to a mechanical check; do **not**
weaken the "a content assertion that genuinely cannot run is a hard failure, not a silent
pass" rule.

## Cause → fix mapping

| Flip cause (verified) | Fix (keeps AI) |
|---|---|
| Sampling variance (temp 1, no seed) → identical body judged pass then fail | Re-verify on a FAIL verdict, decide by **majority (best-of-3)** |
| Transient API blip (429 / timeout / 5xx) throws → hard fail | **App-level retry with exponential backoff** around the call |
| Residual per-call variance | Fixed **`Seed`** (best-effort) + keep the hard-fail-if-truly-unavailable rule |

Key properties that make this safe and cheap:

- A **genuine** mismatch fails *every* attempt, so re-verifying only on failure cannot hide a
  real bug — it only cancels the *rare lone* wrong verdict.
- A green run stays **one** AI call per test (the first PASS short-circuits); extra calls fire
  only when a failure appears.
- Only a **sustained** outage still fails (a momentary blip is absorbed), preserving the
  existing no-silent-pass contract. The no-key / `AI_COMPARISON_ENABLED=false` path in
  `CreateOrNull` / `Require` is left untouched — that is a config error and must stay a hard
  fail.

## Changes — all inside `MaxioApiTests/Ai/OpenAIApiService.cs`

No call sites change (`Expect.AiPassed`, `Require`, the tests, `VerificationReport` all stay
as-is). Replace the single `VerifyAsync` with a consensus wrapper over a resilient single call.

### 1. Consensus wrapper (best-of-3, re-verify only on failure)

```csharp
public async Task<VerificationReport> VerifyAsync(
    string payloadJson, IReadOnlyList<string> rules, CancellationToken ct = default)
{
    // Re-verify only when a verdict comes back FAILED, then decide by majority (best-of-3). The judge is
    // non-deterministic (GPT-5 runs at temperature 1), so a lone flaky "fail" on a byte-identical body must
    // not become a false regression — a genuine mismatch fails every attempt and still loses the majority.
    // A PASS on the first attempt short-circuits, so green runs stay at one AI call per test.
    var maxAttempts = TestSettings.AiVerifyAttempts;   // default 3 (see knob below)
    int passes = 0, fails = 0;
    VerificationReport? passReport = null, failReport = null;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var report = await VerifyOnceAsync(payloadJson, rules, ct);
        if (report.Passed) { passes++; passReport = report; } else { fails++; failReport = report; }

        if (attempt == 1 && report.Passed) return report;   // happy path: 1 call
        if (passes > maxAttempts / 2) return passReport!;    // majority pass
        if (fails  > maxAttempts / 2) return failReport!;    // majority fail → a real mismatch
    }
    return failReport ?? passReport!;
}
```

### 2. Resilient single call (retry transient faults, fixed seed)

```csharp
// One verification with transient-fault resilience: retries 429/5xx/timeout with exponential backoff so a
// momentary OpenAI blip can't flip a test. Only a sustained outage surfaces — still a hard fail, by design.
private async Task<VerificationReport> VerifyOnceAsync(
    string payloadJson, IReadOnlyList<string> rules, CancellationToken ct)
{
    const int maxTransientRetries = 3;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            var response = await _chat.GetResponseAsync<VerificationReport>(
                BuildPrompt(payloadJson, rules),
                new ChatOptions { Seed = 1 },   // best-effort determinism; temperature stays default (GPT-5 requires 1)
                useJsonSchemaResponseFormat: TestSettings.AiUseJsonSchema,
                cancellationToken: ct);
            return response.Result;
        }
        catch (Exception ex) when (attempt <= maxTransientRetries && IsTransient(ex))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)), ct);
        }
    }
}

private static bool IsTransient(Exception ex) => ex switch
{
    System.ClientModel.ClientResultException e => e.Status == 429 || e.Status >= 500,
    HttpRequestException => true,
    TaskCanceledException => true,   // timeout
    _ => false,
};
```

Notes:
- `ChatOptions` is already available via `using Microsoft.Extensions.AI;`. `Seed` is a
  best-effort lever — reasoning models may ignore it — so the real reliability comes from
  consensus + retry, not the seed.
- Confirm the `GetResponseAsync<T>` overload order at implementation time (options is the
  positional arg before `useJsonSchemaResponseFormat`; the current code passes it by named
  args). Add `using System.Net.Http;` if `HttpRequestException` isn't already in scope.

### 3. Optional knob — `MaxioApiTests/TestSettings.cs`

Add an env-overridable attempt count alongside the existing AI settings, defaulting to 3:

```csharp
/// <summary>How many times the AI judge may re-verify a FAILED verdict before deciding by majority
/// (AI_VERIFY_ATTEMPTS). Default 3. A first-attempt PASS short-circuits, so green runs stay at 1 call.</summary>
public static int AiVerifyAttempts =>
    int.TryParse(Get("AI_VERIFY_ATTEMPTS", "3"), out var n) && n >= 1 ? n : 3;
```

(If you'd rather not add a setting, hard-code `const int maxAttempts = 3;` in `VerifyAsync`.)

## What deliberately does NOT change
- The AI judge remains the verifier for **every** content assertion, including case/shape
  comparison — no assertion is converted to a mechanical check.
- `BuildPrompt` stays lenient (that leniency is correct — leave it).
- `Require` / `CreateOrNull` keep hard-failing when no key is resolved or AI is disabled — a
  missing verifier is a config error, not a transient blip.
- `Expect.AiPassed`, `VerificationReport`, and all `Tests/*.cs` are untouched.

## Verification (per CLAUDE.md — confirm live, not compile-only)
1. Build `MaxioApiTests`.
2. Boot the mock (`:8080`) + one integration (Plugin `:5199`) routed at the mock.
3. Confirm the `RecordUsage` response body is still byte-stable (curl ×5, identical sha256).
4. Loop `RecordUsageTests.Known_subscription_records_usage_...` ~30–45× with the real AI key
   → expect **all pass** (no flip), same as the baseline, confirming no regression.
5. Transient-fault path: run once with `AI_ENDPOINT` pointed at an unreachable host and
   confirm the app-level retries fire (backoff visible in timing) before it ultimately fails —
   i.e. a *momentary* blip would be absorbed, only a *sustained* outage fails.
6. Cost sanity: a fully-green run makes exactly one AI call per content test (no extra calls
   unless a verdict fails).

## Files to modify
- `MaxioApiTests/Ai/OpenAIApiService.cs` — replace `VerifyAsync`; add
  `VerifyOnceAsync` + `IsTransient`.
- `MaxioApiTests/TestSettings.cs` — optional `AiVerifyAttempts` knob.
