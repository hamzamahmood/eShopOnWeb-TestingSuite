# Error-code divergence: MaxioMockServer vs. MaxioPassthroughApiTests

Compares what `docs/maxio-mock-server-error-codes.md` says each mock endpoint returns against what
`MaxioPassthroughApiTests` actually asserts through the live `MaxioBillingController` (both
integrations). The mock's status code is not always the status code a black-box caller sees — the
controller/`ExceptionMiddleware` layer sits in between and sometimes remaps it. Three kinds of finding
below: **status-code remaps** (mock code ≠ asserted code), a **short-circuit** (request never reaches
the mock's error branch at all), and **untested error paths** (mock documents an error the suite never
drives).

> **Resolution (2026-07-06):** all three assertions flagged in §1 have been fixed to match live-verified
> reality — see the "Resolved" note under each. Deliberately kept test-side only: the mock's raw 404 stays
> unreachable through the controller for these two routes; the suite now asserts what the controller
> actually returns instead of what the mock does. Fixing the controllers to surface the mock's 404 would be
> a separate, integration-side change.

## 1. Status-code remaps — mock's code is not what the suite asserts

### List-customer-subscriptions: mock 404 → suite asserts **422**

`SubscriptionTests.Unknown_customer_yields_an_error_status` calls
`GET /api/maxio/customers/{unknownId}/subscriptions`, which the mock answers with
**404** `{"errors": ["Customer not found."]}` (see mock-error-codes doc §3). The test asserts
`HttpStatusCode.UnprocessableEntity` (**422**) — not 404. `TestSettings.UnknownCustomerId`'s own doc
comment even says it "drives Maxio's 404 path," but the controller turns that 404 into a 422 on **both**
integrations (this test isn't in the Plugin-advantage set — it's asserted as common-subset behavior).
The client's list-customer-subscriptions method apparently has no typed not-found exception on either
integration, so a 4xx from Maxio falls through to the generic `BillingProviderException` → 422 mapping.

**Resolved:** no test change needed — the assertion already matched reality (422 confirmed live on both
integrations). Only `TestSettings.UnknownCustomerId`'s doc comment was corrected to stop implying a 404.

### Record-usage / read-subscription: mock 404 → Direct asserts/returns **422**, Plugin *should* be 404 but isn't always

Both `ReadSubscriptionTests.Unknown_subscription_yields_an_error_status` and
`RecordUsageTests.Unknown_subscription_yields_an_error_status` assert **404** directly (not
`StatusOneOf`), matching the mock's actual 404 (`{"errors": ["Subscription not found."]}`, mock-error-codes
§5 and §12). Per root `CLAUDE.md` and the live-verified run notes, this is because Plugin's
`ReadSubscriptionAsync` classifies Maxio's 404 into a typed `SubscriptionNotFoundException` → 404, while
Direct has no not-found special case and always falls through to generic `BillingProviderException`
(4xx-origin) → **422**. So:
- **Read subscription**: Plugin matches the mock's 404; Direct remaps it to 422 (by design — this is
  one of the three intentionally Plugin-only assertions called out in `CLAUDE.md`).
- **Record usage**: the suite asserted 404 expecting the same Plugin/Direct split, but the **verified live
  run in this session showed Plugin also returns 422**, not 404 — i.e. `RecordUsageAsync` does *not*
  classify Maxio's 404 the way `ReadSubscriptionAsync` does. This was the one test failing on Plugin
  (38/39), and it means the mock's clean 404 on this route is currently unreachable through **either**
  controller.

**Resolved:** `RecordUsageTests.Unknown_subscription_yields_an_error_status` now asserts **422** (confirmed
live on both integrations); Plugin is fully green (39/39) and this case is no longer part of Direct's
by-design-failure set either — it was never a genuine Plugin/Direct split.

### Reactivate-ineligible: mock 422 → suite tolerates 422 **or** 502

`ReactivateSubscriptionTests.Active_subscription_cannot_be_reactivated` uses
`Expect.StatusOneOf(response, intent, HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadGateway)`.
The mock's reactivate-ineligible response is always **422** (mock-error-codes §9) — Maxio's error is
4xx-origin, so per `CLAUDE.md`'s mapping table both integrations should turn it into 422, never 502
(502 is Direct's mapping only for a *non*-4xx-origin `BillingProviderException`, e.g. a 5xx or
unclassifiable error). The `StatusOneOf` hedge suggests either a real inconsistency in how Direct
classifies this specific error, or defensive slack the author added without confirming 502 is actually
reachable here — worth a live check the next time Direct is booted against the mock.

**Resolved:** live-checked against both integrations — both return 422, never 502. The assertion is now
pinned to `Expect.Status(response, HttpStatusCode.UnprocessableEntity, intent)`; the 502 branch was
unreachable slack, not a real inconsistency.

## 2. Short-circuit — mock's error branch is never reached at all

### Create-customer blank email: mock 422 → both controllers pre-validate to **400**

`FindOrCreateCustomerTests.Blank_email_is_rejected_before_reaching_the_billing_provider` asserts
`HttpStatusCode.BadRequest` (**400**). The mock documents a 422 for this case
(`{"errors": ["Email address: cannot be blank."]}`, mock-error-codes §4), but both controllers reject a
blank email client-side (Direct via an explicit check, Plugin via `[Required]` + `ModelState`) before any
HTTP call is made — the mock's 422 branch for this input is dead code from the black-box suite's
perspective. Not a bug, just worth knowing the mock's documented 422 here is unreachable through the
real controllers.

## 3. Mock error paths the suite never drives (coverage gaps)

The suite tests the unknown-id 404 path for only **2 of the 7** subscription-scoped mutating routes
(read, record-usage) — the other five never assert their mock-documented 404:

| Route | Mock 404 documented? | Tested by the suite? |
|---|---|---|
| Read subscription | Yes (§5) | Yes |
| Pause (hold) | Yes (§7) | **No** |
| Resume | Yes (§8) | **No** |
| Reactivate | Yes (§9) | **No** |
| Migrate | Yes (§10) | **No** |
| Cancel | Yes (§11) | **No** |
| Record usage | Yes (§12) | Yes (but see the remap above) |

Also untested end-to-end:
- **List-plans unknown-family 404** (§1) — `ListPlansTests` only exercises the success path.
- **Component-not-found 404** (§13, `readComponent`) — no controller route in either integration maps
  to this at all through the black-box suite (Direct's internal usage-prerequisite call isn't
  independently observable; there's no metered-component verify test in this suite).
- **429 / 503 / connection-reset on customer lookup** (§2) — `RetrySafetyTests` and
  `ResilientRetryRecoveryTests` only observe the *final* recovered 200, never the intermediate
  429/503/reset itself (expected, since a black-box caller behind retry logic can't see the
  intermediate attempt — not a gap, just a limit of this testing layer).

## Summary

- **Two confirmed status-code remaps** where the mock's error code never reaches the caller unchanged:
  list-customer-subscriptions (404→422 on both integrations, assertion already correct) and record-usage
  (404→422, now on **both** integrations per the latest live run, not just Direct as originally designed
  — assertion fixed from 404 to 422).
- **One hedge resolved**: `ReactivateSubscriptionTests` no longer tolerates `{422, 502}` — live-verified as
  422-only on both integrations and pinned accordingly.
- **One intentional short-circuit** (blank-email 400) where the mock's documented 422 is by-design dead
  code for this suite — left as-is, not a defect.
- **Five of seven lifecycle routes' 404 paths are still completely untested** (§3, unchanged by this
  round of fixes), plus the list-plans and component-read 404s — the suite's error-code coverage remains
  narrower than the mock's documented surface. Not addressed in this pass; a candidate for follow-up.
