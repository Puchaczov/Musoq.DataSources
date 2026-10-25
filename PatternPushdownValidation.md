> Historical alpha.1 qualification. Current runtime and release preparation are recorded in
> [CoreAlpha2ReleaseValidation.md](CoreAlpha2ReleaseValidation.md). The original results below are preserved.

# Native filtering qualification

This six-wave implementation is local to Musoq.DataSources, based on HEAD
`5e771fcf9b09f735b3babcf1c4c99b7dd6b53b21`. No commits, installations, publication or
remote changes are part of this task. Core stays at **17.0.14-alpha.1**; production
package versions, project files and `scripts/release/packages.json` are unchanged.
No Core files were modified. The previous qualification is retained below as
historical evidence; its HEAD, totals and artifact paths describe that earlier run.

## Current wave gates

Every wave runs focused execution tests, a Release solution build, and the full
22-project Release suite without `-m:1`. Authoritative logs/TRX/per-project counters
are in `artifacts/early-pattern/waveN/`, with `result.json` for each completed gate.

| Wave | Focused evidence | Release build | Full passed | Failed | Existing skips |
|---|---|---|---:|---:|---:|
| 1 Shared/OS | OS pattern execution; 112 Representative checks | Pass | 1970 | 0 | 34 |
| 2 Search | Search 360 passed / 3 existing skips; 112 Representative checks | Pass | 1975 | 0 | 34 |
| 3 Git | Git 138 passed / 12 existing skips; OS patterns; 112 Representative checks | Pass | 1982 | 0 | 34 |
| 4 Archives/APIs | Native/early execution across providers; 112 Representative checks | Pass | 1992 | 0 | 34 |
| 5 JSON/CSV | JSON 125/0/0; CSV 258/0/0; 112 Representative checks | Pass | 2000 | 0 | 34 |
| 6 Final packages | 48 native/early and 112 Representative checks; packaged Representative suite 178/0/0 | Pass | 2001 | 0 | 33 |

The initial Wave 1 run recorded 1968 passed, 1 failed, 34 skipped. An existing CSV
source-architecture assertion expected the explicit generic materializer call;
the call had used type inference. Restoring the explicit type argument preserved
behavior and the next full run passed. That attempt is retained under
`wave1-attempt1/`. Wave 2's first passing run and final diagnostic-only rerun are
both retained. No test was newly ignored or made inconclusive.

## Execution evidence and intentional behavior

[The stage matrix](PatternPushdownCoverage.md#execution-stages-and-work-avoided)
distinguishes enumeration, pre-open, native-record, decoded-field and row filtering.
Existing capability/application records and the 308 static source-column entries
are unchanged. New tests exercise actual readers and SDK objects, not only plans.

- OS proves rejected metadata files are never opened using an exclusively locked
  file, while an admitted locked file still fails. Rejected parent directory rows
  do not prevent visiting matching descendants. Native enumeration also observes
  linked reader cancellation and retains admitted entry metadata caches.
- Search executes all five content sources, four LIKE shapes/RLIKE and negation,
  sequential/parallel readers and typed constructors. Path rejection precedes
  observation/opening; file-count limits still apply, byte charges do not. Counts
  had an existing TAKE 0 no-scan gap; the complete-scan coordinator path corrects
  it while retaining audit completion. Effective scope fingerprints include early
  patterns and execution culture.
- Git tests managed/CLI parity, predicate-first captured protocols, exact one-command
  execution, skipped token framing, rich-field avoidance, native tags and original
  filehistory constructor windows. Rejected selected history rows cannot select
  older rows. Nested identity tokens and required traversals remain.
- Archive tests validate wrapper counts, raw indexes through content retrieval and
  exclusive reopen after disposal. No extraction is performed by filtering.
- Docker avoids rejected DTO wrappers with one request. Real default GitHub/Jira
  adapters use mocked SDK connections/services: empty filtered pages continue using
  raw counts/offsets; safe SKIP/TAKE stops subsequent requests. All raw string bindings
  are executed against Core and existing getter semantics. Alternate API mocks
  retain row filtering. Overlapping/repeated default-client executions, cancellation
  after response and finite typed timeout causes are covered.
- JSON/CSV prove sparse slots, multiple predicates, escaping, pooling, rejected-row
  validation, width failures and cleanup after cancellation/materializer failure.
  JSON discovery still rejects duplicate fields; a controlled processor test also
  preserves its prior last-occurrence overwrite behavior. Existing TABLE/COUPLE,
  null/missing, dotted-header, projection, ordering/paging and parallel suites pass.

## Isolated Release measurements

Measurements are informational, never timing assertions. Source/SDK fixtures and
result counts are identical across each comparison; warmup is excluded. Deterministic
work counters are acceptance evidence. No nonselective case adds source I/O, API
requests or repository traversals. Measurements do not claim remote-service latency
improvements or avoided archive-format traversal.

The machine-readable comparison, exact elapsed times and allocation samples are
retained in `artifacts/early-pattern/measurements.json`, with original TRX output.
Scopes: OS enumeration of 1000 files;
Git one captured 100-record CLI command with 4KB message fields (20 iterations);
GitHub mapper over 1000 already-returned SDK objects (20 iterations); JSON/CSV
compiled executions over 1000 escaped ~1KB strings (5 iterations), excluding
initial schema discovery and compilation. OS/JSON/CSV use total process allocation
counters; synchronous Git/mapper probes use current-thread allocation counters.

Selective execution reduces claimed work and allocations. Nonselective OS and API
mapping can allocate or take longer due to early matching/bookkeeping. Structured
reuse benefits accepted escaped strings most; it preserves existing pooling for
unescaped strings. Small-selectivity elapsed-time regressions remain reported.
There are no flaky timing pass/fail thresholds.

Samples below cover all measured iterations, rather than a per-iteration average.

| Fixture | Selectivity | Elapsed ms, row → optimized | Allocated bytes, row → optimized |
|---|---:|---:|---:|
| OS | 1% | 1.2114 → 1.2732 | 531,624 → 288,904 |
| OS | 50% | 0.9125 → 0.8856 | 528,576 → 526,448 |
| OS | 100% | 2.5447 → 2.3742 | 527,904 → 770,272 |
| Git | 1% | 3.1835 → 1.2852 | 16,685,664 → 358,464 |
| Git | 50% | 3.3889 → 2.4933 | 16,685,664 → 8,457,184 |
| Git | 100% | 2.8621 → 3.1237 | 16,685,664 → 16,721,184 |
| GitHub DTO mapper | 1% | 2.2292 → 3.8457 | 643,424 → 171,744 |
| GitHub DTO mapper | 50% | 2.1016 → 2.5167 | 643,424 → 406,944 |
| GitHub DTO mapper | 100% | 1.9655 → 2.7861 | 643,424 → 646,944 |
| JSON | 1% | 11.5520 → 11.1177 | 16,667,480 → 16,571,040 |
| JSON | 50% | 35.2241 → 34.3063 | 21,873,512 → 16,692,696 |
| JSON | 100% | 40.4116 → 28.2351 | 27,180,936 → 16,788,384 |
| SeparatedValues | 1% | 95.5290 → 95.0494 | 21,221,904 → 21,066,040 |
| SeparatedValues | 50% | 142.5804 → 99.9834 | 29,002,168 → 21,162,544 |
| SeparatedValues | 100% | 154.3676 → 113.8921 | 37,111,144 → 21,285,376 |

## Final local scripts, licenses, packages and boundaries

Restore and the prepack Release build passed (zero warnings/errors); the focused
native/early execution suite passed **48/0/0**. All four repository script checks
passed, including five Search environment scenarios and 15 assertions. License
graph validation passed for all 16 checked-in snapshots; project/dependency inputs
are unchanged, so no snapshot regeneration was needed. All 16 packages packed in
manifest order into **64 platform archives**, with per-package smoke checks and
the combined archive-license check passing. The final packaged-artifact Release
suite passed **2001 passed / 0 failed / 33 skipped**, 2034 total tests across
22 projects. The final build passed with zero warnings/errors. All six wave gates
are complete; no new skips were introduced.
`final-preparation.json` and its associated logs record these local gates.
All artifacts for this implementation use `artifacts/early-pattern/`; previous
`artifacts/pattern-pushdown/` and upgrade reproducers remain historical evidence.

The 34 prepack skips comprise 29 existing ignored live/playground tests, one Git
fixture with insufficient history, three Windows symlink-privilege cases, and the
optional Representative package smoke before artifacts are provided. With artifacts
available, that smoke test passed, leaving **33 existing skips**: Docker 9 ignored,
Git 11 ignored plus 1 insufficient-history fixture, Roslyn 5 ignored, Ollama 2
ignored, OpenAI 1 ignored, System 1 ignored, and Search 3 symlink-privilege cases.
Exact test names and emitted reasons are retained in
`artifacts/early-pattern/skip-inventory.json`; comparison with Wave 5 asserts no
new skipped test. Core limitations documented in
the historical section below remain upstream; this task does not modify Core.

`core-package-reverification.json` rehashes all five cached 17.0.14-alpha.1 nupkgs
against retained published evidence and checks their NuGet.org source metadata.
`package-integrity.json` verifies every copied platform archive against its
pack-time SHA-256 and size. `working-tree-integrity.json` records the final local
file hashes, unchanged HEAD, empty index, and unchanged central pin, csproj inputs
and release manifest. Only DataSources files were changed; no Core edits, commits,
installations, publication or remote mutations were performed.

---

# Historical pattern pushdown qualification

Qualification date: 2026-10-24. Host: Windows x64, .NET SDK **10.0.402**.
Runtime: **17.0.14-alpha.1** for Converter, Evaluator, Parser, Plugins and Schema.
Scope: `D:\repos\Musoq.DataSources` only. No commits, tags, publication or installation.
Starting HEAD: `98d85f0b15b0140ba63b9ef0a9220acafeeaa8e8`. Datasource versions and
`scripts/release/packages.json` are preserved. Core was checked clean without edits.

## Wave gates

Each wave ran focused tests, a Release solution build, and the full Release solution
suite across **22 test projects**, without `-m:1`. A wave's full gate completed before
the next wave changed code. Existing work was preserved.

| Wave | Focused passed | Full-suite passed | Failed | Skipped | Result |
|---|---:|---:|---:|---:|---|
| 1: shared matcher and typed eligibility | 4 | 1920 | 0 | 34 | Passed |
| 2: native/API providers | 85 | 1933 | 0 | 34 | Passed |
| 3: JSON, separated values, FlatFile | 97 | 1946 | 0 | 34 | Passed |
| 4: Search and Roslyn | 104 | 1959 | 0 | 34 | Passed |
| 5: final qualification with packaged artifacts | 111 | 1967 | 0 | 33 | Passed |

The reusable local gate is `artifacts/pattern-pushdown/Run-Wave.ps1`:

```powershell
dotnet test Musoq.DataSources.RepresentativeTests -c Release --no-restore --nologo --filter 'FullyQualifiedName~Pattern|FullyQualifiedName~Alpha14' --logger trx --results-directory <wave>/focused
dotnet build Musoq.DataSources.sln -c Release --no-restore --nologo -v quiet
dotnet test Musoq.DataSources.sln -c Release --no-build --no-restore --nologo --logger trx --results-directory <wave>/full
```

Exact per-project totals, TRX files and logs are in
`artifacts/pattern-pushdown/wave{1..5}/`, with one `result.json` per completed wave.
No new test was skipped to avoid a failure. The final package smoke test is enabled
by `MUSOQ_PLUGIN_ARTIFACTS_DIR=artifacts/pattern-pushdown/packages`.

## Focused execution evidence

- Every static supported entry in [the coverage matrix](PatternPushdownCoverage.md)
  has execution coverage: four LIKE shapes and constant RLIKE, each negated and
  non-negated, with capability/application identity assertions and predicate-only
  projection retention. Final native tests cover 308 source-column entries across
  54 source methods, plus dynamic JSON/separated-values string contracts.
- Docker, GitHub and Jira use controlled services, both schema and direct native
  source constructors, and exact request counts. Git uses a local repository;
  archive/CAN/file providers use controlled fixtures. No live service is required.
- Search verifies completed audit accounting even for TAKE 0, raw-byte null text,
  retained text, cancellation, repeated and concurrent executions. Roslyn uses an
  in-memory solution and independently checks solution rows and project filtering.
- Compiled-query tests compare pushdown against disabled capabilities. Coverage
  includes TABLE/COUPLE, declared numeric-looking text without loss of leading
  zeros, literal dotted names (including a conflicting leaf-name column), missing
  values, mixed predicates/residual work, ordering/paging, computed datasource
  arguments, PARAM/PARAMS/LET paging, validation before opening sources, correlated
  System/JSON execution, and revised null semantics.
- Shared matcher tests compare Core Operators across cultures and Unicode values,
  including concurrent culture changes on one bound predicate. They verify
  disposal on early TAKE, residual OR and malformed classified needles. Timeout
  integration preserves the public typed exception and the supplied finite budget.

A final coverage check found and fixed a separated-values name-resolution gap:
`[Display.Label]` must bind the exact header before alias normalization. Both
families, negation and the disabled-pushdown baseline execute the regression.

Focused logs include `wave3-flatfile.log`, `wave3-focused-final.log`,
`wave4-search.log`, `wave4-roslyn.log`, `final-native.log`,
`wave5-separatedvalues.log`, `wave5-representative.log` and
`wave5-final-regressions.log` under `artifacts/pattern-pushdown/`.

## Restore, scripts, licenses and packages

The five restored Core `.nupkg` archives were rehashed against the previously
verified published NuGet package bytes, accounting for the repository's candidate
feed and private package-cache directory. All five matched; evidence is
`artifacts/pattern-pushdown/core-package-reverification.json` and
`artifacts/upgrade-17.0.14-provenance.json`.

Final preparation passed: **15 native-pattern tests passed, 0 failed, 0 skipped**,
all four script checks, all 16 package-definition/pack/smoke checks, all license
graphs, and **64 platform ZIPs**. Search environment resolution passed 5 scenarios
with 15 assertions. Final preparation runs `dotnet restore Musoq.DataSources.sln --force-evaluate`,
a Release build and the native pattern tests. It then runs:

```powershell
pwsh -NoProfile -File scripts/Test-PluginReleaseScripts.ps1
pwsh -NoProfile -File scripts/release/Test-LicenseSnapshotContracts.ps1
pwsh -NoProfile -File scripts/release/Test-SearchReleaseRegistry.ps1
pwsh -NoProfile -File scripts/search/Test-SearchEnvironmentResolution.ps1
pwsh -NoProfile -File scripts/release/Assert-LicenseSnapshots.ps1 -PluginName All -ValidatePackageGraph
```

License snapshots were refreshed when FlatFile's project inputs changed; all
snapshots and resolved dependency graphs are validated. Existing XML-comment,
package-readme and obsolescence warnings do not count as failed gates.

Each of the 16 package definitions is validated using its unchanged
`<version>-<packageId>` tag string, then locally packed with `Pack-Release.ps1`
and checked with `Test-ReleaseSmoke.ps1`. Each package includes four platform
archives, its NuGet/symbol packages and integrity metadata. Platform ZIP targets:
Windows x64, Linux x64, macOS arm64, Alpine x64. Package tests inspect all four
archives; runtime tests execute on the Windows host, not on those other platforms.

Artifacts are retained per package in
`artifacts/pattern-pushdown/release-packages/<slug>/`. The exact 64 ZIPs are also
collected in `artifacts/pattern-pushdown/packages/` for the final suite.
`Assert-ReleaseLicenseArtifacts.ps1 -PluginName All -ExpectedArchiveCount 4`
validates their license payloads. Final preparation records each result in
`artifacts/pattern-pushdown/final-preparation.json` and retains separate pack,
smoke, script and license logs. Packaging and smoke qualification completed successfully. The final packaged-artifact
suite completed with **1967 passed, 0 failed, 33 existing skips (2000 total)**. All
five Release builds and full-suite gates passed; final build: 0 warnings, 0 errors.

## Existing skips

The 34 skips in waves 1–4 consist of:

- 29 existing ignored tests: Docker 9, Git playgrounds 11, Roslyn playgrounds 5,
  Ollama live-service playgrounds 2, OpenAI live-service playground 1, and a legacy System
  range/RowNumber test 1.
- Git `WhenFileHistoryWithSkipTake_ShouldPass`: this checkout lacks enough file
  changes for that existing fixture (1).
- Search symlink/reparse fixtures: the Windows process lacks the privilege needed
  to create the links (3).
- Representative package smoke: no package ZIPs in the default artifact directory
  before final packaging (1). This skip must disappear in wave 5.

Wave 5 has **33 existing skips**: the same 29 ignored tests, one insufficient-history
fixture and three symlink-privilege skips. The package smoke test passed, with all
177 RepresentativeTests passing and no skips.

The final inventory with exact test names and recorded reasons is retained in
`artifacts/pattern-pushdown/skip-inventory.json`. Newly added pattern tests have no
Ignore/Inconclusive escape path.

## Upstream limitations retained without Core changes

**LIKE baseline compilation.** With `rows.json` containing null and non-null Name
values, disabled provider pushdown fails to compile this query in Core 17.0.14:

```sql
select d.Id from json.file('rows.json') d where d.Name not like '%' order by d.Id
```

Core emits CS0023: negation applied to a string. The provider-enabled query works
and returns only the null Name row. The regression
`CoreNotLikeAnyStringBaseline_HasRetainedUpstreamReproducer` asserts the baseline
failure and the successful provider result; shared matcher tests supply the
independent Core Operators oracle. No test is skipped.

**Regex timeout budget/diagnostics.** Compile a JSON query using
`Name rlike '^(?=(a+)+$)a+$'`, with Name equal to 50,000 `a` characters followed by
`!`, and `CompilationOptions.WithRegexMatchTimeout(1ms)`. Core supplies a two-second
source predicate in this release. Providers honor that supplied finite budget and
preserve `RegexMatchTimeoutException` as a typed inner cause. Core's datasource
lifecycle maps the public exception to MQ7011; MQ7017 currently requires Core's
internal timeout wrapper. Production providers do not reference Evaluator or
construct its internal exceptions. The passing regression is
`RegexTimeout_PreservesPublicTypedCauseAndBudget`.

**JSON self-correlation.** Prior 17.0.14 qualification retained two reproducers:
equality correlation fails on generated `_sq_1_key`; general JSON/JSON correlation
fails on duplicate generated carriers/materializers. The SQL files, fixture and
original evidence are in `artifacts/upgrade-17.0.14-upstream-repros/` and its
`known-issues.json`. Passing System/JSON correlated execution is covered separately.

**Transfer boundaries.** Unicode LIKE patterns, column-valued patterns,
cross-source/nested APPLY expressions, unsupported wildcard shapes and unsupported
capability versions remain residual. Regex patterns can contain Unicode and inline
flags. These transfer limits are not missing advertised provider operations.

## Final working-tree boundary check

HEAD remains `98d85f0b15b0140ba63b9ef0a9220acafeeaa8e8`; no commits were made.
All 19 versioned project files retain their original version values. Runtime pins
and the release manifest are unchanged. Core's working tree is clean. `git diff
--check` passes. Evidence is in `artifacts/pattern-pushdown/version-boundaries.json`.
The final changes consist of DataSources implementation, tests, authoring guidance,
the coverage/validation documents and the affected FlatFile license snapshot.
