# Task Specification — SDK-vs-Spec Token Benchmark

> **Status:** LOCKED · v0.4 · 2026-07-10
> **Companion to** `PRODUCTION_READINESS.md` (LOCKED v0.3). This document defines the *neutral
> functional task* both arms receive: the exact routes to build, the per-operation semantics, the
> environment, and the **identical prompt scaffold** whose only difference is the input material.
>
> **v0.4 (2026-07-10) — scope expansion.** Grew the task from **11 to 22 operations** (added
> components, price points, subscription-components, allocations, invoices, coupons) for the
> *larger-scope* investigation: testing whether the SDK's fixed "learn-the-SDK" cost amortizes over a
> bigger integration. §3 routes/contracts below are updated; the gate (+11 C1 checks), mock (+11
> routes), and known-good reference (+11 endpoints) were scaled in lockstep and re-self-tested
> **green** on the reference (37/37 public + 5/5 holdout). Applies identically to both arms — this
> raises the *task size* for both, not the *bar* for either.

---

## 1. What the agent builds

A billing integration in the eShopOnWeb PublicApi that exposes **one HTTP endpoint per capability**
(the 22 in §3) under the route prefix **`/api/billing`**, backed by a provider client that talks to
the Maxio Advanced Billing REST API. The agent is **done** when the production-readiness gate is
green (`PRODUCTION_READINESS.md` §4). The agent works **checklist + run-only** (§4.1 there): it is
given the §6 properties as requirements and can run the gate, but cannot read the gate's source.

### 1.1 Baseline & isolation (both arms, identical)

- **Start state:** a fresh clone of `dotnet-architecture/eShopOnWeb` at a **pinned commit**, with
  **zero billing code** — no `/api/billing`, no provider client, no Maxio config. Both arms start
  from this identical tree.
- **Isolation (critical for validity):** each run executes in a clean directory with **no access to
  this benchmark repo's `CLAUDE.md`, the `benchmark/` docs, or any prior Maxio integration code**.
  Those would contaminate both arms — Arm B most of all. Run with `claude --bare` (skips
  CLAUDE.md/hook/skill auto-discovery) and inject only the intended per-arm material (§2).
- **Full SDK value captured:** because the baseline is pristine (no pre-wired DI/config), the SDK's
  client-init/DI ergonomics (the `dotnet-client-initialization` skill) are part of what's measured —
  not pre-erased by a skeleton.

## 2. Fairness rules for materials (the crux of the A/B)

Everything below is **identical** for both arms: this task spec, the routes (§3), the environment
(§4), the prompt scaffold (§5), the constraints (§6), the gate, the mock, the model, and the effort
setting. **Exactly one input differs:**

| Arm | Material given | Explicitly NOT given |
|---|---|---|
| **A — SDK** | The **`maxio-sdk` Claude plugin** — skills that point to the SDK source (GitHub) + the NuGet package to install, plus skills that guide SDK-calling code (auth, client init, calling endpoints, models, error handling, resilience, testing) | The OpenAPI spec; plugin enabled **only in Arm A** |
| **B — Spec** | The **exact OpenAPI spec APIMatic used to generate the SDK** (a file in the working tree) | The SDK, the `maxio-sdk` plugin, and any SDK docs |

Rules that keep this defensible:
- **Same contract source.** Arm B's spec is the **exact OpenAPI spec APIMatic used to generate the
  `maxio-sdk`** (provided by the experimenter — a materials prerequisite, see §8). The mock (§8) is
  built faithful to that same spec. One contract, both arms — else one is built against a different
  contract → invalid.
- **Arm A's material is the `maxio-sdk` plugin** (APIMatic's real delivery mechanism — progressive
  skills that install the NuGet package, navigate the SDK source, and guide calling code). Its skill
  content loads into context on demand and **its token cost counts toward Arm A** — fair, since the
  SDK's delivery vehicle has a real cost too.
- **Accepted validity caveat (naming):** the provider is named **"Maxio"** concretely (chosen for
  realism). Arm B is therefore not purely "spec only" — the agent may draw on latent Maxio/Chargify
  knowledge from training. This is a disclosed threat to validity (recorded in `PROTOCOL.md`). It
  applies to *both* arms so it is not a directional bias, but it means the spec-only token count is a
  lower bound relative to a genuinely-unknown API.
- **No SDK cues in shared text.** No part of the shared prompt (§5) or this spec may name SDK types,
  methods, or namespaces. The routes and semantics are described in provider/HTTP terms only.

## 3. The public API surface (fixed routes — both arms build these exactly)

Routes are **pinned** so the gate can target them. All under `/api/billing`. **Request bodies are
pinned** (exact field names + types below) so the gate can POST valid bodies to the agent's
endpoints — the agent must accept exactly these request fields. **Response shapes are the agent's
choice**: the gate uses value-presence matching (`PRODUCTION_READINESS.md` C1), asserting the
provider's returned values appear regardless of field name.

| # | Capability | Method + app route | Underlying Maxio call | Success | Notes |
|---|---|---|---|---|---|
| 1 | List plans | `GET /plans` | `GET /product_families/{pfId}/products.json` | 2xx + each plan's **id, name, price** | `pfId` from config |
| 2 | Find-or-create customer | `POST /customers` | `GET /customers/lookup.json` → `POST /customers.json` | 2xx + the customer **id** | idempotent: existing ref returns the same id |
| 3 | List customer subscriptions | `GET /customers/{customerId}/subscriptions` | `GET /customers/{id}/subscriptions.json` | 2xx + each subscription's **id + state** | |
| 4 | Read subscription | `GET /subscriptions/{subscriptionId}` | `GET /subscriptions/{id}.json` | 2xx + the subscription **id, state, plan** | |
| 5 | Pause | `POST /subscriptions/{subscriptionId}/pause` | `POST /subscriptions/{id}/hold.json` | 2xx + resulting **state** | |
| 6 | Resume | `POST /subscriptions/{subscriptionId}/resume` | `POST /subscriptions/{id}/resume.json` | 2xx + resulting **state** | |
| 7 | Reactivate | `POST /subscriptions/{subscriptionId}/reactivate` | `PUT /subscriptions/{id}/reactivate.json` | 2xx + resulting **state** | |
| 8 | Create subscription | `POST /subscriptions` | `POST /subscriptions.json` | 2xx + the new subscription's **id + state** | **billing side effect** → R5 |
| 9 | Commit plan change | `POST /subscriptions/{subscriptionId}/plan-change` | `POST /subscriptions/{id}/migrations.json` | 2xx + the updated subscription/plan | immediate; **billing side effect** → R5 |
| 10 | Cancel | `DELETE /subscriptions/{subscriptionId}` | `DELETE /subscriptions/{id}.json` | 2xx + resulting **state** (canceled) | immediate |
| 11 | Record usage | `POST /subscriptions/{subscriptionId}/usage` | `POST /subscriptions/{id}/components/{compId}/usages.json` | 2xx + the recorded usage **id/quantity** | `compId` from config; **billing side effect** → R5 |
| 12 | List components | `GET /components` | `GET /product_families/{pfId}/components.json` | 2xx + each component's **id, name, kind** | `pfId` from config |
| 13 | Read component | `GET /components/{componentId}` | `GET /product_families/{pfId}/components/{componentId}.json` | 2xx + the component **id, name, kind** | |
| 14 | Create metered component | `POST /components` | `POST /product_families/{pfId}/metered_components.json` | 2xx + the new component **id** | **write** (catalog side effect) |
| 15 | List price points | `GET /components/{componentId}/price-points` | `GET /components/{componentId}/price_points.json` | 2xx + each price point's **id, name** | response is `{ "price_points": [...] }` |
| 16 | Create price point | `POST /components/{componentId}/price-points` | `POST /components/{componentId}/price_points.json` | 2xx + the new price point **id** | **write** (catalog side effect) |
| 17 | List subscription components | `GET /subscriptions/{subscriptionId}/components` | `GET /subscriptions/{id}/components.json` | 2xx + each component's **id + allocated quantity** | |
| 18 | List allocations | `GET /subscriptions/{subscriptionId}/components/{componentId}/allocations` | `GET /subscriptions/{id}/components/{compId}/allocations.json` | 2xx + each allocation's **id + quantity** | |
| 19 | Create allocation | `POST /subscriptions/{subscriptionId}/components/{componentId}/allocations` | `POST /subscriptions/{id}/components/{compId}/allocations.json` | 2xx + the allocation **id + quantity** | **billing side effect** → R5 applies |
| 20 | List subscription invoices | `GET /subscriptions/{subscriptionId}/invoices` | `GET /invoices.json?subscription_id={id}` | 2xx + each invoice's **uid/number, total, status** | list via query filter (no subpath GET) |
| 21 | List coupons | `GET /coupons` | `GET /product_families/{pfId}/coupons.json` | 2xx + each coupon's **id + code** | `pfId` from config |
| 22 | Create coupon | `POST /coupons` | `POST /product_families/{pfId}/coupons.json` | 2xx + the new coupon **id + code** | **write** (catalog side effect) |

**Single record-usage route for both arms** (`POST /subscriptions/{id}/usage`) — the component id is
resolved from config internally, not exposed in the app route.

**Pinned request contracts** (the agent's endpoints MUST accept exactly these fields; the gate sends
these shapes with mock-valid values). App-facing fields are camelCase; the agent maps them to Maxio's
wire shape (snake_case envelopes) internally — that mapping is part of the integration work:
- **`POST /customers`** — `{ "reference": string, "firstName": string, "lastName": string, "email": string }`
- **`POST /subscriptions`** — `{ "customerReference": string, "productHandle": string }`
- **`POST /subscriptions/{id}/plan-change`** — `{ "productHandle": string }`
- **`POST /subscriptions/{id}/usage`** — `{ "quantity": number, "memo": string? }`
- **`POST /components`** — `{ "name": string, "unitName": string, "pricingScheme": string }`
- **`POST /components/{componentId}/price-points`** — `{ "name": string, "pricingScheme": string, "unitPrice": string }`
- **`POST /subscriptions/{id}/components/{componentId}/allocations`** — `{ "quantity": number, "memo": string? }`
- **`POST /coupons`** — `{ "code": string, "name": string, "percentage": string }`
- Lifecycle actions (5, 6, 7, 10) take no request body beyond the path id.

## 4. Environment & configuration (identical for both arms)

The agent's app is configured to reach the provider (the mock) directly:

| Config key | Value | Purpose |
|---|---|---|
| `Maxio__BaseUrl` | `http://localhost:8080` | the mock directly (transport faults are injected by the mock itself; no proxy layer) |
| `Maxio__Subdomain` | `acme` | provider site |
| `Maxio__ApiKey` | `test-api-key` | Basic auth `Base64("{ApiKey}:x")` |
| `Maxio__ProductFamilyId` / `Maxio__ProductFamilyHandle` | (from mock fixtures) | fixed path param for op 1 |
| `Maxio__MeteredComponentId` / `Maxio__MeteredComponentHandle` | (from mock fixtures) | fixed path param for op 11 |
| `UseOnlyInMemoryDatabase` | `true` | no SQL Server needed |

- **DB / app boot** use eShopOnWeb's in-memory database so the app boots hermetically.
- **Required config (drives S3 fail-fast):** at minimum **`Maxio__ApiKey`** must be treated as
  required — booting with it removed must fail fast with a clear config error, not 500 at first
  request. (The gate's S3 removes exactly this key.)
- The provider base URL, credentials, and IDs come from **environment / user-secrets**, never
  committed to `appsettings.json` (S1: the key value must never appear in logs).
- **Per-arm material placement (the treatment, enforced by the run harness):** Arm A runs with the
  `maxio-sdk` plugin enabled and **no** OpenAPI spec file present; Arm B runs with the spec file in
  the tree and the `maxio-sdk` plugin **disabled**.

## 5. The shared prompt scaffold

Both arms receive this verbatim; only the single **{{ARM_MATERIAL}}** block differs.

```
You are adding a billing integration to this eShopOnWeb application.

GOAL
Expose the following HTTP endpoints under /api/billing, each backed by a client that calls the
Maxio Advanced Billing REST API. Surface Maxio's data and behavior; you choose your own field
names and internal structure.

ENDPOINTS
<the §3 table: method, route, and the semantic success description for each of the 22 ops>

PRODUCTION-READINESS REQUIREMENTS
Your integration must satisfy every property below (these are the acceptance criteria):
<the PRODUCTION_READINESS.md §6 checklist, prose form — the property statements only, not any tests>

ENVIRONMENT
The provider base URL and credentials are supplied via configuration (§4). The app uses an
in-memory database and must boot with the supplied configuration.

DEFINITION OF DONE
The integration is complete when the production-readiness gate passes. Run it with:
    <single gate command>
It reports pass/fail per check with a failure message. Iterate until every check is green. You may
run it as often as you like. You cannot read the gate's source, the mock's source, or its data.

MAXIO API REFERENCE
{{ARM_MATERIAL}}

CONSTRAINTS
- Put your integration code in the eShopOnWeb PublicApi/Infrastructure/ApplicationCore layers.
- Do NOT modify anything under gate/ or mock/, and do NOT change the pinned routes above.
- Do NOT hardcode responses to satisfy the gate; the endpoints must genuinely call Maxio.
```

`{{ARM_MATERIAL}}`:
- **Arm A:** "The Maxio Advanced Billing SDK is available via the `maxio-sdk` plugin. Use its skills
  to install the NuGet package, navigate the SDK source, and write the SDK-calling code."
- **Arm B:** "The Maxio Advanced Billing OpenAPI specification is at `<path>`."

## 6. Constraints & rules for the agent (encoded in the prompt)

- Integration code lives in eShopOnWeb's normal layers; the app must boot with the supplied config.
- The pinned routes (§3) must not change — the gate targets them.
- `gate/` and `mock/` are off-limits (not in the agent's working tree beyond the runnable gate
  command; their source is not readable).
- **No hardcoding / no gate-gaming**: endpoints must genuinely call Maxio. Enforced two ways:
  (1) the request-recording mock asserts the app actually made the expected upstream Maxio call
  (right method + path) for each op — a happy-path hardcode that never calls Maxio fails the gate;
  (2) the hidden holdout (`PRODUCTION_READINESS.md` §7) — gaming the visible checks reaches DONE but
  fails ROBUST.

## 7. Decisions (resolved 2026-07-09)

1. **Arm A material — RESOLVED:** the **`maxio-sdk` Claude plugin** (skills → SDK GitHub source +
   NuGet package + SDK-calling-code guidance). APIMatic's real delivery mechanism, so the experiment
   measures the real product. Plugin token cost counts toward Arm A (§2).
2. **Route names — SETTLED:** `/api/billing/...` per §3, pinned identically for both arms.
3. **Provider naming — RESOLVED:** concrete **"Maxio"** in the shared prompt (realism), with the
   spec-only contamination caveat disclosed in §2 and `PROTOCOL.md`.

## 8. Companion documents

- `PRODUCTION_READINESS.md` — LOCKED v0.2 — the definition of done.
- `PROTOCOL.md` — held-constant list, N runs, budget cap, token capture rig, statistics,
  DONE/ROBUST tiering, credibility safeguards.
- `gate/`, `mock/` — the executable gate (public + holdout) and the spec-faithful, fault-injecting,
  request-recording mock (the mock injects transport faults itself; no proxy layer).
- **Materials prerequisite (blocks the build):** the exact OpenAPI spec APIMatic used to generate the
  `maxio-sdk` — present at repo `openAPI/` (`openapi.yaml` + `components/`). It is Arm B's material
  AND the contract the mock is built to. `APIMATIC-META.json` (codegen config) is NOT part of Arm B's
  injected copy.
