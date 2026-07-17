# API Integration Benchmark — Turnkey Playbook

> **What this is.** The operational bridge between `API_INTEGRATION_BENCHMARK.md` (the *methodology* —
> what to measure and why) and a real codebase. It pairs a **shipped, reusable harness** (this kit)
> with a **per-integration profile** (three JSON files you author) so an agent can point the benchmark at
> an API integration and produce a scorecard without hand-writing any instruments.
>
> **The core idea.** The whole provider-specific surface collapses into three data files. The harness —
> a fault/drift/recording mock, the readiness gate, and the quality scorer — is generic and never edited
> per integration. You (or an agent) describe *this* provider + integration in a `profile/`, and the same
> binaries do the rest. The mock talks to the integration **black-box over HTTP**, so it scores a
> Python / Go / TS / Java integration exactly as well as a .NET one; only the D3/D4 static-analysis
> adapter is stack-specific.
>
> **Trust by construction.** Before any score is reported, the playbook makes you *prove the instruments
> discriminate* (§4 Step 4) — a known-good run goes green and injected defects go red. If the mock is
> wrong, the happy path won't pass and you fix it before scoring. That is what makes autonomous
> construction safe (it is `API_INTEGRATION_BENCHMARK.md` §1.4 made operational).

---

## 0. TL;DR — the whole loop

```bash
# run every command from the kit root — the folder with Harness.slnx (benchmark/turnkey/ in the monorepo)
dotnet build Harness.slnx                                  # build the kit once

# (recommended) generate a profile DRAFT from the provider's OpenAPI spec, then complete the app-side
dotnet run --project Harness.Profiler -- --spec <openapi.yaml> --out profiles/<name> --name <name>

# Part A — readiness gate (DONE) and holdout (ROBUST)
dotnet run --project Harness.Gate -- --profile <profileDir> --app-project <appCsproj> --mode public
dotnet run --project Harness.Gate -- --profile <profileDir> --app-project <appCsproj> --mode holdout

# Part B — quality scorecard D1–D4
dotnet run --project Harness.Quality -- --profile <profileDir> --tree <treeRoot> \
  --app-project <appCsproj> --mode all --out runs/scorecard.json

# …or run all of the above (build → gate public + holdout → quality) in one shot, with every output +
# a RUN_LOG skeleton captured under runs/<name>/:
./run.ps1 -Profile <name> -App <appCsproj>          # Windows / pwsh
./run.sh  --profile <name> --app <appCsproj>        # POSIX
```

You supply `<profileDir>/`: **`profile.json`** (how to boot + config + secrets + leak set + analysis
globs), **`contract.json`** (the provider's wire routes + fixtures), **`optable.json`** (the operations +
their expectations + drift plan + roles). Full schema in §5. A complete, validated example ships at
`profiles/maxio-eshop/` (§9).

---

## 1. Scope & fit — read this first

This kit is **turnkey within a defined envelope**, and degrades gracefully (asks you for input) outside
it rather than guessing.

**Turnkey when all of these hold:**
- The provider speaks **REST/JSON over HTTP**.
- The integration is reachable as a **running HTTP service** with drivable endpoints (or you can add a
  thin driver shim — §8).
- You have an **independent provider contract** (OpenAPI spec or official docs) to author the mock from.

**Degrades gracefully (needs a human decision) when:**
- *No provider spec exists* → the agent reverse-engineers routes from the integration's own calls and
  **must flag reduced validity** and get the op table confirmed (§8). The oracle is only as independent
  as its source.
- *The integration is a library, not a service* → write a tiny HTTP driver shim exposing each operation
  (§8).

**Out of scope (for now):** gRPC / GraphQL / message-queue transports (different mock+drift mechanics);
the agent-build comparison mode (D5 dev-speed, cost-to-DONE, Part C statistics) — this kit targets
**evaluating one existing integration** (Part A + D1–D4, with D6/D7 as light add-ons).

**The validity rule (non-negotiable).** Author `contract.json` from the *provider's* contract, **not**
from what the integration under test happens to parse. A mock shaped to the integration measures
"is it self-consistent," not "is it correct." Sentinel id/value checks are robust to this; response
*shapes* (field names, envelopes, types) must come from the provider.

---

## 2. What ships vs. what you author

| Shipped in this kit — never edited per integration | You author per integration (`profile/`) |
|---|---|
| **Harness.Core** — fault engine, drift engine (P1–P8), recorder, HTTP clients + oracle primitives, model types, boot helpers | **profile.json** — boot command, config/env, secrets, leak set, analysis selectors |
| **Harness.Mock** — generic host: serves `contract.json` through record/fault/drift middlewares + `/__mock` control plane | **contract.json** — provider wire routes → guarded cases → fixture bodies |
| **Harness.Gate** — R/E/S/C property templates resolved by op-role + data-driven C1 + holdout | **optable.json** — operations, expectations, drift plan, role assignments |
| **Harness.Quality** — D1/D2 (drift replay) + D3/D4 (static analysis, .NET adapter) | — |

---

## 3. The procedure

### Step 1 — Discover the integration
Read the codebase and the provider contract, and collect:
- The **provider base URL** and how the integration is configured to reach it (env var / settings key).
- The **app routes** the integration exposes (e.g. `GET /api/billing/plans`) and, for each, which
  **provider wire call** it makes (method + path fragment). This app-route ↔ upstream-call map is the
  op table's backbone; the integration's own source is the authoritative source for it.
- The **operations** worth scoring — aim for coverage across CRUD + list + a lifecycle action.
- The **secrets** in play (API key, subdomain/tenant) and the config key(s) that gate startup.
- **Which request header carries auth** — `Authorization`, or a custom one like `api_key` / `X-Api-Key`.
  The mock's S2 check needs to know this (declare it in `contract.authHeaders`; §4.2).
- **Whether the app's OWN routes require auth** (distinct from the provider's auth above). If the
  integration's endpoints are `[Authorize]` / behind a gateway, the harness must present a credential to
  reach them — set `app.headers` (§4.1); the JWT recipe is in §5.1. Without it every op 401s before it
  touches the integration.

### Step 2 — Author the profile bundle

**If you have the provider's OpenAPI spec (recommended), generate a draft first** with `Harness.Profiler`
— it fills the entire provider side authoritatively, so you only complete the integration side:
```bash
dotnet run --project Harness.Profiler -- --spec <openapi.(yaml|json)> --out profiles/<name> --name <name>
```
A multi-file spec is first bundled into one self-contained document (external `$ref`s inlined into
`components`), then parsed with **Microsoft.OpenApi** (native 3.1 + JSON-Schema semantics). It emits
(example-first — fixtures carry the provider's **real** envelopes/field-names/types from the spec's
response examples, with schema synthesis when an operation has no example):
- **`contract.json`** — near-final: one route per operation with a faithful success body + a 404 default
  on by-id reads. This is the validity-critical, tedious part, done mechanically. Because the shapes come
  from the *provider's* spec (not the integration under test), the oracle stays independent (§1 rule).
- **`optable.draft.json`** — each op's `upstream`, `mustContain` (the id from the example), and drift
  candidates filled; `app.method/path` + `roles` + `holdout` left as **`TODO`**.
- **`profile.skeleton.json`** — `leak` defaults + an auth hint; boot config + `analysis` selectors `TODO`.

The generator prints a checklist of exactly what's left (and lists any operations that had **no example**,
so you can add those fixtures by hand). It cannot know the *integration's* surface, so **you complete:**
- **optable**: set each op's `app.method/path` (the integration route that triggers the call — read the
  integration's routes/source); prune to the ops it implements; set `roles` + `holdout`; add `deep{}` +
  `expectDollars` on ~6–8 representative ops for D1/D2 depth (spread the drift profiles across them).
- **contract**: add the `422`/reject cases (missing-field, bad-reference) the gate needs for E1/C3 —
  the generator only produces success + 404.
- **profile**: `routePrefix`, boot config, `secretConfigKeys` (removed for S3), `secretValues` (scraped
  for S1), and the `analysis` selectors.

Rename `optable.draft.json` → `optable.json` when done.

**Without a spec**, author the three files by hand (copy `profiles/maxio-eshop/` and edit) following the
same rules — and flag reduced oracle-independence per §7, since the contract's shapes then come from the
integration rather than an independent source.

> **Prefer a fresh spec-seeded `contract.json` over reusing another integration's.** Copying a sibling
> provider's contract inherits its fixture *identities* (customer references, ids, handles) — you then pay
> a realignment tax (the recorder will 404 until they match) and risk a stale oracle drifting from the real
> provider shapes. If you have the spec, regenerate the provider side; reuse only the app-side authoring
> (roles / holdout / the app routes). Same provider, different app is the one case where contract reuse is
> safe — but re-verify the fixtures against the recorder (Step 3) either way.

### Step 3 — Mock-sanity gate (fail fast on a wrong mock)
Run the public gate against the **integration under test**. Every `C1.*` must be green — that proves the
mock serves each operation's happy path with the expected values and the integration consumes them. If a
C1 fails, fix the contract/optable before going further (a red C1 usually means a fixture field name or a
guard is wrong, not that the integration is broken).

### Step 4 — Discrimination-validate (the "gate on the gate")
Prove the instruments actually separate good from bad **on this codebase** before trusting a score. The
cheapest way: inject a defect into a *copy* of the integration and confirm the right check reddens.
Minimum set (mirrors `API_INTEGRATION_BENCHMARK.md` §C.2):

| Inject into a copy of the integration | Must redden |
|---|---|
| Leak the exception/stack in error bodies | E1 · E3 · E4 · R3 |
| Return canned data without calling the provider | the C1 for that op · R3 · R6 |
| Drop/disable auth | S2 |
| Retry a write (POST) on 5xx | R5 (upstream count > 1) |
| Remove the per-attempt timeout | R4 (elapsed ≥ ceiling) |
| Read a renamed field by a hardcoded key | D2 cells → SILENT-WRONG |

If a metric doesn't move when you break the thing it measures, the instrument measures nothing — fix it.
(The shipped `reference/` integration + its `BREAK=` toggles demonstrate every one of these; §9.)

**Never mutate the tree you'll score.** Do the injection on a throwaway copy or a scratch branch and
restore with `git checkout` before running Part B (confirm `git status` is clean). If you control the
integration, prefer wiring the defects behind a `BREAK=` env toggle (the `reference` project's pattern) so
discrimination re-runs touch no files at all.

### Step 5 — Part A readiness gate → DONE / ROBUST
```bash
dotnet run --project Harness.Gate -- --profile profiles/<name> --app-project <appCsproj> --mode public
dotnet run --project Harness.Gate -- --profile profiles/<name> --app-project <appCsproj> --mode holdout
```
`public` all-green ⇒ **DONE**. `public` **and** `holdout` all-green ⇒ **ROBUST**. A large DONE≫ROBUST gap
means the integration overfit its visible checks. The agent under evaluation must only ever be given the
`public` invocation; you run `holdout` afterward.

### Step 6 — Part B quality scorecard → D1–D4
```bash
dotnet run --project Harness.Quality -- --profile profiles/<name> --tree <treeRoot> \
  --app-project <appCsproj> --mode all --out runs/scorecard.json
```
Produces the four deterministic dimensions: **D1** correctness-depth, **D2** drift resilience/safety +
the 4-way confusion matrix, **D3** maintainability (wire-coupling, complexity, nesting, LOC), **D4**
security (source findings, transitive deps, vulnerable packages). `--mode` also accepts `deep`, `drift`,
`metrics`, `security`, `dynamic`, `static` to run subsets.

### Step 7 — D6 / D7 (optional, lower-trust)
- **D6 tests:** does the integration ship its own tests? Report coverage/mutation if a runner exists;
  report **0 as a finding** if none. (Agents frequently ship untested integrations.)
- **D7 readability:** a blind LLM-judge ensemble per `API_INTEGRATION_BENCHMARK.md` §C.6 — never the
  headline; treat its flags as hypotheses to confirm deterministically.

### Step 8 — Assemble the scorecard + diagnose
Report the gate tier first (`DONE`/`ROBUST`), then the D1–D7 scorecard as the headline (not a single
number). Use `API_INTEGRATION_BENCHMARK.md` §7 to turn low dimensions into concrete fixes.

---

## 4. Profile schema reference

All three files are camelCase, comment-tolerant (`"//": "..."`) and trailing-comma-tolerant.

### 4.1 `profile.json`
```jsonc
{
  "name": "my-integration",
  "app": {
    "project": "",                       // app csproj; usually passed per-run via --app-project instead
    "runCommand": [],                    // non-.NET: explicit launch tokens; ${appUrl}/${mockUrl} substituted
    "baseUrl": "http://127.0.0.1:5111",  // where the app under test listens
    "readyPath": "/",                    // polled until the app answers
    "routePrefix": "/api/billing",       // prepended to every op's app.path
    "launchArgs": ["--no-launch-profile"],// extra process args (ASP.NET launchSettings override, etc.)
    "headers": {                         // OPTIONAL: attached to EVERY harness→app request (gate happy-path
      "Authorization": "Bearer <jwt>"    //   + fault drives, holdout, quality drift). For an integration
    },                                   //   whose OWN routes require auth — test scaffolding to reach the
                                         //   endpoints; does NOT change the integration. Recipe in §5.1.
    "config": {                          // env/config the app boots with; ${mockUrl} points it at the mock
      "Provider__BaseUrl": "${mockUrl}",
      "Provider__ApiKey": "test-api-key"
    },
    "secretConfigKeys": ["Provider__ApiKey"],  // S3 removes these → app must fail to boot
    "secretValues": ["test-api-key"]           // S1 scrapes logs → these must be absent
  },
  "mock": { "baseUrl": "http://localhost:8080", "contract": "contract.json" },
  "leak": {                              // Part A forbids ALL of these in any failure body;
    "generic": ["System.", "   at ", "StackTrace", "Traceback"],  // D2 forbids only the internals
    "extra":   ["test-api-key"]          // provider secrets/wire names; underscored names are wire, not leaks
  },
  "analysis": {
    "stack": "dotnet",                   // static-analysis adapter (§6)
    "integrationPathPattern": "/(Billing|Provider)",  // regex on /-path → the integration's OWN files
    "depProjectFile": "Infrastructure.csproj"          // project whose dependency graph D4 counts
  }
}
```

### 4.2 `contract.json`
The declarative mock. A request matches a route by `method` + `path` (with `{param}` captures); the
route's `cases` are tried top-to-bottom and the first whose `when` guard holds is served.
```jsonc
{
  "authHeaders": ["Authorization"],      // header(s) that count as authenticated for S2; omit ⇒ Authorization.
                                         //   set to the provider's real header if custom (e.g. ["api_key"])
  "fixtures": {                          // named JSON bodies; {{path.x}} {{query.x}} {{body.a.b}} interpolate
    "activeSub": { "subscription": { "id": 950001, "state": "active" } }
  },
  "routes": [
    { "method": "GET", "path": "/subscriptions/{sid}.json", "cases": [
      { "when": { "pathIn": { "sid": ["950001"] } }, "status": 200, "bodyFixture": "activeSub" },
      { "status": 404 }                  // catch-all default (no `when`)
    ] },
    { "method": "POST", "path": "/subscriptions.json", "cases": [
      { "when": { "bodyMissing": ["subscription"] }, "status": 422, "body": { "errors": ["blank"] } },
      { "when": { "bodyValueNotIn": { "subscription.product_handle": ["pro-plan"] } }, "status": 422,
        "body": { "errors": ["not found"] } },
      { "status": 201, "bodyFixture": "createdSub" }
    ] }
  ]
}
```
**Guards** (all present conditions must hold): `pathIn`, `queryPresent`, `queryIn`, `bodyPresent`
(paths exist & non-empty), `bodyMissing` (matches when any listed path is absent — the reject branch),
`bodyValueIn`, `bodyValueNotIn` (matches when value ∉ set — the bad-reference branch).
**Interpolation:** a string leaf that is exactly one token (`"{{path.sid}}"`) is coerced to a number when
numeric; embedded tokens splice as text. Property names are preserved verbatim (wire casing survives).
**Auth header (S2).** S2 treats a request as authenticated when any header in `authHeaders` is present.
Omit it for `Authorization`-bearing providers (the default); set it to the provider's actual auth header
(e.g. `["api_key"]`, `["X-Api-Key"]`) or S2 fails even when the integration authenticates correctly. The
`petstore` example uses `["api_key"]`.

### 4.3 `optable.json`
```jsonc
{
  "roles": {                             // resolve the fixed R/E/S/C templates to concrete ops
    "read": "plans", "readById": "read-sub", "write": "create-sub",
    "unknownIdPath": "/subscriptions/999"
  },
  "holdout": {                           // same property classes, different instances (never shown to builder)
    "read": "find-or-create", "readById": "read-sub",
    "write": "create-sub", "write2": "usage", "secretValue": "acme"
  },
  "ops": [
    {
      "id": "read-sub", "scope": 11,
      "app": { "method": "GET", "path": "/subscriptions/950001" },   // path is relative to routePrefix
      "upstream": { "method": "GET", "pathContains": "/subscriptions/950001.json" },
      "gate": { "mustContain": ["950001"], "mustContainAny": [["active"]] },  // Part A C1 (shallow)
      "deep": { "mustContain": ["950001", "active", "pro-plan"] },            // Part B D1 (deeper); optional
      "drifts": [                                                             // Part B D2; optional
        { "label": "rename state", "profile": "rename", "field": "state", "to": "sub_state", "check": "Values" },
        { "label": "new-enum",     "profile": "newenum", "field": "state", "to": "paused_pending",
          "check": "NewEnum", "expect": ["950001", "paused_pending"] }
      ]
    }
  ]
}
```
- `mustContain` = all must appear; `mustContainAny` = each inner group contributes ≥1 (state-representation
  tolerance). `gate` drives Part A C1; `deep` (defaults to `gate`) drives D1; ops **without** `deep` are
  Part-A-only. Add `expectDollars` to `deep` for a cents-vs-dollars units check.
- `drift.profile` ∈ `additive | rename | envelope | retype | union | newenum | remove`;
  `drift.check` ∈ `Values | NewEnum | Units`.
- The **write** role op should carry `invalidBody` (locally-invalid → C3) and `domainErrorBody`
  (provider-rejected → E1).
- `scope` = smallest task size that includes the op (lets a partial integration be scored on what it
  implements). Quality auto-detects scope by probing the max-scope GET ops.
- **`app.preSteps`** (optional) makes a **stateful, multi-step** operation drivable in one shot. It's a list
  of app calls run in order *before* the op; each may `capture` response values (by JSON dotted-path;
  numeric segments index arrays) into named vars that interpolate as `{{capture.NAME}}` into later steps'
  and the op's own `path`/`body`. e.g. a plan-change `commit` that needs a staleness token echoed from a
  prior `preview`:
  ```jsonc
  { "id": "commit", "scope": 11,
    "app": {
      "method": "POST", "path": "/950001/plan-change/commit",
      "body": "{\"stalenessToken\":\"{{capture.tok}}\"}",
      "preSteps": [
        { "method": "POST", "path": "/950001/plan-change/preview",
          "body": "{\"targetProductHandle\":\"basic-plan\"}",
          "capture": { "tok": "stalenessToken" } }   // dotted-path into the preview response
      ]
    },
    "upstream": { "method": "PUT", "pathContains": "/subscriptions/950001.json" },
    "gate": { "mustContain": ["950001"] } }
  ```
  Ops with no `preSteps` (the default) are a single request — byte-identical to the pre-preSteps behavior.
  (The mock still just serves each call's upstream normally; the token round-trips harness↔app.)

---

## 5. Extending the mock (the escape hatch)

The declarative contract covers static fixtures + known/unknown-id + missing-field/bad-reference errors +
created-ids + body echo — enough for most REST/JSON providers. When a provider needs **computed or
stateful** responses (a running balance, a value derived from the request), the mock host is small and
yours to extend: add a bespoke route handler in `Harness.Mock/Program.cs` (or enrich `GuardSpec`/`CaseDef`
in `Harness.Core/Model/Contract.cs`). Anything you add still flows through the same recording / fault /
drift middlewares, so faults and drift keep working. Prefer widening the declarative model over one-off
handlers when the pattern will recur across providers.

### 5.1 Auth scaffolding (reaching `[Authorize]` app routes)

If the integration's **own** routes require auth (JWT bearer, a gateway header), the harness needs a
credential to drive them — otherwise every op 401s before it reaches the integration. Put the credential in
`profile.app.headers` (§4.1); it's attached to every gate/holdout/quality request. This is test scaffolding
to *reach* the endpoints — it does not change the integration, and the gate's S2 still verifies the
integration authenticates **upstream**.

For a symmetric-key JWT (HS256) signed with an in-repo secret, mint one long-lived token and paste it in:
```bash
b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
SECRET='<the in-repo signing secret>'
HDR=$(printf '%s' '{"alg":"HS256","typ":"JWT"}' | b64url)
# claim NAMES/values must match what the app checks (role claim, username claim); exp far in the future:
PLD=$(printf '%s' '{"unique_name":"admin@example.com","role":"Administrators","exp":2051222400}' | b64url)
SIG=$(printf '%s.%s' "$HDR" "$PLD" | openssl dgst -sha256 -hmac "$SECRET" -binary | b64url)
echo "Bearer $HDR.$PLD.$SIG"     # → profile.app.headers.Authorization
```
A token signed with a **repo-known test secret** is safe to commit (it grants nothing outside the test
harness). A **live** provider credential is not — keep those out of a shared/example profile (S1 scrapes
`secretValues` from logs; don't hand it a real secret to find).

---

## 6. Per-stack static analysis (D3 / D4)

The behavioral layers (Part A, D1, D2) are **stack-agnostic** — they only speak HTTP. Only D3/D4 read
source, so only they need a per-stack adapter. The **.NET (C#) adapter ships**. To score another stack,
implement the same two measurements and keep everything else:

| Dimension | .NET adapter (shipped) | Port to another stack |
|---|---|---|
| D3 complexity/nesting | Roslyn syntax walk | tree-sitter / language AST; or `radon` (Py), `gocyclo` (Go), PMD (Java), `ts-morph` (TS) |
| D3 wire-coupling | regex: `.json` URL literals + snake_case field literals + Base64/Basic auth | same idea — count hand-maintained endpoint-path + wire-field string literals; widen the URL regex if endpoints aren't `.json` |
| D3 owned LOC | non-blank/non-comment lines in selected files | same |
| D4 deps | `dotnet list package --include-transitive` (+ `--vulnerable`) | `pip`/`pip-audit`, `npm ls`/`npm audit`, `go list -m all`/`govulncheck`, `mvn dependency:tree` |
| D4 source scan | regex: hardcoded secrets, non-TLS URLs, disabled cert validation | same regexes, stack-agnostic |

File selection (`analysis.integrationPathPattern`) and the dep project (`analysis.depProjectFile`) are
profile-driven and stack-independent.

---

## 7. Graceful degradation

- **No provider spec.** Derive the route table from the integration's own outbound calls, author the
  contract, and **state in the report that the oracle's independence is reduced** — have a human confirm
  the op table + fixture shapes. Sentinel id/value checks still hold; treat "semantically right place"
  claims (D1) as weaker.
- **Library, not a service.** Write a thin HTTP shim that maps one route per operation to a direct call
  into the library, boot the shim as the "app," and point `routePrefix`/op paths at it. The shim is test
  scaffolding, not production code — keep it out of the D3/D4 file selection.
- **Partial integration.** Give ops a `scope`; the tools score only the ops present (quality auto-detects
  scope, the gate runs the whole table). Log what was skipped — never let missing coverage read as green.
- **Weak unknown-resource signal (E2).** Point `roles.unknownIdPath` at a **provider-backed by-id read** so
  the 404 flows through the integration's error mapping (provider 404 → clean 4xx) — that's what E2 is meant
  to test. If the app exposes no such route, a *routing* 404 still passes E2, but it only exercises the
  framework's routing, not the integration; say so in the report (weaker signal) rather than reading it as
  full credit.
- **Stateful / multi-step op.** If an op needs a value from a prior call (a staleness token, a created id),
  drive it with `app.preSteps` (§4.3) instead of dropping it as "uncoverable." Only fall back to a
  documented coverage gap when even a scripted pre-step can't set it up — and log the gap, don't green it.

---

## 8. Worked example — `maxio-eshop` (validated)

The shipped profile `profiles/maxio-eshop/` scores an eShopOnWeb ↔ Maxio billing integration. It was
validated against the origin study this kit was extracted from (that study's execution record is not
bundled in this repo). The **self-contained** part reproduces here directly:

```bash
# run from the kit root (this folder)
dotnet build Harness.slnx

# Part A on the known-good reference → 37/37 public, 5/5 holdout
dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop \
  --app-project reference/Reference.csproj --mode public     # 37/37
dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop \
  --app-project reference/Reference.csproj --mode holdout    # 5/5

# Discrimination-validation (gate on the gate): each BREAK reddens its target check(s)
BREAK=leak       dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj  # E1,E3,E4,R3
BREAK=hardcode   dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj  # C1.plans,R3,R6
BREAK=noauth     dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj  # S2
BREAK=retrywrite dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj  # R5
BREAK=notimeout  dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj  # R4

# Part B needs a produced integration tree to score — point --tree/--app-project at your own tree.
# (For a fully in-repo Part B run, use the petstore example — `--profile profiles/petstore --tree reference-petstore` (command in README) — which scores the bundled reference-petstore/ tree.)
dotnet run --project Harness.Quality -- --profile profiles/maxio-eshop \
  --tree <yourProducedTree> \
  --app-project <yourProducedTree>/src/PublicApi/PublicApi.csproj \
  --mode all --out runs/maxio-eshop.json
```

**Results from the origin study** (cited — the SDK-vs-spec arm trees are not bundled in this repo, so the
`scope22-*` D1–D4 rows below are the study's measured numbers, not reproducible from this repo alone; the
`reference` row *is* reproducible from the commands above):

| | Gate | D1 | D2 resilience / safety / silent-wrong | D3 wire-coupling / avgCC / maxNest / LOC | D4 deps / vuln / source |
|---|---|---|---|---|---|
| reference | 37/37 + 5/5 | — | — | — | — |
| scope22-armA (SDK) | DONE·ROBUST | 100% | 41% / 64% / 8 | 0 / 2.04 / 4 / 793 | 96 / 4 / 0 |
| scope22-armB (spec) | DONE·ROBUST | 100% | 50% / 55% / 10 | 24 / 3.0 / 6 / 869 | 90 / 5 / 0 |

That study *is* the kit's discrimination proof: the instruments surfaced a win for **each** implementation
choice (the spec arm won drift resilience + deps; the SDK arm won wire-coupling + drift safety), so the
suite is not biased toward either. Use `maxio-eshop` as the template for a new profile.

**Second example — a *different* provider (`profiles/petstore/`).** To show the kit isn't wedded to the
API it was built from, `profiles/petstore/` + `reference-petstore/` run the whole benchmark on the Swagger
Petstore API (OpenAPI **3.0**, bare/array bodies, camelCase, `api_key`-header auth — none of which Maxio
exercises). Provider side was seeded by `Harness.Profiler` from the spec, then finished by hand. Result:
gate **22/22 public + 5/5 holdout**, every `BREAK=` case reds its target, D1 100% / D2 resilience 53% /
D3 maxCC 23, LOC 230 / D4 0 findings, 0 deps. It's the reference for authoring a profile against a
camelCase / custom-auth API — run commands are in `README.md`.

---

## 9. Caveats & limits

- **Directional, not powered.** A single tree gives a point score. For claims, produce N trees and apply
  `API_INTEGRATION_BENCHMARK.md` §C.3 statistics.
- **A wrong mock produces confident nonsense.** Steps 3–4 are the guard; do not skip them. Discrimination-
  validation catches most bad harnesses but not one wrong in a way both the happy path and the injected
  defect tolerate — keep the contract faithful to the provider.
- **Wire-coupling's default regexes assume snake_case / `.json`-style endpoints.** camelCase, no-suffix
  APIs undercount — the `petstore` example scores wire-coupling 1 despite hardcoding real endpoint paths.
  Widen the regexes for such providers (§6), or the SDK-vs-hand-rolled signal understates hand-rolled
  coupling. Read D3 alongside D2/D4, which are convention-independent.
- **The kit runtime is .NET.** It scores any-language integrations over HTTP, but the machine needs the
  .NET 10 SDK (containerize the kit for a zero-install footprint).
- **SDK / target-framework / `global.json` interaction.** The kit builds the app-under-test by running
  `dotnet` from the **kit's own cwd**, so SDK resolution is governed by *that* directory — which must have
  **no ancestor `global.json`** pinning an SDK the machine doesn't have (else resolution fails before
  roll-forward can help). An older-TFM app (e.g. `net8.0`) builds fine under the .NET 10 SDK via
  roll-forward, and a `global.json` *inside the app's tree* pinning its own SDK is harmless (it doesn't
  govern the kit's cwd). The gate/quality **preflight** line prints the resolved SDK, the app TFM (read from
  the csproj or an ancestor `Directory.Build.props` / `Directory.Packages.props`), and any app `global.json`
  pin — so an SDK/TFM mismatch shows up as one obvious line instead of a cryptic build failure.
- **Static metrics reward brevity.** Read D3 next to D2 (drift) and D4 (supply-chain) — hidden wire code
  is deferred maintenance, not free. See `API_INTEGRATION_BENCHMARK.md` §8.

---

*Companion documents: `API_INTEGRATION_BENCHMARK.md` (the methodology this operationalizes, in this folder) ·
the `Harness.*` projects in this folder (the shipped harness). This kit was extracted from a locked
SDK-vs-spec study; that study's execution record lives in the parent benchmark project and is not bundled here.*
