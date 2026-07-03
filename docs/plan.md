# Plan: New test cases showcasing the SDK (Plugin) integration's production-readiness

This plan adds test cases to `MaxioPassthroughApiTests` that demonstrate where the SDK-based
integration (`eShopOnWeb-Plugin`) is closer to production-ready than the hand-rolled HTTP
integration (`eShopOnWeb-Direct`). Two kinds of tests:

- **Advantage tests** — pass on Plugin, fail on Direct. Each failure marks something the SDK
  version does better.
- **Safety-net tests** — pass on both. The value is the story: Direct passes only because of
  custom hand-written code; Plugin passes out of the box via SDK defaults.

Everything stays black-box: tests only talk to `/api/maxio` over HTTP, so the comparison stays fair.

## Summary

| # | Test case | Kind | Mock changes | Test-suite changes | Plugin app changes | Direct result |
|---|---|---|---|---|---|---|
| 1 | Richer payment-failure messages | Advantage | Small | Small | None | Fails (generic msg) |
| 2 | Customer-lookup endpoint | Advantage | None | Small | None | Fails (no endpoint) |
| 3 | No internals in error bodies | Safety-net | None | New file | None | Passes |
| 4 | Create never silently retried | Safety-net | Small | Small | None | Passes (502) |
| 5 | Recovers from 429 / transient 503 | Safety-net | **None** | Small | None (optional log) | Passes |
| 6 | Frozen provider → clean timeout | Safety-net | Small | Small | Depends on #7 | Passes (slower) |
| 7 | Fix: provider-down leaks raw 500 | Bug fix + pin | None | (covered by #6) | **Small fix** | Passes already |
| 8 | Wrong-state transition → 409 + reason | Advantage | None | Update 4 facts | Medium | Fails (422) |
| 9 | Double-submit usage recorded once | Advantage | None | New file | Small | Fails (double-charged) |
| 10 | Unknown provider status → safe default | Advantage | Small | Small | **None** | Fails (raw string) |

Recommended order: 1, 2, 3, 5 (no app changes) → 7, 4, 6 (mock + one Plugin fix) → 10, 9, 8
(advantage showcases; 8 has a decision point).

## Why no changes to eShopOnWeb-Direct (deliberate)

Direct is the **control group** — no item in this plan modifies it, by design:

- **Advantage tests are supposed to fail on Direct.** Each failure pins something the SDK route
  provides that the hand-rolled route doesn't. Patching Direct to pass (hand-writing a payment
  classifier, a lookup endpoint, a state machine, an idempotency cache) would erase the signal the
  suite exists to measure. Per this workspace's ground rules, Direct-vs-Plugin differences are
  intentional signal, not drift.
- **Safety-net tests already pass on Direct unchanged** — because its hand-written resilience code
  (`MaxioDependencies.cs`: Polly retries, `DisableForUnsafeHttpMethods`, 10s per-attempt timeout;
  `MaxioErrorReader.cs`: error sanitizing) covers them. That asymmetry (bespoke code vs SDK
  defaults) is the finding, not a gap to close.
- **The Plugin changes don't tilt the scales.** Item 7 fixes a real Plugin bug (the one place
  Direct is currently better — required for an honest comparison). Items 8/9 only expose
  capabilities the Plugin codebase already contains but the controller bypasses. Nothing is built
  for Plugin that Direct couldn't also hand-roll — the case study's argument is exactly that on
  Direct each of those is a from-scratch effort.

What running against Direct *does* involve: the same env knobs as today (notably
`RECORD_USAGE_PATH_TEMPLATE` for its usage route), and an updated expected-failure list — see the
verification recipe.

---

## Item 1 — Richer payment-failure messages

**What it shows.** The Plugin turns provider validation messages that mention payment problems into
a typed `PaymentVerificationRequiredException` with an actionable message
("Additional payment information is required…"). Direct surfaces a generic provider error. Today
only one flavor (`card-required`) is tested; production sees several.

**How it works today.** Plugin keyword classifier: `ContainsPaymentKeyword` in
`eShopOnWeb-Plugin/src/Infrastructure/Services/MaxioBillingClient.cs:407-412` matches
`card`, `payment`, `3-d secure`, `3d secure`, `3ds`. The middleware maps the typed exception to
422 with the message (`ExceptionMiddleware.cs:67-75`). The mock's `card-required` branch is in
`MaxioMockServer/Program.cs` route 6 (`POST /subscriptions.json`, currently lines 177-182).

**Changes required.**

1. `MaxioMockServer/Program.cs`, route 6: replace the single `card-required` equality check with a
   lookup over a small map of payment-failure handles → messages, **kept before the
   known-product-handle check** (same position as today):
   - `card-required` → existing two messages (unchanged).
   - `threeds-required` → `"3-D Secure authentication is required to complete this payment."`
   - `card-declined` → `"The credit card was declined. Payment could not be collected."`
   Each returns the standard `Errors(422, …)` shape. (Each message contains at least one Plugin
   keyword: `3-d secure`/`payment`, `card`/`payment`.)
2. `MaxioPassthroughApiTests/TestSettings.cs`: add
   `PaymentRequiredProductHandles` — env override `PAYMENT_REQUIRED_PRODUCT_HANDLES`, default
   `"card-required,threeds-required,card-declined"`, split on comma. Keep the existing
   `PaymentRequiredProductHandle` for back-compat.
3. `MaxioPassthroughApiTests/Tests/PluginAdvantageTests.cs`: convert the payment fact to a
   `[Theory]` (`MemberData` over the handles). Each case: `POST /api/maxio/subscriptions` with the
   known customer id (98765) and the handle; assert status 422 **and** body contains
   `"Additional payment information is required"`.

**Expected.** Plugin: all cases pass. Direct: all fail (generic "billing provider rejected"
message) — by design.

---

## Item 2 — Customer-lookup endpoint (Plugin-only capability)

**What it shows.** The Plugin exposes a read-only customer lookup
(`GET /api/maxio/customers/lookup?reference=…`, `MaxioBillingController.cs:88-100`) because with
an SDK it costs a few lines. Direct never built it. The mock route
(`GET /customers/lookup.json`) already exists.

**Changes required.**

1. `TestSettings.cs`: add `CustomerLookupPath(string reference)` →
   `/api/maxio/customers/lookup?reference={reference}` (URL-encode the reference).
2. New file `Tests/CustomerLookupTests.cs`, two facts:
   - Known reference (`TestSettings.KnownCustomerReference`, `cust_12345`) → 200, and
     `TestJson.GetCustomerId` equals `TestSettings.KnownCustomerId` (98765).
   - Unknown reference (`TestSettings.UnknownCustomerReference`) → 404.
3. File-level doc comment stating this endpoint exists only on Plugin: on Direct the known-reference
   fact fails with 404 (route missing) and the unknown-reference fact passes only coincidentally.

**Expected.** Plugin: both pass. Direct: known-reference fact fails — by design.

---

## Item 3 — No internal details in error bodies

**What it shows.** Every failure response is a clean `{statusCode, message}` JSON body — no stack
traces, exception type names, or library internals. Encodes the middleware's "never leak raw
exception text" contract as an executable check.

**Changes required.**

1. New file `Tests/ErrorHygieneTests.cs`. One `[Theory]` sweeping the canned failure cases
   (all already supported by the mock — no mock changes):
   - `GET  /api/maxio/subscriptions/{UnknownSubscriptionId}`
   - `POST /api/maxio/subscriptions/{KnownOnHoldSubscriptionId}/hold`
   - `POST /api/maxio/subscriptions` with `UnknownProductHandle`
   - `DELETE /api/maxio/subscriptions/{KnownCanceledSubscriptionId}` (timing `Immediate`)
   - `POST /api/maxio/subscriptions/{KnownActiveSubscriptionId}/migrations` with
     `UnknownProductHandle` (timing `Immediate`)
   Assertions per case: status is 4xx; content type is JSON; body contains **none** of:
   `"System."`, `"   at "`, `"Exception"`, `"MaxioAdvancedBilling"`, `"HttpRequestException"`,
   `"Polly"`, `"StackTrace"`.
2. No mock or app changes.

**Expected.** Passes on both today (provider-error paths only). The transport-failure path joins
this contract after item 7.

---

## Item 4 — A transient failure while creating a subscription is never silently retried

**What it shows.** A create (POST) that fails with an error response must NOT be automatically
retried — a retry could charge the customer twice. Both integrations honor this: Direct via its
hand-written Polly rule (`DisableForUnsafeHttpMethods`,
`eShopOnWeb-Direct/src/Infrastructure/MaxioDependencies.cs:64`), Plugin via the SDK's default
retry method list (status-triggered retries apply only to GET/HEAD/PUT/OPTIONS —
`RetryOptions.Default()`). Direct wrote the guarantee; Plugin got it as a default.

**Honest caveat (verified in the SDK source).** The SDK's method allowlist governs
*status-code-triggered* retries only. Its retry predicate also handles `HttpRequestException`
(a transport-level drop) with **no method filter**, and JSON-bodied POSTs are replayable
(`JsonRequest.CanRetry => true`) — so a connection that dies mid-POST *could* be retried by the
SDK. This test's scenario (a 503 **response**) is method-filtered and safe on both integrations;
the transport-drop-on-POST case is a residual duplicate-risk on Plugin worth noting in the case
study (Direct's `DisableForUnsafeHttpMethods` covers both flavors). Not testable with this mock
(it can't drop connections mid-request); document rather than test.

**How the test proves it black-box:** the mock fails the FIRST create attempt with 503 and would
succeed on a second attempt. If an integration silently retried the POST, the caller would see a
successful 2xx. Seeing an error therefore proves no hidden retry happened.

**Changes required.**

1. `MaxioMockServer/Program.cs`, route 6 (`POST /subscriptions.json`): before the
   known-product-handle check, add: if `productHandle` starts with `"transient_"` →
   `mocks.NextAttempt(productHandle)`; attempt 1 → `Errors(503, "The billing service is temporarily
   unavailable. Please retry.")`; attempt ≥ 2 → return `mocks.NewSubscriptionJson(15100300,
   customerId.Value, "gold")` with 201 (treat the handle as an alias of `gold`).
   `MockStore.NextAttempt` already exists (`MockStore.cs:75`); keying on the full nonce-bearing
   handle keeps runs order-independent, same as the `race_`/`retry_` prefixes.
2. `TestSettings.cs`: add `TransientCreateProductHandlePrefix` (env
   `TRANSIENT_CREATE_PRODUCT_HANDLE_PREFIX`, default `"transient_"`) and
   `NewTransientCreateProductHandle()` returning prefix + GUID nonce.
3. New file `Tests/RetrySafetyTests.cs`, fact `Transient_create_failure_is_surfaced_not_retried`:
   `POST /api/maxio/subscriptions` (known customer 98765, fresh transient handle) → assert the
   status is **not** 2xx and is 422 or 502 (Plugin maps `BillingProviderException` → 422; Direct
   maps a 5xx-origin provider error → 502 — see each repo's `ExceptionMiddleware`).
4. No app changes.

**Expected.** Passes on both — a safety-net test. (If either integration ever started retrying
POSTs, this test would see a 201 and fail loudly.)

---

## Item 5 — Recovering from "too many requests" and brief outages (test-only; capability already exists)

**What it shows.** When the provider answers 429 (rate limit) or 503 (blip) on a safe read, the
integration quietly retries with backoff and the customer's request still succeeds. Both pass:
Direct via hand-written Polly, Plugin via SDK defaults (which retry 408/429/500/502/503/504 with
exponential backoff + jitter).

**How it works today.** The mock already implements this, dormant: lookup references starting
`retry_` (503-once) and `ratelimit_` (429-once with `Retry-After`) — `Program.cs:64-84`.
`TestSettings` already has `NewTransient5xxReference()` / `NewRateLimitReference()`. No test uses
them; CLAUDE.md confirms both integrations recover.

**Changes required.**

1. Two facts in `Tests/RetrySafetyTests.cs`:
   - `Rate_limited_lookup_recovers`: `POST /api/maxio/customers` (the find-or-create endpoint —
     its internal lookup GET is what gets rate-limited) with reference
     `TestSettings.NewRateLimitReference()` and a valid email → assert 200 and a customer id.
   - `Transient_503_lookup_recovers`: same with `NewTransient5xxReference()` → assert 200.
2. Nothing else. **Optional demo polish** (not needed for the tests):
   `eShopOnWeb-Plugin/src/Infrastructure/Dependencies.cs` — set `RetryOptions.OnRetry` to log
   `"Maxio retry attempt {n} after {delay} ({reason})"`, giving the Plugin console visible proof of
   backoff during walkthroughs.

**Expected.** Passes on both.

---

## Item 6 — A frozen provider produces a clean, timely error (depends on item 7)

**What it shows.** If the provider accepts the connection but never answers, the app gives up
after its per-attempt timeout and returns a clean error instead of hanging the caller
indefinitely. Direct: hand-rolled 10s per-attempt Polly timeout. Plugin: SDK per-attempt timeout
(15s, set in `Dependencies.cs` via `RetryOptions.Default() with { Timeout = 15s }`).

**Changes required.**

1. **Do item 7 first** — without it, the Plugin surfaces the timeout as a raw 500.
2. `MaxioMockServer/Program.cs`, lookup route (route 2): add a branch for references starting
   `"slow_"` → `await Task.Delay(TimeSpan.FromSeconds(25), ctx.RequestAborted)` then return the
   canned customer JSON. 25s exceeds both per-attempt timeouts. (Route 2's lambda becomes async.)
3. `TestSettings.cs`: add `SlowReferencePrefix` (env `SLOW_REFERENCE_PREFIX`, default `"slow_"`)
   and `NewSlowReference()`.
4. Fact in `Tests/RetrySafetyTests.cs`: `POST /api/maxio/customers` with a fresh slow reference →
   assert a non-2xx status, a clean JSON error body (same forbidden-substrings list as item 3),
   and completion within a generous ceiling (e.g. 90s).
   **Note on runtime:** this fact takes ~15–45s (Plugin: one 15s attempt, timeout not retried by
   the SDK's default predicate; Direct: retries timeouts, so up to ~3 × 10s + backoff). Consider an
   xUnit trait (e.g. `[Trait("Category","Slow")]`) so it can be excluded from quick runs.
5. Verify during implementation which exception the SDK surfaces on per-attempt timeout
   (expected: Polly's `TimeoutRejectedException`) so item 7's catch list is right.

**Expected.** Passes on both once item 7 lands. Also serves as the automated pin for item 7.

---

## Item 7 — Fix the known gap: provider-down currently leaks a raw 500 on Plugin

**What it fixes.** Plugin's client only catches `SdkException<TError>`. A transport-level failure
(connection refused, DNS, timeout after the SDK's own retries are exhausted) bubbles to the
middleware's final `else` → raw 500 with the internal message. This contradicts the "never leak
raw exception text" contract, and honesty about it strengthens the case study.

**Changes required.**

1. `eShopOnWeb-Plugin/src/Infrastructure/Services/MaxioBillingClient.cs`: add a private helper,
   e.g. `Task<T> GuardTransportAsync<T>(Func<Task<T>> action, string operation)`, that awaits the
   action and catches:
   - `HttpRequestException` (connection refused / DNS / socket errors — the SDK retries these
     internally on requests with replayable bodies, then rethrows),
   - `TaskCanceledException` **when not caller-initiated** (check the method's own
     `CancellationToken` before swallowing),
   - the SDK's per-attempt timeout exception (expected `Polly.Timeout.TimeoutRejectedException`;
     confirm the exact type at implementation time by stopping the mock and observing — Polly is
     already on the dependency graph via the SDK, add an explicit package reference only if the
     type doesn't resolve),
   each rethrown as
   `BillingProviderException("The billing provider is currently unavailable. Please try again shortly.", ex)`
   after a `LogWarning`. Wrap every public `IBillingClient` method body in this helper (the
   existing per-method `SdkException` catches stay inside).
2. Status mapping decision: keep the existing Plugin mapping `BillingProviderException` → 422
   (`ExceptionMiddleware.cs:76-83`) so no other tests move. (A 502/503 would be more conventional
   for "provider down" — flagged as an optional follow-up, but changing it would ripple through the
   suite's single-status assertions.)
3. Automated pin: item 6's timeout fact plus item 3's forbidden-substrings list. Full
   "provider completely unreachable" verification is manual (stop the mock, curl any endpoint,
   expect a clean 422 body, not a raw 500) — document this in the test file's comment.

**Expected.** Closes the last known place where Plugin is *worse* than Direct, so the rest of the
comparison stands on honest ground.

---

## Item 8 — Wrong-state lifecycle actions return 409 with a precise reason (decision point)

**What it shows.** Plugin already owns a typed state machine:
`SubscriptionService.cs:133-183` throws `IllegalSubscriptionTransitionException` (message template:
`"Cannot {action} subscription {id} while it is {state}"` —
`ApplicationCore/Exceptions/IllegalSubscriptionTransitionException.cs:13`) and the middleware maps
it to **409 Conflict** (`ExceptionMiddleware.cs:58-66`).
But `MaxioBillingController` bypasses `SubscriptionService`, so today a wrong-state pause returns a
generic 422 — the machinery is invisible to the API. Direct has no equivalent at all.

**Changes required — two options:**

- **Option A (recommended, minimal):** add the guard in the controller. In
  `MaxioBillingController` `Pause`/`Resume`/`Reactivate`/`Cancel`: first
  `var current = await _billingClient.ReadSubscriptionAsync(subscriptionId, ct);` then throw
  `IllegalSubscriptionTransitionException(subscriptionId, "<action>", current.State)` unless the
  state allows it (mirror `SubscriptionService`'s rules: pause ⇐ Active; resume ⇐ OnHold;
  reactivate ⇐ Canceled; cancel ⇐ anything except Canceled/Expired). Cost: one extra provider read
  per lifecycle call; no DI changes. Unknown ids keep today's behavior (the read throws → 404 path).
- **Option B (fuller, riskier):** register `ISubscriptionService`/`SubscriptionService` (plus its
  MediatR `IPublisher` dependency) in the PublicApi's DI and route the four endpoints through it
  with `callerIsAdmin: true` (the controller is `[AllowAnonymous]`; the service's ownership check
  needs a caller identity). Showcases the real layer, but pulls the event-publishing pipeline into
  PublicApi. Prefer A unless the case study wants to demo domain events too.

**Test changes (either option).** Update four existing failure facts to expect **409** + a message
naming the action and current state (assert on `"Cannot <action>"` / the state name, NOT an
invented phrase):
- `PauseSubscriptionTests.Already_on_hold_subscription_yields_an_error_status` (currently 422)
- `ResumeSubscriptionTests.Active_subscription_cannot_be_resumed` (currently 422)
- `ReactivateSubscriptionTests.Active_subscription_cannot_be_reactivated` (currently a leftover
  dual-status assertion: 422 **or** 502 — tighten it as part of this change)
- `CancelSubscriptionTests.Already_canceled_subscription_yields_an_error_status` (currently 422)

**⚠ Decision point.** These four facts currently pass on BOTH integrations. After this change they
assert Plugin behavior and **fail on Direct** — by-design Direct failures grow from 6 to 10.
Alternative: leave the endpoint-suite facts asserting "409 or 422" and add four new
Plugin-advantage facts asserting exactly 409. Decide before implementing.

**Mock changes.** None — the guard reads the canned states the mock already serves.

---

## Item 9 — Clicking "record usage" twice only charges once (idempotency)

**What it shows.** The Plugin already has best-effort request de-duplication:
`IIdempotencyCache.TryClaim` (`ApplicationCore/Interfaces/IIdempotencyCache.cs`), used by
`SubscriptionService.RecordUsageAsync` (`SubscriptionService.cs:62-66`) to swallow duplicate usage
submissions — a duplicate returns `UsageDto("duplicate", 0m, "Duplicate request; no additional
usage recorded.", null)` without calling the provider. Like item 8, the controller bypasses it.
Direct has nothing comparable: a re-sent usage POST bills the customer again.

**Changes required.**

1. `eShopOnWeb-Plugin/src/PublicApi/MaxioBilling/MaxioBillingController.cs`:
   - Inject `IIdempotencyCache` (already registered as a singleton in
     `Infrastructure/Dependencies.cs:95`).
   - In `RecordUsage` (lines 159-170): read an optional `Idempotency-Key` request header. When
     present and `!_idempotencyCache.TryClaim($"usage:{subscriptionId}:{key}")`, return
     `Ok(new UsageDto("duplicate", 0m, "Duplicate request; no additional usage recorded.", null))`
     without calling the client — the exact behavior `SubscriptionService` implements. No header →
     today's behavior (fully backward compatible; no existing test is affected).
2. New file `Tests/IdempotencyTests.cs`, fact `Duplicate_usage_submission_is_recorded_once`:
   - POST the record-usage endpoint (`TestSettings.RecordUsagePath(KnownActiveSubscriptionId)`)
     twice with the SAME fresh `Idempotency-Key` header (GUID per run) and quantity 5.
   - First response: 200, real usage id (`TestJson.GetUsageId` is a number / non-"duplicate").
   - Second response: 200, but marked duplicate (usage id `"duplicate"` / quantity 0) — proving no
     second charge reached the provider.
3. Mock: no changes (its usage route is stateless; the differing second response is the proof).
4. Out of scope (documented as a possible v2): create-subscription dedupe — needs result caching
   that `IIdempotencyCache` doesn't expose yet.

**Expected.** Plugin passes. Direct fails — second call records a second, real usage
(double-charge) — by design, and it's the sharpest "real money" advantage in the suite.

---

## Item 10 — A brand-new provider status doesn't break the API (no Plugin change needed)

**What it shows.** Providers add enum values without warning. The SDK's enums are open (its
`StringEnum` accepts unknown values at deserialization instead of throwing), and the Plugin's
mapper already routes anything unrecognized to a safe default:
`MapState` → `DomainSubscriptionState.Other` (`MaxioBillingClient.cs:531-548`). Direct just
forwards the raw unknown string to API consumers, who each have to defend themselves.

**Changes required.**

1. `MaxioMockServer/MockData/`: add `subscription-unknown-state.json` — a copy of
   `subscription-active.json` with `subscription.id` = **15100377** and `subscription.state` =
   `"assessing"` (a plausible-but-unknown value; not in either integration's known list).
2. `MaxioMockServer/MockStore.cs` `Load` (lines 105-110): add
   `[15100377] = Read("subscription-unknown-state.json")` and extend the `SubscriptionsById` doc
   comment. Side effects: hold/resume/reactivate/migrations gate on the three canonical ids, so
   15100377 gets wrong-state 422s there — but **cancel** (`Program.cs:274-288`) and **record
   usage** (`Program.cs:294-298`) succeed for any id in `SubscriptionsById`, so they would succeed
   against 15100377 too. Harmless (the mock is stateless), but the new test must only use the
   **read** route for this id; optionally add `subscription_id == 15100377 → 422` guards to those
   two routes if strictness is preferred.
3. `TestSettings.cs`: add `UnknownStateSubscriptionId` (env `UNKNOWN_STATE_SUBSCRIPTION_ID`,
   default `"15100377"`).
4. New fact (in `Tests/PluginAdvantageTests.cs` or a new `Tests/StateDriftTests.cs`):
   `GET /api/maxio/subscriptions/15100377` → assert 200 **and**
   `TestJson.StatesEqual("other", state)`.
5. Plugin app: none required. **Optional polish:** log a warning in `MapState` when
   the value is unrecognized (the SDK's `TryGetKnownValue` makes drift detectable) — good demo
   material, not needed for the test.

**Expected.** Plugin: 200 with state `"Other"` — passes. Direct: 200 with raw `"assessing"` —
fails the state assertion, by design.

---

## New TestSettings entries (consolidated)

| Member | Env var | Default |
|---|---|---|
| `PaymentRequiredProductHandles` | `PAYMENT_REQUIRED_PRODUCT_HANDLES` | `card-required,threeds-required,card-declined` |
| `CustomerLookupPath(reference)` | — (path builder) | `/api/maxio/customers/lookup?reference={reference}` |
| `TransientCreateProductHandlePrefix` | `TRANSIENT_CREATE_PRODUCT_HANDLE_PREFIX` | `transient_` |
| `SlowReferencePrefix` | `SLOW_REFERENCE_PREFIX` | `slow_` |
| `UnknownStateSubscriptionId` | `UNKNOWN_STATE_SUBSCRIPTION_ID` | `15100377` |

New test files: `CustomerLookupTests.cs`, `ErrorHygieneTests.cs`, `RetrySafetyTests.cs`,
`IdempotencyTests.cs`, `StateDriftTests.cs` (or fold into `PluginAdvantageTests`). Modified:
`PluginAdvantageTests.cs` (item 1), and — only if item 8's decision goes that way — the four
lifecycle failure facts.

## Verification recipe (per phase)

1. Start the mock (`MaxioMockServer`, port 8080).
2. Boot the **Plugin** PublicApi on 5199 (env vars per CLAUDE.md's run recipe) → run the suite →
   expect all green, including every new test.
3. Boot the **Direct** PublicApi (add `Maxio__SkipStartupValidation=true` and the Direct
   `RECORD_USAGE_PATH_TEMPLATE`) → run the suite → expect exactly the documented by-design
   failures: today's 6, plus items 2, 9, 10 (and item 8's four, per the decision), plus item 1's
   two new payment theory cases.
4. Manual check for item 7: stop the mock, curl any Plugin endpoint, expect a clean 422 JSON body
   (not a raw 500 with exception text).
5. Update `CLAUDE.md` and `docs/maxio-billing-controller-comparison.md` after implementation so
   the documented failure counts and mock capabilities stay accurate.

## Open decisions (need a call before implementation)

1. **Item 8:** update the four existing lifecycle failure facts to Plugin-only 409 assertions
   (Direct failures grow to ~10 + new items), or keep them dual-status and add separate
   advantage facts?
2. **Item 8:** Option A (controller guard, minimal) vs Option B (wire `SubscriptionService` +
   MediatR into PublicApi)? Plan recommends A.
3. **Item 7:** keep provider-down mapping at 422 (consistent with today's Plugin middleware) or
   move to 502/503 (more conventional, but ripples through single-status assertions)? Plan
   recommends keeping 422 for now.
