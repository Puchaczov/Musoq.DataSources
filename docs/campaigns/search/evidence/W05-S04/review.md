# W05-S04 review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchTextSourceBase.cs`
- `Musoq.DataSources.Search/SearchTextScanner.cs`
- `Musoq.DataSources.Search/SearchTextSinks.cs`
- `Musoq.DataSources.Search/SearchCount.cs`
- `Musoq.DataSources.Search/SearchCountsSource.cs`
- `Musoq.DataSources.Search/SearchCountsHelper.cs`
- `Musoq.DataSources.Search/SearchCountsTable.cs`
- Search schema and planner changes
- `Musoq.DataSources.Search.Tests/SearchCountTests.cs`
- `Musoq.DataSources.Search.Tests/SearchSchemaTests.cs`
- `docs/search/search-sink-contract-v1.md`
- W05-S04 requirements and the count-source contract

## Findings

No material correctness, ownership, resource-lifecycle, cancellation,
projection-invariance, compatibility or evidence-integrity finding remains for
this scope.

The shared source invokes the sink completion hook only after the scanner has
returned successfully and cancellation has been observed. The count sink does
not emit occurrence rows; it accumulates checked 64-bit occurrence and
matching-line counters and emits exactly one row, including a zero-hit row,
only at complete-file time. A read or open failure therefore cannot be
published as an exact partial count.

`BytesScanned` is taken from the regular input file's original byte length
after successful decoded EOF. The current public source uses a stable regular
file reader, so a completed scan accounts for the complete input byte payload;
the injected reader factory remains an internal test seam. Sink staging remains
bounded and chunks retain owned row snapshots rather than pooled input memory.

Schema metadata, constructor inventory, XML documentation and compiled
projection tests cover the new `counts` source. The planner recognizes the
source without accepting predicates, ordering, columns or aggregate/count
pushdown, so residual SQL semantics remain visible to the host.

The change stays within the owning Search implementation, its tests and Search
documentation. It changes no sibling repository, query-engine adapter,
release, publication or installation surface.

## Residual boundaries

- The source remains a literal, case-sensitive decoded-text source; regex,
  byte search, multi-pattern sharing, virtual origins and audit rows remain
  later scopes.
- Byte offsets for matching-line rows remain unavailable in the decoded source;
  count byte accounting is file-level only.
- The repository-wide suite retains its existing 31 expected skips and 3
  platform-conditional symlink skips; no package was published.

## Verification

- `dotnet test .\Musoq.DataSources.Search.Tests\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 93 discovered, 90 passed, 0 failed, 3 platform-conditional skips.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,377 discovered, 1,343 passed, 0 failed, 34 skips.
- `dotnet build .\Musoq.DataSources.Search.Benchmarks\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.
