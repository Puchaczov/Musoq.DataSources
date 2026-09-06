# Search campaign initial gate freeze

Status: approved for the core track

This ADR freezes the proof boundaries for the Search campaign. It records proposed gates and measurement obligations; it does not claim that performance, host compatibility or release readiness has already been achieved.

## Release tiers

| Tier | Boundary | Required proof |
| --- | --- | --- |
| T0 | Deterministic source/unit behavior | Focused tests, negative tests, exact result semantics and resource assertions. |
| T1 | Datasource repository integration | `dotnet test --configuration Release`, nonzero test discovery, zero failures, classified existing skips and retained result evidence. |
| T2 | Compiled Musoq/runtime-v2 query behavior | A version-matched compiled query fixture, schema/XML/runtime agreement and lifecycle trace. This is not substituted by source-only tests. |
| T3 | External CLI, service or source-engine compatibility | Explicitly configured executable/checkout, exact version/commit/package tuple, read-only inspection or verified handoff; absent prerequisites are blocked. |
| T4 | Release readiness | All applicable correctness, performance, latency, package and agent-facing gates pass on the locked tuple. No publication is implied by this local tier. |

## Measurement cohorts

Measurements must keep these boundaries separate:

- scanner/source-only: direct scan work and raw file/byte counters;
- datasource: row production and datasource lifecycle overhead;
- compiled query: parser/planner/executor plus datasource behavior;
- warm service: an already-started host and explicitly labeled warm cache state;
- cold client: process startup, connection/setup and first client-visible evidence.

The mandatory performance comparison is the declared Search-versus-ripgrep profile only after result equality, scope equality, output-unit equality and failure/completeness checks pass. The campaign's proposed geometric-mean ratio (`<= 1.25`) and per-cell ceiling (`<= 2.0`) remain frozen targets, not measurements. Warm-service and cold-client latency targets are likewise separate (`<= 100 ms` and `<= 500 ms` p95 overhead respectively).

## Repository and external-write boundary

- Tracked implementation, tests, docs and evidence are owned by `Puchaczov/Musoq.DataSources`.
- Configured engine and CLI roots may be inspected or tested only when explicitly supplied and compatibility-locked; they are never a license to edit another checkout.
- Machine-specific absolute paths belong only in ignored local configuration. Shared examples contain null/unresolved values and must not infer executables, credentials, schemas or services.
- No push, tag, package publication, release, user-profile installation or automatic external mutation is part of a core scope.

## Proof obligations

Every completed scope retains:

1. exact argv and cwd, tool/runtime/package versions and process exits;
2. focused and full-suite test counts with actual result/TRX evidence where applicable;
3. expected skips classified without relabeling failures;
4. source and artifact SHA-256 hashes, an approved separate review and known limitations;
5. one reachable completion commit carrying both required Search trailers.

Configured command vectors are exact data, not shell fragments. The frozen repository full-suite vector is `dotnet test --configuration Release`; redirection, pipelines and shell interpolation are wrapper concerns and are never stored as product command arguments.

## Secret policy

Shared configuration and committed evidence must contain no passwords, tokens, API keys, bearer values, private keys or credential-bearing properties. Secret-bearing test fixtures are synthetic names only. Missing credentials produce a blocked/skip result that is disclosed, never a fabricated success.

## Baseline reference

W00-S03 established the current datasource baseline at 20 result projects, 1,284 discovered, 1,253 passed, 0 failed and 31 existing classified skips. Later scopes may supersede these numbers only with a fresh exact command and evidence; a plan-validator success alone is not a product test result.
