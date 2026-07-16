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
reference/         known-good integration used as the self-test fixture (+ BREAK=… defect toggles)
```

**Prerequisites:** .NET 10 SDK. (The harness runtime is .NET; it scores integrations in *any* language
over HTTP — only the D3/D4 static-analysis adapter is stack-specific.)

**Quickstart (self-test — proves the kit works out of the box):**
```bash
cd benchmark/turnkey
dotnet build Harness.slnx
# Part A on the bundled known-good integration → 37/37 public, 5/5 holdout
dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj --mode public
dotnet run --project Harness.Gate -- --profile profiles/maxio-eshop --app-project reference/Reference.csproj --mode holdout
```

Then follow **`PLAYBOOK.md`** to author a profile for your own integration and produce its scorecard.

The `maxio-eshop` profile reproduces the locked study (`../docs/EXECUTION_RECORD.md`) exactly — gate
37/37 + 5/5 on `reference/`, every `BREAK=` discrimination case, and the D1–D4 scorecard on the study's
`../runs/scope22-arm*` trees. Use it as the template for a new profile.
