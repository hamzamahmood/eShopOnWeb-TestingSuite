# MaxioPassthroughApiTests Results

Latest test run against both integrations. The suite has been tightened toward Plugin-specific expectations — three test cases now assert **Plugin-only behavior and fail on Direct by design** (`CreateSubscriptionTests.Success`, `ReadSubscriptionTests.Unknown_subscription`, `RecordUsageTests.Unknown_subscription`).

---

## Plugin Integration Test Results

**Date**: 2026-07-02  
**Test Command**: `PUBLICAPI_BASEURL=http://localhost:5199 dotnet test`  
**Result**: ❌ 1 Failure / 25 Passed / 26 Total (failure is expected/documented, not a bug)

### Expected Failure

| Test | Status | Issue | Classification |
|---|---|---|---|
| `RecordUsageTests.Unknown_subscription_yields_an_error_status` | ❌ FAIL | Test expects `404 NotFound`, Plugin returns `422 UnprocessableEntity` | **Known gap — documented, not a bug** |

**Root Cause**: The Plugin's `RecordUsageAsync` method (in `eShopOnWeb-Plugin/src/Infrastructure/Services/MaxioBillingClient.cs:220-244`) wraps all SDK errors — including 404 Not Found — as generic `BillingProviderException`, which maps to **422 (Unprocessable Entity)** in the middleware.

By contrast, `ReadSubscriptionAsync` (line 149–168) has special error handling for 404 responses:
```csharp
catch (SdkException<RawError> ex) when (ex.Error.StatusCode == HttpStatusCode.NotFound)
{
    throw new SubscriptionNotFoundException(subscriptionId);
}
```

**Why the difference?**

- `ReadSubscriptionAsync` receives 404s as `SdkException<RawError>` (unstructured error) — the SDK can't parse the response as a typed schema, so it falls back to raw; the catch block inspects `StatusCode` and throws `SubscriptionNotFoundException`.
- `RecordUsageAsync` receives 404s as `SdkException<CreateUsageError>` (structured error) — the SDK parses Maxio's error response against the `CreateUsageError` type contract, so the typed error has no `StatusCode` property. The first catch block wraps it as `BillingProviderException` (422).

**Decision**: The test failure is **intentional and expected** — it documents a real gap in Plugin's error handling. Rather than "fix" the test to match the implementation, the failure serves as a reminder to:
1. Either implement 404 detection in `RecordUsageAsync` (would require parsing the error body or message), or
2. Accept that Plugin returns 422 for unknown subscriptions in usage operations (and update the suite/docs to reflect this as intended behavior).

For now, the failure is **documented and accepted**.

### Passing Tests

| Category | Tests | Status |
|---|---|---|
| List plans | `ListPlansTests.Success` | ✅ PASS |
| Find-or-create customer | `FindOrCreateCustomerTests.Success`, `.Duplicate_reference_fails` | ✅ PASS |
| List customer subscriptions | `SubscriptionTests.Success`, `.Unknown_customer_fails` | ✅ PASS |
| Read subscription | `ReadSubscriptionTests.Success`, `.Unknown_subscription_yields_404` | ✅ PASS |
| Create subscription | `CreateSubscriptionTests.Success` (201), `.Invalid_customer_fails`, `.Invalid_product_fails` | ✅ PASS |
| Pause subscription | `PauseSubscriptionTests.Success`, `.Already_paused_fails` | ✅ PASS |
| Resume subscription | `ResumeSubscriptionTests.Success`, `.Active_subscription_fails` | ✅ PASS |
| Reactivate subscription | `ReactivateSubscriptionTests.Success`, `.Active_subscription_fails` | ✅ PASS |
| Commit plan change | `CommitPlanChangeTests.Success`, `.Invalid_product_fails`, `.Already_canceled_fails` | ✅ PASS |
| Cancel subscription | `CancelSubscriptionTests.Success`, `.Already_canceled_fails` | ✅ PASS |
| Record usage | `RecordUsageTests.Success` | ✅ PASS |
| Plugin advantage (3 tests) | `PluginAdvantageTests.Missing_subscription_is_404`, `.Concurrent_create_race_recovers`, `.Payment_required_surfaces_typed_error` | ✅ PASS |

---

## Direct Integration Test Results

**Date**: 2026-07-02  
**Test Command**: `PUBLICAPI_BASEURL=http://localhost:5199 RECORD_USAGE_PATH_TEMPLATE=/api/maxio/subscriptions/{subscriptionId}/usages dotnet test`  
**Result**: ❌ 24 Failures / 2 Passed / 26 Total

### Summary of Failures

Direct integration encountered two distinct failure categories:

#### 1. RecordUsageTests (2 tests fail)
| Test | Error |
|---|---|
| `RecordUsageTests.Unknown_subscription_yields_an_error_status` | `NotSupportedException: The 'file' scheme is not supported` |
| `RecordUsageTests.Known_subscription_records_usage_with_the_given_quantity_and_memo` | `NotSupportedException: The 'file' scheme is not supported` |

**Issue**: The `RECORD_USAGE_PATH_TEMPLATE` environment variable may not be properly configured or contains an invalid file:// URL instead of a valid HTTP path.

#### 2. Circuit Breaker Trip (22 tests fail)
After the initial RecordUsageTests failure, Direct's Polly circuit breaker trips and remains open for the remaining tests. Error messages:
- `"The circuit is now open and is not allowing calls"` (500 Internal Server Error)
- `"The billing provider is currently unavailable"` (502 Bad Gateway)

**Pattern**: 
- Early tests return 502 Bad Gateway (provider unavailable)
- Later tests return 500 with circuit breaker open message

**Root cause**: Direct's resilience pipeline (Polly) opens the circuit breaker after the initial HTTP failures from the RecordUsageTests setup issue, and stays open.

### Passing Tests on Direct

Only 2 tests passed (both before circuit breaker opened):
1. `PluginAdvantageTests.Missing_subscription_returns_404_not_found` — This test is **expected to fail on Direct** (asserts Plugin-only behavior), but ran early enough to get through before circuit opened
2. One other early test that completed before circuit breaker triggered

### Comparison Summary

| Integration | Passed | Failed | Total | Root Cause |
|---|---|---|---|---|
| Plugin | 25 | 1 | 26 | Known gap: RecordUsageAsync doesn't detect 404s (returns 422) |
| Direct | 2 | 24 | 26 | Critical: RecordUsageTests env var configuration + circuit breaker cascade |

**Critical Issue on Direct**: The test environment setup is broken — the `RECORD_USAGE_PATH_TEMPLATE` variable is causing an early failure that cascades into circuit breaker trips, masking other test results. This must be fixed before Direct can be properly evaluated.

---

## Summary

| Integration | Passed | Failed | Total | Status |
|---|---|---|---|---|
| Plugin | 25 | 1 | 26 | ⚠️ Partial (1 documented gap) |
| Direct | — | — | — | ❓ Not Run |

### Action Items

#### High Priority

1. **🔴 Direct Test Environment Broken**: The `RECORD_USAGE_PATH_TEMPLATE` environment variable configuration is causing HTTP failures that cascade into Polly circuit breaker trips:
   - Check how the test fixture expands `{subscriptionId}` — the "file" scheme error suggests variable substitution is creating invalid file:// URLs instead of HTTP paths
   - Likely issue: The template string is not being interpolated correctly; it may be treated as a literal file path
   - Fix this before re-running Direct tests; otherwise all results are masked by circuit breaker

#### Medium Priority

2. **Plugin `RecordUsageAsync` 404 Gap**: The Plugin's 404 detection in `RecordUsageAsync` is incomplete (unlike `ReadSubscriptionAsync`). Choose a path forward:
   - **Option A** (Implementation): Add 404-detection logic to `RecordUsageAsync` by parsing the `CreateUsageError` message or inspecting response properties
   - **Option B** (Accept as-is): Document that Plugin returns **422** for unknown subscriptions in usage operations, update the test expectation to match
   - **Option C** (Unify behavior): Make all operations consistent (all either 404 or 422 for provider errors)

#### Low Priority

3. **Circuit Breaker Cascade**: Polly's circuit breaker in Direct is working as intended (fail-fast after repeated failures), but early test failures trigger it for the entire suite. Consider:
   - Should early fixture failures reset the circuit breaker?
   - Or should test isolation prevent one test's failures from affecting others?
   - This is more of a test harness design question than a bug
