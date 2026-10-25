# Core alpha.2 release preparation

Local preparation for Core **17.0.14-alpha.2**, starting from
`1e5eb7220114acbf00d680acf63ba8f4ec94810f`, on 2026-10-25.
Only Musoq.DataSources is changed. Core and both helper package versions remain
unchanged. One local release-preparation commit follows successful qualification;
no push, tag, installation or publication is authorized.

## Wave 1 — Runtime integration

The five Core dependencies are upgraded together. Published package-byte provenance,
resolved versions, focused regressions, license snapshots, Release build and full
solution results are recorded under `artifacts/alpha14.2-release-20261025/`.
Wave 1 passed: **123 focused tests**, Release build, license graph validation, and
**2011 passed / 0 failed / 34 existing or deferred skips** across 22 projects.
Five restored Core archives match published NuGet bytes; exact SHA-256 values and
resolved package sources are retained in `core-provenance.json`.

Earlier alpha.1 defects and qualification totals in PatternPushdownValidation.md
are historical evidence. Current tests require corrected LIKE, regex timeout and
JSON self-correlation behavior, including disabled/rejected provider pushdown.

## Upstream reproducers

| Reported alpha.1 limitation | alpha.2 integration result |
|---|---|
| Residual `NOT LIKE '%'` compiles incorrectly | Resolved: missing, null, empty and non-empty JSON values agree across enabled, disabled and provider-rejected execution. |
| Custom/default regex budgets do not reach providers | Resolved: default 2 seconds and custom 17/73 ms budgets reach negotiated positive and negated predicates; compiled queries retain independent budgets during repeated and overlapping runs. |
| Public provider regex timeout maps to MQ7011 | Resolved: a controlled 1 ms provider timeout produces `MQ7017_RegexMatchTimeout`, retaining `RegexMatchTimeoutException` and its actual budget. An ordinary provider IOException still produces MQ7011. |
| JSON self-correlation binding/carrier failures | Both reproducers resolved: equality and general correlation return independently specified rows across all three pushdown modes, repeated runs and overlapping compiled queries. |

The integration tests remain in `Alpha14IntegrationTests.cs`. Production providers
retain their existing capabilities and residual boundaries, and do not reference
Evaluator. The shared matcher change is documentation only.

## Wave 2 — Datasource package versions

Only the 16 registered datasource package versions change. Identities, manifest
order, publication flags, and helper versions are unchanged. AsyncRowsSource stays
at `3.0.3-alpha.1`; Roslyn.CommandLineArguments stays at `1.0.3-alpha.1`.

| Package | Before | Prepared version |
|---|---|---|
| Musoq.DataSources.Archives | 6.0.6-alpha.1 | 6.0.7-alpha.1 |
| Musoq.DataSources.CANBus | 4.0.6-alpha.1 | 4.0.7-alpha.1 |
| Musoq.DataSources.Docker | 5.0.6-alpha.1 | 5.0.7-alpha.1 |
| Musoq.DataSources.FlatFile | 8.0.6-alpha.1 | 8.0.7-alpha.1 |
| Musoq.DataSources.Git | 3.0.3-alpha.1 | 3.0.4-alpha.1 |
| Musoq.DataSources.GitHub | 2.0.6-alpha.1 | 2.0.7-alpha.1 |
| Musoq.DataSources.Jira | 2.0.6-alpha.1 | 2.0.7-alpha.1 |
| Musoq.DataSources.Json | 10.0.6-alpha.1 | 10.0.7-alpha.1 |
| Musoq.DataSources.Ollama | 5.0.6-alpha.1 | 5.0.7-alpha.1 |
| Musoq.DataSources.OpenAI | 6.0.6-alpha.1 | 6.0.7-alpha.1 |
| Musoq.DataSources.Os | 9.0.6-alpha.1 | 9.0.7-alpha.1 |
| Musoq.DataSources.Roslyn | 3.0.7-alpha.1 | 3.0.8-alpha.1 |
| Musoq.DataSources.Search | 1.0.3-alpha.1 | 1.0.4-alpha.1 |
| Musoq.DataSources.SeparatedValues | 11.0.3-alpha.1 | 11.0.4-alpha.1 |
| Musoq.DataSources.System | 8.0.6-alpha.1 | 8.0.7-alpha.1 |
| Musoq.DataSources.Time | 8.0.6-alpha.1 | 8.0.7-alpha.1 |

Wave 2 passed: **123 focused tests / 0 failed / 0 skipped**, Release build with
**0 warnings / 0 errors**, license graph validation, all four script checks, all 16
release definitions and per-package smoke tests, and the full solution suite:
**2012 passed / 0 failed / 33 inherited skips** across 22 projects. The representative
suite passed all **189 tests**, including the artifact-dependent package smoke test.

Fresh per-package archives are retained in
`artifacts/alpha14.2-release-20261025/release-packages/<slug>/`, with all 64 platform
ZIPs collected in `packages/` for the final suite. All **64 platform ZIPs, 16 NuGet
packages and 16 symbol packages** passed package and license qualification. These local working-tree artifacts
are qualification evidence, not publication assets. Publication requires fresh
exact-commit CI and release verification.

## Qualification commands and evidence

Commands run from the repository root; no solution command uses `-m:1`.

- Restore: `dotnet restore Musoq.DataSources.sln --force-evaluate`.
- Refresh: `pwsh scripts/release/Update-LicenseSnapshots.ps1 -PluginName All -NoRestore`.
- Dependency graphs: `pwsh scripts/release/Assert-LicenseSnapshots.ps1 -PluginName All -ValidatePackageGraph`.
- Focused tests: `dotnet test Musoq.DataSources.RepresentativeTests -c Release --no-restore --filter 'FullyQualifiedName~Alpha14|FullyQualifiedName~Pattern'`.
- Build: `dotnet build Musoq.DataSources.sln -c Release --no-restore`.
- Full suite: `dotnet test Musoq.DataSources.sln -c Release --no-build --no-restore`, with TRX results retained for each wave.
- Scripts: `scripts/Test-PluginReleaseScripts.ps1`, `scripts/release/Test-LicenseSnapshotContracts.ps1`, `scripts/release/Test-SearchReleaseRegistry.ps1`, and `scripts/search/Test-SearchEnvironmentResolution.ps1`.
- Each manifest entry: `Validate-Release.ps1 -Tag <version>-<packageId> -Json`, `Pack-Release.ps1 -Tag <tag> -OutputPath <fresh-directory>`, then `Test-ReleaseSmoke.ps1 -Tag <tag> -ArtifactDirectory <directory>`.
- All archive licenses: `Assert-ReleaseLicenseArtifacts.ps1 -PluginName All -AssetsDirectory <packages> -ExpectedArchiveCount 4`.

The final suite receives `MUSOQ_PLUGIN_ARTIFACTS_DIR` pointing at this run's flat
`packages/` directory. Per-wave JSON totals, per-project TRX files, script logs,
per-package pack/smoke logs and `packaging-result.json` are retained under the
evidence root. Published Core archives are retained in `published-core/`; restored
package hashes and resolved versions are checked against those exact bytes despite
the configured candidate feed and existing caches.

## Existing skip inventory

The inherited skips are retained; none of the new regression cases is ignored or
inconclusive. The per-project TRX files retain the individual test names.

| Existing category | Count | Reason |
|---|---:|---|
| Docker playgrounds | 9 | Existing `[Ignore]` fixtures requiring a live Docker environment. |
| Git playgrounds | 11 | Existing `[Ignore]` fixtures tied to a local repository playground. |
| Roslyn playgrounds | 5 | Existing `[Ignore]` exploratory solution/NuGet fixtures. |
| Ollama playgrounds | 2 | Existing `[Ignore]` fixtures for a live model service. |
| OpenAI playground | 1 | Existing `[Ignore]` fixture for a live API. |
| System range/RowNumber case | 1 | Existing `[Ignore]` legacy test; no reason supplied in its attribute. |
| Git file-history skip/take | 1 | Existing inconclusive guard: insufficient file changes in this checkout. |
| Search symbolic-link/reparse fixtures | 3 | Existing inconclusive guard: this Windows process lacks symbolic-link fixture privileges. |

Wave 1 additionally deferred one existing package-contract smoke test because no
plugin archives were available in its artifact search directory. Wave 2 supplies
all newly built archives explicitly so that this test must execute successfully.

## Final review and local handoff

The complete diff was reviewed and `git diff --check` passed. Only the selected
16 project versions increment; all other explicitly versioned projects retain
their starting values. Package identities, manifest order and publication flags
are unchanged. All 16 license reports retain their dependency closure and license
identities, with only the expected Musoq package versions changed.

Core remains clean at `a6c7663deabd4684d70b8a962f33c365b01ca531`. DataSources
qualification starts from `1e5eb7220114acbf00d680acf63ba8f4ec94810f` and is followed
by exactly one local commit, `chore: prepare datasource release for Core 17.0.14-alpha.2`.
The committed tree and clean working-tree verification, along with the resulting
commit SHA, are retained in the local evidence root's `handoff.json`. No remote Git
changes, tags, installation or publication are part of this preparation.

The release-script check initially exposed one stale escaped JSON version assertion;
it was corrected to match the updated fixture version and all checks were rerun
successfully. No new skip was introduced. Wave 1's only artifact-dependent deferral
became a passing test in Wave 2. All four reported Core limitations are resolved by
executable DataSources integration coverage; no Core source changes were needed.

The archives were built from the qualified working tree before the local commit.
Fresh CI and release verification for the exact release commit remain required
before any later publication.
