# W05-S02 review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/LiteralMatcher.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search.Tests/SearchLiteralBoundaryTests.cs`
- `docs/search/search-literal-scanner-v1.md`
- W05-S02 requirements, the W01 physical-line/non-overlap semantics, the
  Runtime V2 row/chunk contract, and the W03 independent reference scanner

## Findings

No material correctness, ownership, resource-lifecycle, cancellation,
performance-boundary, compatibility or evidence-integrity finding remains for
this scope.

The production matcher uses the selected managed `SearchValues<char>`
primitive to skip non-candidate runs and ordinal span equality for complete
in-block candidates. A candidate that reaches a block boundary continues
through the bounded KMP prefix state and coordinate ring; it is emitted exactly
once when its final code unit is consumed. The post-match reset preserves
leftmost non-overlapping semantics, including adjacent matches.

The source passes only the returned reader count to the matcher, preserves
one pooled reader buffer from W05-S01, and converts emitted scalar spans into
owned `SearchMatch` rows. Absolute UTF-16 offsets, physical-line numbers and
columns continue across blocks. Newline handling discards partial record state,
and literals containing a newline produce no physical-record spans, matching
the independent line-scoped oracle.

Tests cover every split of a hand-written fixture, a pattern longer than every
block, adjacent and EOF-ending matches, physical-record boundaries,
deterministic randomized inputs at every non-zero chunk size, and the real
source with irregular reader blocks. The independent oracle is not shared with
the optimized matcher.

The change stays within the owning Search implementation, its tests and
Search documentation. It changes no public source signature, engine adapter,
sibling repository, release surface, package publication or normal-profile
installation.

## Residual boundaries

- Matcher state and the temporary per-block span list are bounded by the
  literal length and the fixed reader block/hit count; native grep integration,
  byte matching, regex and multi-pattern sharing remain later scopes.
- The current row source still materializes owned `SearchMatch` objects for
  emitted occurrences; nonmatches do not allocate row objects.
- The repository-wide suite retains its existing 34 classified skips and
  existing compiler/package-audit warnings; no package was published.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests/Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 85 discovered, 82 passed, 0 failed, 3 platform-conditional skips.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,369 discovered, 1,335 passed, 0 failed, 34 skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks/Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.
