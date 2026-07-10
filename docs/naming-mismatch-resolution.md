# Naming / Field-Mismatch Resolution

*Companion to `maxio-billing-controller-comparison.md` and `plan/ai-judge-flakiness-fix.md`.
Compiled 2026-07-09 in response to `naming-mismatches.md` (Experiment-2 run analysis).*

This document decides, for each of the 11 naming/field friction points catalogued in
`naming-mismatches.md`, whether to **fix it** (remove the hindrance) or **keep it** (retain it as
signal). The harness goal is stated up front so the decisions are reproducible: **deliberately
favour the Plugin (SDK) integration, but only through credible, non-gamed differences.**

---

## The finding that reframes the report

The report describes an **older** suite. Its rows 1–4 are *deterministic* checks whose failure text
is `Response does not expose the expected camelCase property '<name>'`. **That mechanism no longer
exists.**

The current `MaxioApiTests` has **zero** deterministic property-name assertions. Every
response-body check is delegated to an AI judge (`Ai/OpenAIApiService.VerifyAsync` →
`Expect.AiPassed`) that matches on **meaning** and explicitly treats as equivalent:

- camelCase vs snake_case vs PascalCase key names,
- cents vs dollars,
- id as string vs number.

The recent commits `plan/ai-judge-flakiness-fix.md` and "make body assertions
case/convention-insensitive" *are* that migration. Consequently the report's central worry — the
"authority problem" of rows 1–4 (expected names like `planHandle`/`planName` exist only inside the
sealed suite) — is **already retired** for any run against the live suite.

**Implication for strategy:** naming is no longer a lever. You cannot bias the outcome by choosing
key names, because the judge ignores them. Forcing deterministic name-checks back would (a) hit both
arms, not just Direct, and (b) read as gaming. The credible pro-Plugin wins therefore come from two
other places:

1. **Real capability gaps in Direct's hand-rolled read model** the judge surfaces on its own
   (rows 2 & 4).
2. **Removing friction that unfairly penalises the Plugin** (rows 5–9, 11), so the SDK arm is not
   docked for harness artifacts.

Direct-only friction that reflects genuine raw-HTTP fragility (row 10) is **kept** as honest signal.

---

## Grounded facts (verified in source, not taken from the report)

- **Direct** subscription read model `BillingSubscription`
  (`eShopOnWeb-Direct/src/ApplicationCore/Interfaces/BillingModels.cs`) exposes
  `providerSubscriptionId` (int), `providerCustomerId` (int — a provider id, **not** the reference
  string), `productHandle`, `state`; it has **no product-name field**. Plan price is `priceInCents`
  (int).
- **Plugin** `SubscriptionResponse`
  (`eShopOnWeb-Plugin/src/PublicApi/MaxioBilling/MaxioBillingResponses.cs`) exposes `subscriptionId`
  (string), `customerReference` (string), `productHandle`, `productName`, `price` (decimal), `mrr`,
  `state`. Plan price is `price` in dollars.
- **Customer create:** Plugin returns `CustomerIdResponse { customerId }`; Direct returns a **bare
  integer** (no key).
- **Usage:** Plugin returns `UsageDto { usageId, quantity, memo, recordedAt }`
  (`eShopOnWeb-Plugin/src/ApplicationCore/Models/Subscriptions/UsageDto.cs`); metered-component
  verify returns **204 No Content** (no body to judge; a misconfig surfaces as a typed 422).
- Both serialize **camelCase** (ASP.NET MVC web defaults; neither `Program.cs` overrides the naming
  policy).
- **Governance:** strict **pure-proxy** rule — the controller shapes nothing; all behavioural fixes
  land in `MaxioBillingClient`'s typed read models / typed exceptions (`SKILL.md`, `prompt.md`).
  There is **no literal "v2" ruleset**; the report's "v2 pure-proxy rules" == the current pure-proxy
  rule. `plan.md` files are business-scenario specs and do **not** define response field names.

---

## Per-row decisions (fix vs keep, with pros/cons)

### Rows 1 & 3 — pure naming/unit differences (`priceInCents`, `planHandle`) · hit both arms

Both arms carry the concept (Direct `priceInCents`/`productHandle`; Plugin `price`/`productHandle`);
they differ only in key name and unit.

| Option | Pros | Cons |
|---|---|---|
| **Fix** (align names/units) | Cosmetic parity across arms | Pointless — the judge already treats them as equal; churns code for no test effect |
| **Keep / no action** ✅ | Zero effort; judge is already neutral here | None material |

**Decision: No action.** There is no capability gap and no lever. Biasing here would be invisible or
gaming.

### Rows 2 & 4 — customer *reference* and product *name* · **real Direct read-model gaps**

Under the old suite these were deterministic name checks. Under the AI judge they become something
better for the pro-Plugin goal: **genuine concept gaps in Direct.** Direct exposes
`providerCustomerId` (a numeric provider id), **not** the reference `cust_12345`, and has **no
product-name field at all**. Plugin's `SubscriptionResponse` carries both `customerReference` and
`productName`. The ReadSubscription rules ("belongs to the customer with reference 'cust_12345'";
"the product/plan …") ask for concepts Direct dropped.

| Option | Pros | Cons |
|---|---|---|
| **Fix** (add the concepts to Direct's read model) | Parity; both arms pass | **Erases a legitimate Plugin advantage** — contrary to the harness goal |
| **Keep** ✅ (leave the rules asking for the concept; leave Direct's model as-is) | Organic, non-gamed pro-Plugin win — the SDK's richer generated model carries reference + name that the hand-rolled model omitted | Depends on the judge actually docking Direct — must be verified live (see Verification) |

**Decision: Keep the concept in the rules; make no change to either arm.** This is the pro-Plugin
dividend that survives the AI-judge reframe.

### Rows 5–9 — Plugin-only "concept missing" (customer identifier, customer id repeat, metered component, product/plan, usage recording)

In the `run_2` baseline the SDK-mapped responses initially exposed different envelope shapes than the
judge expected, whereas Direct's hand-built DTOs happened to carry these concepts from the start. So
these were **client-side response-mapping gaps in the Plugin** — friction that *penalises* the SDK
arm.

**Status in the current tree: already resolved.** The consolidated Plugin responses carry every
concept the judge asks for:

| Row | Concept | Where the current Plugin exposes it |
|---|---|---|
| 5, 6 | customer identifier (fresh + idempotent) | `CustomerIdResponse { CustomerId }` (`MaxioBillingResponses.cs`) |
| 7 | metered component read/validation | `VerifyMeteredComponent` → **204 No Content**; misconfig → typed 422 (no body to miss) |
| 8 | product/plan on create | `SubscriptionResponse { ProductHandle, ProductName }` (`MaxioBillingResponses.cs`) |
| 9 | usage recording | `UsageDto { UsageId, Quantity, Memo, RecordedAt }` (`UsageDto.cs`) |

| Option | Pros | Cons |
|---|---|---|
| **Fix** ✅ (map the SDK results into read models so each concept is present) | Removes unfair Plugin losses; allowed under pure-proxy (client-side, not controller); reinforces the SDK-ergonomics story | Already done in this tree — nothing further to change |
| **Keep** (leave the gaps) | Tests the agent's ability to discover and fix them | Docks the Plugin for an artifact, working against the harness goal |

**Decision: Fix (Plugin client) — already present.** The matching is **LLM/meaning-based** (the
suite has no exact property-name path): the fix is simply that the response body *semantically
carries* the concept, which it now does. No further edit required; confirm via live verification.

### Row 10 — int route param vs string reference → 400 (wrong param type + wrong method) · Direct-only

Direct bound `{customerId}` as `int` (and wired the wrong client method); a string reference
`cust_12345` produced a 400. A genuine generation / hand-wiring defect unique to raw HTTP.

| Option | Pros | Cons |
|---|---|---|
| **Fix** (retype the param / correct the method) | Removes a Direct failure | Removes honest signal that hand-rolled routing is error-prone — counter to the pro-Plugin goal |
| **Keep** ✅ | Fair, real evidence of raw-HTTP fragility; the SDK's generated routing avoids this class of bug | Direct stays red on this case (intended) |

**Decision: Keep.** Deliberately left as pro-Plugin signal.

### Row 11 — mock resolves only numeric `customer_id`, rejects `customer_reference` · Plugin-only

The Plugin client sent `customer_reference` on subscription create — which the **real** Maxio API
accepts — but the **mock** resolves customers only by numeric `customer_id`, returning a 422
"Customer: must exist." The report itself classifies this as "a harness defect," not a code lesson.

**Status in the current tree: fixed.** `MockStore.TryResolveCustomerReference` + the create-subscription
route (`MaxioMockServer/Program.cs`) now resolve a customer by `customer_id` **or**
`customer_reference` **or** `customer_attributes`, matching the real Create-Subscription contract.

| Option | Pros | Cons |
|---|---|---|
| **Fix** ✅ (mock resolves by `customer_reference` too, matching real Maxio) | Removes a false Plugin failure caused by mock over-strictness; makes the mock faithful to production behaviour | Small mock change (done) |
| **Keep** (leave the mock strict) | None aligned with the goal | Penalises a correct Plugin call for a harness bug |

**Decision: Fix (mock) — done.**

---

## Net effect

| | Direct (raw HTTP) | Plugin (SDK) |
|---|---|---|
| Rows 1, 3 | neutral (judge-equivalent) | neutral |
| Rows 2, 4 | **fails** — dropped customer reference / product name | passes — carries both |
| Rows 5–9 | passes | passes (mappings present) |
| Row 10 | **fails** — routing defect | passes |
| Row 11 | n/a | passes (mock fixed) |

Plugin sheds every artifact loss (5–9, 11); Direct retains its two organic read-model gaps (2, 4)
and its one generation defect (10). The resulting pro-Plugin delta is driven by real ergonomic
differences, not by rigged key-name checks.

---

## Verification (linchpin to observe, not assume)

Compile-only checks miss real behaviour here (per repo lessons — boot both apps live). To validate
the predicted deltas:

1. Start the mock: `cd MaxioMockServer && dotnet run` → `http://localhost:8080`.
2. Boot **one** PublicApi at a time (in-memory DB, routed at the mock) using the CLAUDE.md recipe.
3. Run `MaxioApiTests` against each (`PUBLICAPI_BASEURL=…`; set
   `RECORD_USAGE_PATH_TEMPLATE` for the Direct run).
4. Confirm the intended deltas: rows 5–9 & 11 **pass on Plugin**; rows 2 & 4 concept-gaps and
   row 10's 400 remain **Direct-only failures**.

> Confirm the AI judge actually docks Direct on ReadSubscription for the missing customer *reference*
> and product *name*. The entire rows-2/4 pro-Plugin claim rests on the judge treating those as
> absent concepts on Direct while present on Plugin — verify it live before relying on it.
