# W05-S01 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchCharBuffer.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search.Tests/SearchBufferedReaderTests.cs`
- W05-S01 requirements, the Runtime V2 row/chunk contract, the Search source
  contract, and the resulting test/evidence diff

## Findings

No material correctness, ownership, resource-lifecycle, cancellation,
performance-boundary, or evidence-integrity finding remains for this scope.

The literal Search source now rents one bounded 8,192-character buffer for the
sequential source scan and returns it through a deterministic `IDisposable`
lease. The reader is disposed before the lease returns the array to the shared
pool, and repeated disposal is harmless. Only the requested 8,192-character
window is passed to `TextReader.Read`, even if the pool supplies a larger
bucket, so active pooled memory remains bounded.

The existing scanner consumes exactly the count returned by each read, so
irregular short reads and a partial final block do not expose stale buffer
contents. Cancellation is checked before and after reads and during character
consumption; an interrupted read still disposes the reader and the pooled
lease. Match rows retain scalar coordinates and immutable strings (`Path` and
the query literal), not pooled memory or a borrowed character span.

Focused tests cover direct short/irregular reads, premature EOF, an
interrupting reader, repeated buffer disposal, and rows retained across reuse
of the pooled buffer by multiple files. Existing boundary, lifecycle,
cancellation, disposal, diagnostic and compiled-query tests remain green.

The change is confined to the owning Search implementation and its tests. It
does not claim native grep integration, alter public constructor shape, edit a
sibling repository, publish a package, or change query-engine code.

## Residual boundaries

- The shared pool is the .NET runtime pool; the scope proves lease ownership
  and bounded usage, not a process-wide pool performance result.
- A `TextReader` that violates the .NET read contract by returning a count
  outside the supplied buffer range remains outside the source contract.
- The repository-wide suite retains 31 existing classified skips and the
  three W04-S04 platform-conditional symlink fixture skips; no package was
  published.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests/Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 79 discovered, 76 passed, 0 failed, 3 platform-conditional skips.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,363 discovered, 1,329 passed, 0 failed, 34 skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks/Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.
