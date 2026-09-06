# W04-S01 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchFileTraversal.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search.Tests/SearchTraversalTests.cs`
- W04-S01 requirements, source references, existing Search contracts and the
  resulting test/evidence diff

## Findings

No material correctness, ownership, resource-lifecycle or evidence-integrity
finding remains for this bounded traversal scope.

The production path now keeps an explicit stack of live directory
enumerators. It consumes one entry at a time, retains only the current
directory frame per open depth level, avoids the previous directory-wide
`OrderBy` buffer, and checks cancellation before and after filesystem
enumerator advancement. Each frame is disposed on exhaustion, cancellation,
access failure or consumer disposal.

Entries are represented by a compact path/kind record. Deleted or otherwise
non-file entries are not invented as candidates; if a discovered candidate
vanishes before its reader opens, the existing typed `SEARCH-SOURCE-002`
failure remains visible instead of becoming a successful partial or empty
scan.

The tests exercise one-entry-at-a-time consumption with a synthetic
enumerator, frontier disposal on cancellation, real deep and wide trees,
empty directories, and deletion after candidate discovery. Existing Search
row, cancellation, diagnostic and compiled-query tests continue to pass.

The change is limited to the owning Search implementation and tests. It does
not modify the public API, sibling repository, query engine, release surface
or later ignore/link policy scopes.

## Residual boundaries

- Filesystem enumeration order is intentionally not a SQL ordering contract;
  callers requiring order must use `ORDER BY`, while later scope resolution
  owns deterministic candidate manifests and ignore/link policies.
- The underlying platform enumerator can still observe a live, changing
  filesystem; a candidate access failure is typed rather than silently
  reclassified as an exhaustive result.
- This scope proves bounded traversal behavior, not whole-search memory or
  throughput targets; those remain benchmark and later implementation work.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 57 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,341 discovered, 1,310 passed, 0 failed, 31 existing expected skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks\\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.
