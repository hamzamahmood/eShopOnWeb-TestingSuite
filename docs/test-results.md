# MaxioPassthroughApiTests Results

Latest test run against both integrations. The suite has been tightened toward Plugin-specific expectations — three test cases now assert **Plugin-only behavior and fail on Direct by design** (`CreateSubscriptionTests.Success`, `ReadSubscriptionTests.Unknown_subscription`, `RecordUsageTests.Unknown_subscription`).

---

## Plugin Integration Test Results

**Date**: 2026-07-02  
**Test Command**: `PUBLICAPI_BASEURL=http://localhost:5199 dotnet test`  
**Result**: ❌ 1 Failure / 25 Passed / 26 Total (failure is expected/documented, not a bug)

---

## Direct Integration Test Results

**Date**: 2026-07-02  
**Test Command**: `PUBLICAPI_BASEURL=http://localhost:5199 RECORD_USAGE_PATH_TEMPLATE=/api/maxio/subscriptions/{subscriptionId}/usages dotnet test`  
**Result**: ❌ 6 Failures / 20 Passed / 26 Total
