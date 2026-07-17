# API Integration Benchmark — Turnkey Package

Everything needed to run the API-integration benchmark on **any REST/JSON integration**, in one
self-contained folder. Provider-specific details live entirely in a **profile** (three JSON files); the
four `Harness.*` projects are generic and never edited per integration.

```
API_INTEGRATION_BENCHMARK.md   the methodology — WHAT is measured and WHY (read to understand)
PLAYBOOK.md                    the procedure — HOW to run it on a codebase (read to operate)
Harness.slnx                   solution tying the four projects together

Harness.Core/      engine: fault + drift (P1–P8) + recorder, HTTP clients + oracle primitives,
                   profile/contract/optable model types, process/boot helpers      (provider-agnostic)
Harness.Mock/      generic host: serves a declarative contract.json through the record/fault/drift
                   middlewares + the /__mock control plane
Harness.Gate/      Part A readiness gate — R/E/S/C templates resolved by op-role + data-driven C1 + holdout
Harness.Quality/   Part B scorecard — D1/D2 drift replay + D3/D4 static analysis (.NET adapter)
Harness.Profiler/  OpenAPI spec → profile draft generator (fills the provider side; app-side is TODO)

profiles/
  maxio-eshop/     worked, validated example profile (profile.json · contract.json · optable.json)
  petstore/        second example — a DIFFERENT provider (Swagger Petstore, OpenAPI 3.0)
reference/         known-good Maxio integration used as the self-test fixture (+ BREAK=… defect toggles)
reference-petstore/  known-good Petstore integration for the second example (+ the same BREAK=… toggles)
```

**Prerequisites:** .NET 10 SDK. (The harness runtime is .NET; it scores integrations in *any* language
over HTTP — only the D3/D4 static-analysis adapter is stack-specific.)

**Quickstart (self-test — proves the kit works out of the box):**
```bash
# run every command from the kit root — the folder with Harness.slnx (benchmark/turnkey/ in the monorepo)
dotnet build Harness.slnx
# Part A on the bundled known-good integration → 37/37 public, 5/5 holdout
dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj --mode public
dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj --mode holdout
```

Then follow **`PLAYBOOK.md`** to author a profile for your own integration and produce its scorecard.

The `maxio-eshop` profile is validated two ways. The **self-contained** proof ships in this repo: gate
37/37 public + 5/5 holdout on `reference/`, plus every `BREAK=` discrimination case. Separately, in the
origin study this kit was extracted from, the same profile produced the full D1–D4 scorecard on that
study's SDK-vs-spec arm trees — those produced trees are *not* bundled here (see PLAYBOOK §8). Use
`maxio-eshop` as the template for a new profile.

**Second example — a different provider (proves cross-API generality).** `profiles/petstore/` +
`reference-petstore/` run the whole benchmark on the Swagger Petstore API (OpenAPI **3.0**, bare/array
bodies, camelCase, `api_key`-header auth — none of which Maxio exercises). It was built by generating the
provider side from the spec with `Harness.Profiler`, then completing the integration-side fields. Result:
gate **22/22 public + 5/5 holdout**, all `BREAK=` cases red their target check, and a full D1–D4 scorecard
(D1 100% · D2 resilience 53% · D3 maxCC 23/LOC 230 · D4 0 findings/0 deps).

```bash
# Part A on the second example (run from the kit root)
dotnet run --project Harness.Gate -- --profile profiles/petstore --mode public
dotnet run --project Harness.Gate -- --profile profiles/petstore --mode holdout
# Part B scorecard
dotnet run --project Harness.Quality -- --profile profiles/petstore --tree reference-petstore --mode all
```
