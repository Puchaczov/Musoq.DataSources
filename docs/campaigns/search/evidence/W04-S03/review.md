# W04-S03 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchContracts.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search/SearchScopeTraversal.cs`
- `Musoq.DataSources.Search.Tests/SearchMetadataTests.cs`
- W04-S03 requirements, the prior scope boundary, the Search source contract,
  and the resulting test/evidence diff

## Findings

No material correctness, ownership, resource-lifecycle or evidence-integrity
finding remains for this scope.

The new metadata predicate seam is internal because the public request binder
does not yet expose name, extension, size or time predicates. When supplied,
name and extension checks run from the normalized candidate path; extension
comparison is exact and case-insensitive, so `CS` does not accidentally match
`.cs.bak`. Inclusive minimum/maximum byte sizes and inclusive UTC modification
time bounds are validated and applied before the reader factory is called.

The implementation avoids metadata work entirely when no metadata predicate is
present. It does not allocate a `FileInfo` unless a size bound is requested,
and it performs the timestamp read only when a time bound is requested. A
fresh metadata read is made for each enumeration, so a file-size change
between scans cannot be hidden by a stale per-process cache.

Metadata filters are applied only to regular-file candidates. They never
return `Skip` for a directory, preserving the W04-S02/W01-S03 rule that a
file-name or extension predicate cannot prune a directory whose descendants
may match. Scope counters record files considered, metadata reads, metadata
rejections and yielded candidates; the Search source additionally records
content-open attempts. The focused tests pair those counters with a reader
factory probe and prove that rejected candidates are not opened.

Metadata access failures retain the typed `SEARCH-SOURCE-002` source-access
diagnostic at the affected file path. Cancellation remains checked by the
existing W04-S01 frontier and scan loops, and the new metadata reads do not
introduce shared mutable state or change row multiplicity.

The public Search source constructor and SQL shape remain unchanged. No
public metadata API, sibling repository, query-engine adapter, release or
publish surface was modified.

## Required coverage

The focused fixtures cover uppercase extensions, the `.cs` versus `.cs.bak`
suffix trap, minimum-size rejection before content open, a file changing size
between fresh scans, exact inclusive modification-time boundaries, and a
directory whose name has a file-like suffix while its matching descendant is
still traversed.

## Residual boundaries

- The metadata policy is an internal implementation/test seam until the later
  request binder exposes explicit metadata predicates; no future public
  contract is claimed here.
- Metadata and content reads observe a live filesystem rather than an atomic
  snapshot. A file can change after its metadata passes and before its reader
  opens; existing typed read/open failures remain authoritative.
- Counters are per-run diagnostic evidence, not yet a public audit result;
  the later paths/explanation/audit scopes own that surface.
- Hidden-entry, symbolic-link/reparse-point, special-file and explicit
  inaccessible-entry policies remain later scopes.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 66 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,350 discovered, 1,319 passed, 0 failed, 31 existing expected skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks\\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.
