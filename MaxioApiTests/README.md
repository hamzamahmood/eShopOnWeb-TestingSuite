# MaxioApiTests

Black-box HTTP tests for billing API endpoints. This project is standalone with no project references
and talks to the running API purely over HTTP.

## Test Design

Each content test splits its assertions in two:
- **Status codes** are asserted deterministically in-code
- **Response contents** are verified by an AI payload verifier that matches on semantic meaning
  rather than exact field names, making tests robust to API response shape variations

## Route-divergence auto-skip

When a test hits a route the API does not expose, it receives an empty-body 404 (no route match).
The suite distinguishes this from a genuine API 404 (with a JSON error body) and skips the test rather
than failing it. This surfaces route divergence as Skipped results instead of misleading failures.

## AI payload verification

Response **bodies** are checked by an AI verifier (`Ai/OpenAIApiService.cs`, built on
`Microsoft.Extensions.AI`), not by key-based field reads. A content test keeps its deterministic
`Expect.Status(...)` check, obtains the verifier via `OpenAIApiService.Require(intent)`, then passes the
raw response body plus plain-English **rules** to `VerifyAsync`; the model returns a structured
`VerificationReport` (per-rule pass/fail + reason) and `Expect.AiPassed` asserts it. Because the model
matches on *meaning*, the same rules hold even when responses name, case, or nest fields differently.
The verifier judges only the body; status codes remain the job of the deterministic `Expect.Status`
helpers.

**On by default; fails (not skips) when unavailable.** When a key is resolvable the content-comparison
tests run and AI-verify the body. When no key is configured (`AI_API_KEY` unset) — or you override
`AI_COMPARISON_ENABLED=false` — `OpenAIApiService.Require` **fails** the test with a message explaining
how to configure it, rather than skipping. A content assertion that cannot run is a hard failure, not a
silent pass, so it can't be mistaken for verified. (Only the status-only and behavioral tests, which
don't call `Require`, run without a key.)

| Variable | Default | Meaning |
|---|---|---|
| `AI_COMPARISON_ENABLED` | `true` | Master switch for AI payload verification; with it `false` (or no key) the content tests **fail** rather than skip |
| `AI_API_KEY` | *(none)* | API key for the OpenAI / OpenAI-compatible endpoint |
| `AI_MODEL` | `gpt-5.5` | Judge model used to verify response payloads |
| `AI_ENDPOINT` | *(OpenAI cloud)* | Optional OpenAI-compatible base-URL override (Azure / local / proxy) |
| `AI_USE_JSON_SCHEMA` | `true` | Constrain output with a JSON schema; set `false` for endpoints lacking native schema support |

> The AI verdict is not perfectly deterministic. Keep rules literal and unambiguous (judge *value*, not
> *validity*); the deterministic status assertions remain the hard gate.

## Test Suite

Tests cover API endpoints for billing operations including customer management, subscription lifecycle,
plan changes, and usage tracking. Each test validates both status codes and response content.

## Prerequisites to run end-to-end

1. **The mock server** (canned API responses on `http://localhost:8080`):
   ```sh
   cd ../MaxioMockServer
   dotnet run
   ```

2. **The API under test**, booted with its calls routed to the mock:
   ```sh
   dotnet run --project <path-to-api-project>
   ```

3. **This test project**, pointed at that API via `PUBLICAPI_BASEURL`.

## Run

```sh
# default base URL is http://localhost:5000
dotnet test

# or target a specific instance / port
PUBLICAPI_BASEURL=http://localhost:5199 dotnet test
```

## Configuration (environment variables)

All optional; defaults match the mock's canned data.

| Variable | Default | Meaning |
|---|---|---|
| `PUBLICAPI_BASEURL` | `http://localhost:5000` | Base URL of the API under test |
| `AI_API_KEY` | *(none)* | OpenAI API key for response content verification |
| `AI_MODEL` | `gpt-5.5` | Model for response verification |
| `AI_COMPARISON_ENABLED` | `true` | Enable AI-based response verification |

Additional test-specific variables are available in `TestSettings.cs` for customizing known test data values.

## Test metadata

Tests are tagged with xUnit traits for categorization and filtering by endpoint and category.

- **`Category`** — the test category (e.g. `endpoint` or `safety-net`).
- **`MaxioApi`** — the underlying Maxio operation signature as `METHOD /path` (e.g.
  `POST /subscriptions/{subscription_id}/hold.json`). Tests backed by more than one Maxio call (e.g.
  find-or-create, which does a lookup then a create) carry multiple `MaxioApi` traits.

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
  <testcase classname="MaxioApiTests.Tests.PauseSubscriptionTests" name="Active_subscription_is_paused">
    <properties>
      <property name="Category" value="endpoint" />
      <property name="MaxioApi" value="POST /subscriptions/{subscription_id}/hold.json" />
    </properties>
  </testcase>
  ```
  A caller can parse this XML to join each test's pass/fail with the Maxio operation it targets.
  `tests.runsettings` also declares this logger under `LoggerRunSettings`, so
  `dotnet test --settings tests.runsettings` (or Rider's "Use specific .runsettings file") emits
  `maxio-results.xml` with no extra flags.

  `LogFilePath`, when relative, resolves against the working directory `dotnet test` is invoked from — not
  the build output directory. Running from this folder writes `./maxio-results.xml` here; running from
  elsewhere (e.g. the repo root) writes it there instead. Pass an absolute path to pin the location
  regardless of invocation directory: `--logger "junit;LogFilePath=$(pwd)/maxio-results.xml"`.

## Adding tests for a new endpoint

Add a `Tests/*.cs` file with a success case and, where a failure is reachable through valid caller
input, a failure case. Add the class-level `[Trait(MaxioTraits.Category, …)]` and one or more
`[Trait(MaxioTraits.Api, …)]` (add a new constant to `MaxioTraits.cs` if the operation isn't already
listed) — see "Test metadata" above. For assertions on the response **body**, don't parse fields by key —
state plain-English rules and verify them with the AI verifier (`OpenAIApiService.VerifyAsync` +
`Expect.AiPassed`), so the test is robust to differing/renamed field names (see "AI payload
verification"). For error cases, verify the status live and pin the single code the API actually returns.
Then wire the mock route in `MaxioMockServer`.
