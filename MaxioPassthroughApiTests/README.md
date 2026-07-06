# MaxioPassthroughApiTests

Black-box HTTP tests for the `MaxioBillingController` endpoints exposed by the eShopOnWeb PublicApi
(Direct and Plugin). Both integrations expose the same operation set under `/api/maxio`, one endpoint
per `IBillingClient` method, returning **flattened, provider-agnostic DTOs** — not Maxio's raw
envelopes — with errors remapped by the shared `ExceptionMiddleware`.

This project is **standalone**: it lives outside both `eShopOnWeb-Direct.sln` and `eShopOnWeb-Plugin.sln`,
has **no project references**, and talks to the running PublicApi purely over HTTP. The same tests run
against **either** integration — just point `PUBLICAPI_BASEURL` at whichever one is running.

## Why one suite covers both, and where it can't

11 endpoints are identical (or near-identical) in route and request shape between the two integrations
— see `../docs/maxio-billing-controller-comparison.md` for the full route-by-route comparison. This
suite covers exactly those 11. Where the two integrations' *response* shapes genuinely differ (field
names, casing, or status codes), tests either:
- assert only the fields common to both shapes (e.g. `productHandle`, `state` — compared via
  `TestJson.StatesEqual`, since Plugin renders `on_hold` as `"OnHold"`, not snake_case),
- read an id via a tolerant getter (`TestJson.GetSubscriptionId` / `GetUsageId` / `GetCustomerId`),
  since the two integrations use different property names/types for provider ids.

Every shared-suite status assertion pins the single status code verified live on both integrations —
`Expect.StatusOneOf` exists in `Expect.cs` for a set of acceptable codes, but nothing in the suite
currently needs it (an earlier `ReactivateSubscriptionTests` case hedged `{422, 502}` without live
verification; live-checked, both integrations return 422, so it's pinned to that single code now).

**Not covered:** the customer-lookup-only endpoint (`GET /api/maxio/customers/lookup`) exists only on
Plugin — Direct has no equivalent — so it doesn't fit the shared suite's "same endpoint, both
integrations" premise. Revisit separately if that endpoint gets a Direct-side equivalent.

## Route-divergence auto-skip (endpoint-missing 404 → Skipped)

When a test hits a route the running integration does **not** expose, ASP.NET returns a **routing 404**
— an empty body with no `Content-Type`. A **genuine** not-found from the controller/middleware instead
carries the app's JSON error body (`{"StatusCode":404,"Message":"…"}`, or an RFC-7807 ProblemDetails
body). The suite uses that difference to tell the two apart: the status helpers in `Expect`
(`Status` / `StatusOneOf` / `StatusInRange`) call `TestJson.IsEndpointMissing`, and on an empty-body 404
they **Skip** the test (via `Xunit.SkippableFact`) rather than pass or fail it. A genuine API 404 falls
through to the normal assertion, so a test that expected 404 still **Passes**.

Effect: a route this integration doesn't expose (e.g. `customers/lookup` on Direct, or a mismatched
`RECORD_USAGE_PATH_TEMPLATE`) shows up as **Skipped** in the JUnit XML (`<skipped />`), not as a
misleading pass/fail. Skipped tests are true framework skips — visible in the `dotnet vstest` summary and
the JUnit report.

> Edge case: an endpoint that deliberately returns a *bare* `NotFound()` with an empty body would be
> classified as endpoint-missing and skipped. In practice both integrations return a JSON body on every
> genuine 404 (ExceptionMiddleware or ProblemDetails), so this is theoretical today — but a new
> empty-body business 404 would skip rather than pass.

## Shared tests (23) — green on both integrations

| File | Endpoint | Covers |
|---|---|---|
| `ListPlansTests` | List plans | success only (no caller input to vary) |
| `FindOrCreateCustomerTests` | Find-or-create customer | success + idempotency on repeat calls; blank-email 400 |
| `SubscriptionTests` | List customer subscriptions | success; unknown customer → error |
| `ReadSubscriptionTests` | Read subscription | success; unknown subscription → error |
| `CreateSubscriptionTests` | Create subscription | success; unknown product; unknown customer |
| `PauseSubscriptionTests` | Pause subscription | success; already on-hold → error |
| `ResumeSubscriptionTests` | Resume subscription | success; not on-hold → error |
| `ReactivateSubscriptionTests` | Reactivate subscription | success; not canceled → error |
| `CommitPlanChangeTests` | Migrate plan (immediate) | success; unknown product; not-active subscription |
| `CancelSubscriptionTests` | Cancel subscription | success; already canceled → error |
| `RecordUsageTests` | Record usage | success; unknown subscription → error |

## Plugin-advantage tests (3) — `PluginAdvantageTests`

Unlike the shared suite (which asserts only the common subset so it stays green on either integration),
these assert the **superior** behavior of the Plugin (SDK) integration. They are designed to **pass
against Plugin and FAIL against Direct** — the failure pins the exact behavior Direct lacks. Run the
suite against Plugin for an all-green result; running it against Direct shows exactly these 3 red.

| Test | Plugin (passes) | Direct (fails) |
|---|---|---|
| `Missing_subscription_returns_404_not_found` | Maps Maxio 404 → `SubscriptionNotFoundException` → **404** | No not-found special case → generic `BillingProviderException` → **422** |
| `Find_or_create_customer_recovers_from_a_concurrent_create_race` | Catches the create conflict and re-reads → **200** with the existing id | No recovery → the conflict surfaces as **422** |
| `Payment_failure_surfaces_a_typed_payment_verification_error` | Classifies card/payment errors as `PaymentVerificationRequiredException`; body carries "Additional payment information is required…" | Returns Maxio's raw provider messages only — the typed phrase is absent (both statuses are 422) |

Two mock behaviors back these (see `../MaxioMockServer`): a `race_`-prefixed customer reference
(`RACE_REFERENCE_PREFIX`) and the `card-required` product handle (`PAYMENT_REQUIRED_PRODUCT_HANDLE`).
The missing-subscription case needs no mock support.

## Prerequisites to run end-to-end

1. **The Maxio mock server** (canned Maxio responses on `http://localhost:8080`):
   ```sh
   cd ../MaxioMockServer
   dotnet run
   ```
   See `../MaxioMockServer/README.md` for the full set of known ids/handles this suite relies on
   (product family `527890`, customer `cust_12345`→`98765`, subscriptions `15100121`/`15100210`/`15100299`
   in active/on_hold/canceled states, component `641814`/`api-calls`).

2. **One eShopOnWeb PublicApi** (Direct *or* Plugin), booted with its Maxio calls routed to the mock. An
   in-memory-DB boot needs no SQL Server or real Maxio credentials:
   ```sh
   # from eShopOnWeb-Plugin/ or eShopOnWeb-Direct/
   UseOnlyInMemoryDatabase=true ASPNETCORE_URLS=http://localhost:5199 ASPNETCORE_ENVIRONMENT=Development \
   Maxio__BaseUrl=http://localhost:8080 Maxio__Subdomain=acme Maxio__ApiKey=test-api-key \
   Maxio__ProductFamilyId=527890 Maxio__ProductFamilyHandle=acme-projects \
   Maxio__MeteredComponentHandle=api-calls Maxio__MeteredComponentId=641814 \
   Maxio__SkipStartupValidation=true \
     dotnet run --project src/PublicApi --no-launch-profile
   ```
   `Maxio__SkipStartupValidation` only exists on Direct's `MaxioSettings` (Plugin doesn't verify the
   metered component at boot, so the setting is a no-op there — harmless to always pass it).

3. **This test project**, pointed at that PublicApi.

## Run

```sh
# default base URL is https://localhost:5099
dotnet test

# or target a specific instance / port
PUBLICAPI_BASEURL=http://localhost:5199 dotnet test
```

## Configuration (environment variables)

All optional; defaults match the mock's canned data and Plugin's route shapes.

| Variable | Default | Meaning |
|---|---|---|
| `PUBLICAPI_BASEURL` | `https://localhost:5099` | Base URL of the PublicApi under test |
| `LIST_PLANS_PATH` | `/api/maxio/product-families/527890/products` | List-plans route (identical on both integrations) |
| `RECORD_USAGE_PATH_TEMPLATE` | `/api/maxio/subscriptions/{subscriptionId}/components/1/usages` | Record-usage route — **differs by integration**; set to `/api/maxio/subscriptions/{subscriptionId}/usages` for Direct (no component-id segment) |
| `KNOWN_CUSTOMER_REFERENCE` | `cust_12345` | A reference the mock resolves |
| `KNOWN_CUSTOMER_ID` | `98765` | A customer id the mock has subscriptions for |
| `UNKNOWN_CUSTOMER_REFERENCE` | `no_such_customer_ref` | A reference the mock 404s |
| `UNKNOWN_CUSTOMER_ID` | `99999999` | A well-formed but unknown numeric customer id |
| `KNOWN_ACTIVE_SUBSCRIPTION_ID` | `15100121` | The mock's canned active subscription |
| `KNOWN_ON_HOLD_SUBSCRIPTION_ID` | `15100210` | The mock's canned on-hold subscription |
| `KNOWN_CANCELED_SUBSCRIPTION_ID` | `15100299` | The mock's canned canceled subscription |
| `UNKNOWN_SUBSCRIPTION_ID` | `88888888` | A well-formed but unknown numeric subscription id |
| `KNOWN_PRODUCT_HANDLE` | `gold` | The active subscription's current product |
| `ALTERNATE_PRODUCT_HANDLE` | `zero-dollar-product` | A second known product, used as a migration target |
| `UNKNOWN_PRODUCT_HANDLE` | `no-such-plan` | Drives the "unknown product" validation path |
| `RACE_REFERENCE_PREFIX` | `race_` | Prefix for the concurrent-create-race reference used by `PluginAdvantageTests` |
| `PAYMENT_REQUIRED_PRODUCT_HANDLE` | `card-required` | Product handle that triggers a card/payment 422 on create-subscription (used by `PluginAdvantageTests`) |

`TRANSIENT_5XX_REFERENCE_PREFIX` / `RATE_LIMIT_REFERENCE_PREFIX` drive the mock's `retry_` / `ratelimit_`
transient-failure behaviors. Note these are **not** a Plugin-vs-Direct differentiator: both integrations
retry idempotent GETs (Direct via a `Microsoft.Extensions.Http.Resilience`/Polly pipeline, Plugin via the
SDK's default `RetryOptions`), so both recover from a transient 429/503 on the customer-lookup GET. They
remain as a mock capability, not a current test.

If the PublicApi isn't reachable, tests fail with a clear message telling you to start it and point it at
the mock (rather than an opaque connection error).

## Test metadata / reports

Every test carries two xUnit `[Trait]`s (defined once in `MaxioTraits.cs`) so an executor — human, CI, or
an agent — can tell which Maxio API operation and which category a test belongs to without opening the
test source. This is metadata only: test bodies still call nothing but the PublicApi over HTTP.

- **`Category`** — `endpoint` (the 11 shared-suite files), `plugin-advantage`, or `safety-net`, mirroring
  the grouping in `../docs/test-cases-by-integration-2026-07-03.md`.
- **`MaxioApi`** — the underlying Maxio operation signature, verbatim `METHOD /path` from
  `../openAPI/openapi.yaml` (e.g. `POST /subscriptions/{subscription_id}/hold.json`). Tests backed by more
  than one Maxio call (e.g. find-or-create, which does a lookup then a create) carry multiple `MaxioApi`
  traits.

**xUnit traits do not appear in the default console output and are not reliably written to `.trx`.** Two
ways to actually make use of them:

- **Filter a run by trait:**
  ```sh
  dotnet test --filter "Category=endpoint"
  dotnet test --filter "MaxioApi=POST /subscriptions/{subscription_id}/hold.json"
  ```
- **Get a machine-readable report** via the `JunitXml.TestLogger` package reference — it serializes every
  trait as a `<property>` on each `<testcase>`:
  ```sh
  dotnet test --logger "junit;LogFilePath=maxio-results.xml"
  ```
  ```xml
  <testcase classname="MaxioPassthroughApiTests.Tests.PauseSubscriptionTests" name="Active_subscription_is_paused">
    <properties>
      <property name="Category" value="endpoint" />
      <property name="MaxioApi" value="POST /subscriptions/{subscription_id}/hold.json" />
    </properties>
  </testcase>
  ```
  A caller parses this XML to join each test's pass/fail with the Maxio operation it targets — e.g. to
  regenerate the by-integration comparison report programmatically instead of by hand. `tests.runsettings`
  also declares this logger under `LoggerRunSettings`, so `dotnet test --settings tests.runsettings` (or
  Rider's "Use specific .runsettings file") emits `maxio-results.xml` with no extra flags.

  `LogFilePath`, when relative, resolves against the working directory `dotnet test` is invoked from — not
  the build output directory. Running from this folder writes `./maxio-results.xml` here; running from
  elsewhere (e.g. the repo root) writes it there instead. Pass an absolute path to pin the location
  regardless of invocation directory: `--logger "junit;LogFilePath=$(pwd)/maxio-results.xml"`.

## Adding tests for a new endpoint

Add a `Tests/*.cs` file with a success case and, where a failure is reachable through valid caller
input, a failure case. Add the class-level `[Trait(MaxioTraits.Category, …)]` and one or more
`[Trait(MaxioTraits.Api, …)]` (add a new constant to `MaxioTraits.cs` if the operation isn't already
listed, matching `../openAPI/openapi.yaml`'s path + method exactly) — see "Test metadata / reports" above.
If the two integrations' response shapes differ, add a tolerant getter to
`TestJson.cs` rather than hardcoding one integration's property names. If the two integrations' error
statuses differ for the same failure, assert a status *set*, not a single code — trace each client's
exception-handling path (`MaxioBillingClient.cs` in each `Infrastructure/`) through to its
`ExceptionMiddleware` to find the real set. Then wire the mock route (see
`../MaxioMockServer/README.md`) and update `../docs/maxio-billing-controller-comparison.md` if the
route comparison changes.
