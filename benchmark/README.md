# SDK-vs-Spec Token Benchmark

An experiment measuring whether an agent using APIMatic's Maxio SDK (via the `maxio-sdk` plugin)
reaches a **production-ready** eShopOnWeb billing integration with **fewer tokens** than an agent
given only the OpenAPI spec. Both arms are forced to the same external production-ready gate; the
measured quantity is tokens-to-DONE.

## Layout

| Path | What |
|---|---|
| `docs/PRODUCTION_READINESS.md` | LOCKED — the definition of done (the gate's checklist) |
| `docs/TASK_SPEC.md` | LOCKED — the neutral task: routes, request contracts, prompt scaffold |
| `docs/PROTOCOL.md` | run mechanics, token capture, statistics, credibility safeguards |
| `mock/` | spec-faithful, fault-injecting, request-recording Maxio mock *(to build)* |
| `gate/` | executable gate: public checks + hidden holdout *(to build)* |
| `harness/` | headless run harness, prompt composer, token capture *(to build)* |
| `../openAPI/` | the Maxio OpenAPI spec — the shared contract + Arm B's material |
| `../eShopOnWeb/` | pristine vanilla baseline both arms start from |

## Status

Design **locked**. Building Stage 1 (harness + pilot) per the approved plan. Arm A material: the
`maxio-sdk` plugin at `C:\repos\v4-plugins\plugins\maxio-sdk`.
