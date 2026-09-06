# W05-S03 review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchTextSourceBase.cs`
- `Musoq.DataSources.Search/SearchTextScanner.cs`
- `Musoq.DataSources.Search/SearchTextSinks.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search/SearchLinesSource.cs`
- `Musoq.DataSources.Search/SearchFilesSource.cs`
- `Musoq.DataSources.Search/SearchLine.cs`
- `Musoq.DataSources.Search/SearchFile.cs`
- Search schema, helper/table metadata and planner changes
- `Musoq.DataSources.Search.Tests/SearchSchemaTests.cs`
- `Musoq.DataSources.Search.Tests/SearchSinkTests.cs`
- `docs/search/search-sink-contract-v1.md`
- W05-S03 requirements and the occurrence/line/file source contract

## Findings

No material correctness, ownership, resource-lifecycle, cancellation,
projection-invariance, compatibility or evidence-integrity finding remains for
this scope.

The three content sources share one traversal, reader and `LiteralMatcher`
pipeline. `matches` emits every occurrence with a per-path ordinal, `lines`
groups spans by physical line and retains the decoded line terminator, and
`files` emits exactly one row after the first qualifying span. The file sink
returns from the scanner immediately after that row, so later occurrences and
reader blocks cannot change its existential cardinality.

The bounded pooled input buffer is never exposed through a row. Sink chunks
are copied before their staging list is reused. Reader disposal and the
existing typed open/read/output diagnostics remain active through the shared
path. Line grouping keeps its occurrence map across reader blocks, and the
separate line-text cursor remains synchronized with the matcher’s physical
line coordinates.

Schema metadata, constructor inventory, XML documentation and compiled
`SELECT *` contracts cover all four currently executable Search sources.
Focused tests cover repeated paths, multiple occurrences on one line,
zero-hit exclusion, projection invariance, existential early termination and a
matching physical line that crosses the 8,192-character reader boundary.

The change stays within the owning Search implementation, its tests and Search
documentation. It changes no sibling repository, engine adapter, release,
publication or installation surface.

## Residual boundaries

- `lines` retains the current physical line text in a `StringBuilder`; explicit
  line-size/resource budgeting remains a later scope concern.
- The sources remain literal, case-sensitive, decoded-text sources. Byte
  matching, regex, multi-pattern sharing, virtual origins and count/audit rows
  remain later scopes.
- The repository-wide suite retains its existing 31 expected skips and 3
  platform-conditional symlink skips, plus existing compiler/package-audit
  warnings; no package was published.

## Verification

- `dotnet test .\Musoq.DataSources.Search.Tests\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 89 discovered, 86 passed, 0 failed, 3 platform-conditional skips.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,373 discovered, 1,339 passed, 0 failed, 34 skips.
- `dotnet build .\Musoq.DataSources.Search.Benchmarks\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.

