# W02-S01 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass

## Scope reviewed

- `Musoq.DataSources.sln`
- `Musoq.DataSources.Search/`
- `Musoq.DataSources.Search.Tests/`
- the W02-S01 focused and repository-wide test evidence

## Findings

No material correctness, contract, ownership-boundary, or provenance findings
remain for this scaffold.

The Search assembly is solution-discoverable, registers the `search` schema,
publishes XML documentation for its exported API, and exposes one exact
`matches(string root, string literal)` constructor. The static table metadata,
`SearchMatch` row type, and compiled-query result shape agree. Literal matching
is ordinal, non-overlapping, preserves one row per occurrence, orders files by
stable relative path, and keeps `PatternId` null for the single-pattern call.

The source planner accepts no projection, predicate, ordering, or window work;
all such work remains residual. This is conservative and prevents the
scaffold from claiming pushdown semantics it does not implement. File handles
are owned by a scoped `StreamReader`, content is consumed through a fixed
character buffer, match rows flush as independent bounded chunks, and
cancellation is checked before each file and during match enumeration.

The final focused suite passes 8 tests, including chunk-boundary and
cross-buffer tests that guard multiplicity, cursor advancement, and streaming
state. The final repository-wide Release suite passes 1,261 of 1,292 tests across 21 test assemblies, with 0 failures
and 31 existing expected skips.

## Residual boundaries

- This scope implements only the minimal single-pattern literal `matches`
  source. Request models, richer row fields, regex/multi-pattern/byte modes,
  encoding rules, scope/ignore policy, typed diagnostics, completion outcomes,
  and advanced source planning remain later scopes.
- Files are streamed with default `StreamReader` decoding and directory
  traversal has no scoped access-error or ignore policy yet; explicit encoding
  and traversal contracts are intentionally not advertised as complete Search
  behavior.
- The source does not declare configurable read, memory, or result budgets.
  It bounds emitted row chunks, while budget contracts and stronger lifecycle
  tests remain later W02/W03 work.
- Progress is limited to emitted-row reporting. Full progress/settings
  isolation and mid-read/disposal tests belong to W02-S03 and W02-S05.
- The Release build emits the existing-style NuGet missing-readme advisory for
  the new local package; it is non-fatal and no package was published.
- The user-requested focused-test policy was recorded in the local ignored
  campaign `GOAL.md`; it does not alter this product scope or source manifest.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release`: exit 0; 8 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 project summaries; 1,292 discovered, 1,261 passed, 0 failed, 31 skipped.
- `dotnet sln Musoq.DataSources.sln list`: both Search projects are listed.
- `git diff --check`: passed before evidence/commit finalization.
