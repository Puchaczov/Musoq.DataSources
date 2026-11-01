# Core 17.0.15-alpha.1 datasource release preparation

Locally qualified on 2026-11-01 (Europe/Warsaw). Changes are uncommitted. No tags,
pushes, publication, remote CI, or sibling-repository changes were performed.

Starting and final HEAD: `6a200ad0767192d72c414d756b414b3fe0f7c34a`.
Tracked source diff SHA-256: `B49428485B14CC6B74E318BA6B88E7EDD73FDDE318976717086203CBCFE07087`. The tracked diff remained unchanged through
the final solution and package gates. The new report is separate from that diff.

## Changes

The five shared Core pins now resolve to 17.0.15-alpha.1. Each of the 16 registered
datasources received its patch increment and alpha.1 suffix. Helper versions remain
AsyncRowsSource 3.0.3-alpha.1 and Roslyn.CommandLineArguments 1.0.3-alpha.1.
Active runtime/version assertions and compatibility guidance were updated; current
license snapshots were regenerated through Update-LicenseSnapshots.ps1. Historical
reports were preserved. No production datasource API changes were introduced.

NuGet.Config is an ignored local file. Its sibling candidate feed was removed,
the five Core IDs were mapped explicitly to nuget.org, and qualification used the
repository-local `artifacts/release-core17.0.15-alpha.1-cache`. All five restored
Core package origins and SHA-256 hashes are retained in the evidence bundle.

## Validation

- All 16 proposed release identities validated without creating tags.
- Release-script suite, license snapshot contracts, and dependency-graph checks passed.
- Final serialized solution restore and Release build passed. The initial build
  reported 430 warnings and zero errors; the final incremental build reported zero
  warnings/errors. Warning details are retained; no warning suppression was added.
- Complete final solution run: **2052 passed, 0 failed, 34 skipped**, across 22 test projects.
- All 16 packages passed packing, release smoke checks, and executed artifact-contract
  tests: 64 platform ZIPs, 16 NuGet packages, 16 symbol packages, license artifacts,
  immutable release metadata, Core compatibility, and host-assembly exclusion.
- `git diff --check` passed. Cross-platform ZIP checks ran on Windows; they do not
  establish native Linux, macOS, or Alpine execution.

The first full run had three failures: two Roslyn reference-count queries used stale
copied sample-solution assets for Core 17.0.11-alpha.2 in an old temporary cache, and
the constructor inventory omitted git.worktrees. Restoring both copied sample
solutions resolved the Roslyn failures (2/2 focused retests passed). Adding worktrees
to the concrete inventory raised its expected count from 74 to 75 (4/4 conformance
checks passed). The subsequent complete solution run passed. Original failures,
focused retests, and final TRX files are retained separately.

## Package results

| Package | Version | Pack | Release smoke | Artifact contract |
|---|---|---|---|---|
| Musoq.DataSources.Archives | `6.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.CANBus | `4.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Docker | `5.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.FlatFile | `8.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Git | `4.0.1-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.GitHub | `2.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Jira | `2.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Json | `10.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Ollama | `5.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.OpenAI | `6.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Os | `9.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Roslyn | `3.0.9-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Search | `1.0.5-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.SeparatedValues | `11.0.5-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.System | `8.0.8-alpha.1` | Passed | Passed | Passed |
| Musoq.DataSources.Time | `8.0.8-alpha.1` | Passed | Passed | Passed |

## Skipped and inconclusive tests

Existing ignored and environment-dependent tests were preserved. The solution-wide
artifact test is conditional on top-level artifacts; it was subsequently executed
successfully once per newly packed datasource using MUSOQ_PLUGIN_ARTIFACTS_DIR.

| Project | Test | Recorded reason |
|---|---|---|
| Musoq.DataSources.System.Tests | RowNumberEvenForRangeMinSignedMaxTest | Existing ignored test. |
| Musoq.DataSources.OpenAI.Tests | DoSomeRealTests | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | NetworksPlaygroundDesc_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | VolumesPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | JoinContainersWithImages_ShouldPass | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | ContainersPlaygroundDesc_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | ContainersPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | VolumesPlaygroundDesc_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | NetworksPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | ImagesPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Docker.Tests | ImagesPlaygroundDesc_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Ollama.Tests | DoSomeRealTests | Existing ignored test. |
| Musoq.DataSources.Ollama.Tests | DoSomeOtherRealTests | Existing ignored test. |
| Musoq.DataSources.Roslyn.Tests | Playground_WithoutTransitivePackages | Existing ignored test. |
| Musoq.DataSources.Roslyn.Tests | SolutionPlayground | Existing ignored test. |
| Musoq.DataSources.Roslyn.Tests | Playground_GetMetadataAsync | Existing ignored test. |
| Musoq.DataSources.Roslyn.Tests | Playground_GetDependenciesAsync | Existing ignored test. |
| Musoq.DataSources.Roslyn.Tests | Playground_WithTransitivePackages | Existing ignored test. |
| Musoq.DataSources.Git.Tests | CommitsPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | RemotesPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | TagsPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | FileHistoryWildcardPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | FileHistorySkipTakePlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | BranchesPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | WhenFileHistoryWithSkipTake_ShouldPass | Assert.Inconclusive failed. Repository does not have enough file changes to test skip/take |
| Musoq.DataSources.Git.Tests | StatusPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | FileHistoryPlaygroundDesc_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | RepositoryPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | FileHistoryTakePlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.Git.Tests | FileHistoryPlayground_ShouldBeIgnored | Existing ignored test. |
| Musoq.DataSources.RepresentativeTests | PluginPackages_WhenArtifactsExist_ShouldMatchRuntimeV2PackageContract | Assert.Inconclusive failed. No plugin packages found in: D:\repos\Musoq.DataSources\artifacts |
| Musoq.DataSources.Search.Tests | DefaultPolicy_ShouldExcludeReparseEntriesAndBrokenLinks | Assert.Inconclusive — niepowodzenie. The platform or test process does not have the privilege required for symbolic-link fixtures. |
| Musoq.DataSources.Search.Tests | FollowPolicy_ShouldGuardCyclesAndDuplicateDirectoryIdentities | Assert.Inconclusive — niepowodzenie. The platform or test process does not have the privilege required for symbolic-link fixtures. |
| Musoq.DataSources.Search.Tests | FollowPolicy_ShouldRejectOutsideRootTargets | Assert.Inconclusive — niepowodzenie. The platform or test process does not have the privilege required for symbolic-link fixtures. |

## Evidence and handoff

Evidence root: [artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1).

- [Gate commands, times and exit codes](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/gates.jsonl)
- [Final verification and source fingerprint](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/final-verification.json)
- [Core package provenance](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/core-package-provenance.json)
- [Resolved solution dependencies](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/resolved-core-dependencies.json)
- [Qualified fixture restore state](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/qualified-fixture-restore-state.json)
- [NuGet configuration snapshot](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/NuGet.Config.qualified.xml)
- [Final test totals](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/solution-test-summary.json), [TRX files](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/solution-final-trx), [skip inventory](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/skipped-tests.json)
- [Initial failure inventory](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/initial-failed-tests.json)
- [Initial build warnings](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/initial-build-warnings.txt)
- [Per-package receipts](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/package-results.json), [artifacts](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/packages), [artifact hashes](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/artifact-hashes.json)
- [Qualified tracked diff](artifacts/release-preparation-20261101-185304-core17.0.15-alpha.1/final-tracked.diff)

Local candidates were built from the uncommitted source state identified above.
A release commit, qualification/CI for the exact release commit, tags, and publication
remain deferred. Retain these artifacts for review; do not treat local preparation
as a published release or remote CI result.
