# Test coverage gaps — MaxioPassthroughApiTests

_Analysis date: 2026-07-08. Reviewed: `MaxioPassthroughApiTests/` (38 cases, 16 files),
`MaxioMockServer/` (13 routes), `openAPI/openapi.yaml`, and both integration controllers
(`eShopOnWeb-Plugin` / `eShopOnWeb-Direct` `MaxioBillingController.cs`)._

## Coverage baseline

The suite tests **11 of ~15 controller actions**, mostly one success + one failure each. Four
controller actions have **zero coverage**, and several tested endpoints are missing error cases the
code paths and OpenAPI actually support.

Legend: **[T]** = new test file/case only · **[M]** = new mock route/behavior · **[S]** = new
`TestSettings` entry.

---

## Group 1 — Entirely untested endpoints (highest value)

Exist in **both** integrations, no test at all; most also lack a backing mock route.

| # | Endpoint (Plugin route · Direct route) | Missing | Changes |
|---|---|---|---|
| 1 | **Preview plan change** — `POST subscriptions/{id}/migrations/preview` (both) | test + mock route | **[M]** `POST /subscriptions/{id}/migrations/preview.json` · **[S]** `MigrationsPreviewPath(id)` · **[T]** `PreviewPlanChangeTests.cs` |
| 2 | **Usage summary / balance** — Plugin `GET …/components/{id}/summary` · Direct `GET …/component-balance` | test + mock route | **[M]** `GET /subscriptions/{id}/components/{comp}.json` (readSubscriptionComponent) · **[S]** configurable path template (routes diverge, like `RECORD_USAGE_PATH_TEMPLATE`) · **[T]** `UsageSummaryTests.cs` |
| 3 | **Metered-component verify** — Plugin `GET metered-component/verify` (**204 no body**) · Direct `GET metered-component` (**200 + data**) | test only (mock route exists) | **[S]** configurable path (route + success-status diverge: 204 vs 200) · **[T]** `MeteredComponentTests.cs` |
| 4 | **Cancel at end of period** — Direct `POST subscriptions/{id}/delayed_cancel` · Plugin `DELETE …` w/ `timing:"EndOfPeriod"` | test + mock route | **[M]** `POST /subscriptions/{id}/delayed_cancel.json` · **[T]** add end-of-period case to `CancelSubscriptionTests.cs` |
| 5 | **Schedule plan change at renewal** — Direct `PUT subscriptions/{id}` · Plugin `POST …/migrations` w/ `timing:"AtRenewal"` (locally computed, no Maxio call) | test + mock route (Direct only) | **[M]** `PUT /subscriptions/{id}.json` (updateSubscription) for Direct · **[T]** add `timing:"AtRenewal"` case to `CommitPlanChangeTests.cs` |

The Plugin `timing` field (#4, #5) is the key divergence worth exercising: Plugin unifies these behind
`migrations`/`DELETE` + `timing`; Direct uses dedicated routes.

---

## Group 2 — Missing failure/error cases on already-tested endpoints

Distinct error paths, not repeats of existing assertions.

- **List plans — no failure case.** The 404 (`ProductFamilyNotFound`, in OpenAPI) is **not reachable**
  through either controller — path id is inert; both clients query the configured (known) family.
  Recommendation: document as intentionally-untestable; **[T]** optional comment/trait, no new assertion.
- **Find-or-create customer — missing `first_name`/`last_name` divergence.** Only blank *email* is
  tested (both → 400). Direct requires all four fields (400 on blank first/last); Plugin enforces only
  its DTO-`[Required]` set. Untested behavioral divergence. **[T]** add blank-first-name case
  (Plugin-advantage-style).
- **Create subscription — missing `customer_id` → potential 500 leak (likely real bug).** Plugin's
  `CreateSubscription` does `request.Subscription.CustomerId!.Value` with **no null check** → `NullRef`
  → raw 500 (violates "never leak" contract). Direct returns clean 400. **[T]** add missing-customer_id
  case — **verify live before asserting**; may surface a real Plugin bug.
- **Record usage — quantity edge cases.** Missing-quantity (Direct 400 client-side; Plugin via DTO),
  negative/zero quantity — untested. **[T]** extend `RecordUsageTests.cs`.
- **Cancel already-canceled — error-shape divergence not asserted.** Mock returns Maxio's *singular*
  `{"error":…}` key here (vs plural `{"errors":[…]}` elsewhere); only status (422) checked. **[T]** add
  AI-verified body assertion that the "already canceled" reason is surfaced (exercises singular-key
  error parsing in each client).

---

## Group 3 — Input-validation & divergence tests (no mock changes)

Hit controller logic before Maxio → **[T]** only.

- **Non-numeric subscription id.** Plugin `InvalidId` → clean **400 ValidationProblem**; Direct `:int`
  route constraint → route miss → **404/bare**. Clear divergence, untested. Good `PluginAdvantageTests`
  case (Plugin gives REST-correct 400). **Verify live** (400 vs 404).
- **Invalid `timing` value** on cancel (`"Later"`) and migrate. Plugin → 400 ValidationProblem; Direct
  ignores `timing` (dedicated routes). Untested.
- **Malformed/empty request body** (e.g. `POST subscriptions` with no `subscription` wrapper, or empty
  body) — model-binding behavior untested on both.

---

## Group 4 — OpenAPI error cases: covered vs genuinely missing

Maxio's spec is thin on error codes: operations document **200/201 + 422** (`Error-List-Response`), a
few add **404** (`cancelSubscription`, `initiateDelayedCancellation`, `listProductsForProductFamily`),
plus a global **401**.

- **422 (validation)** — well covered by status across the suite. ✓
- **404** — covered for read-subscription and list-customer-subscriptions. `listProductsForProductFamily`
  404 is **unreachable** (inert path id — see Group 2).
- **401 Unauthorized** — documented everywhere but **not black-box testable** (mock ignores
  `Authorization`; both integrations inject configured creds). Note it; not a gap to fill.
- **Mock `StrictValidationMiddleware` never exercised.** Enforces required wrapper key, required attrs,
  JSON types, `payment_collection_method` enum → spec-shaped 422. No test sends a contract-violating
  body. Reachability limited (controllers reshape requests), but create-customer required-attrs path is
  reachable and untested. **[T]** one direct-contract test.
- **`previewSubscriptionProductMigration` documents 204** in spec, but controllers return **200 + quote
  body** — spec/impl mismatch to capture when adding the preview test (Group 1 #1).

---

## Suggested implementation order & where changes land

1. **Mock** (`MaxioMockServer/Program.cs` + `MockStore.cs`) — add 4 routes: `migrations/preview.json`,
   `subscriptions/{id}/components/{comp}.json`, `delayed_cancel.json`, `PUT subscriptions/{id}.json`.
   `MockStore` already has reusable helpers (`WithProduct`, `WithState`, `NewUsageJson`); add a
   `PreviewJson`/`ComponentBalanceJson` builder or new `MockData/*.json`.
2. **`TestSettings.cs`** — add `MigrationsPreviewPath`, `USAGE_SUMMARY_PATH_TEMPLATE`,
   `METERED_COMPONENT_PATH` (divergent — follow the `RECORD_USAGE_PATH_TEMPLATE` pattern).
3. **New test files** — `PreviewPlanChangeTests.cs`, `UsageSummaryTests.cs`, `MeteredComponentTests.cs`;
   extend `CancelSubscriptionTests`, `CommitPlanChangeTests`, `RecordUsageTests`, `FindOrCreateCustomerTests`.
4. **Divergence/validation cases** (Group 3) — no mock work; slot into `PluginAdvantageTests.cs`
   (non-numeric id, missing customer_id) and relevant endpoint files.

**Verify live** (boot each integration against the mock) before locking assertions on: Group 2
"missing customer_id → 500 on Plugin" and Group 3 "non-numeric id 400 vs 404" — both likely pin real
divergences (per the repo's "compile-only misses real bugs" lesson).

**Recommended starting point:** the **preview plan change** endpoint (Group 1 #1) — whole endpoint,
both integrations, zero coverage. Add mock route + settings + test together and verify live on both.

---

## Untested controller actions — quick reference

| Action | Plugin route | Direct route | Backing mock route exists? |
|---|---|---|---|
| Preview plan change | `POST …/migrations/preview` | `POST …/migrations/preview` | ❌ |
| Usage summary / balance | `GET …/components/{id}/summary` | `GET …/component-balance` | ❌ (`GET …/components/{comp}.json`) |
| Metered-component verify/read | `GET metered-component/verify` (204) | `GET metered-component` (200) | ✅ (`GET /product_families/{id}/components/{comp}.json`) |
| Cancel at end of period | `DELETE …` + `timing:"EndOfPeriod"` | `POST …/delayed_cancel` | ❌ |
| Schedule change at renewal | `POST …/migrations` + `timing:"AtRenewal"` (local) | `PUT subscriptions/{id}` | ❌ (Direct needs `PUT …/{id}.json`) |
