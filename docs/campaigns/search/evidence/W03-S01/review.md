# W03-S01 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search.Tests/SyntheticSearchCorpusTests.cs`
- `Musoq.DataSources.Search.Tests/TestData/SyntheticSearchCorpus/manifest.json`
- Search test-project content-copy behavior
- deterministic generation, encoding, path and cleanup boundaries

## Findings

No material correctness, ownership-boundary, or corpus-contract findings
remain for this scope.

The fixture generator is test-owned and writes only beneath caller-created
temporary roots. It uses an explicit `xorshift32-v1` algorithm and direct byte
writes, so generation does not depend on the runtime `Random` implementation,
host code page, default encoding or line-ending conversion. Relative paths are
validated before directories and files are created, and both temporary roots
are removed in a `finally` block.

The manifest records the seed, generator version, literal, platform
prerequisites, file categories, expected byte totals, expected occurrence
units and exact UTF-16/byte spans. The generated corpus includes sparse and
dense truth inputs, a line beyond the source buffer size, Unicode composed and
decomposed text, 32 small scale files, an ignored subtree, UTF-8/UTF-16
encodings and binary payloads. The test regenerates the corpus twice and
compares every relative-path/kind/length/hash signature before validating the
declared units with a separate ordinal span routine.

## Residual boundaries

- The corpus test validates deterministic bytes and declared spans; it does
  not select a scanning backend or claim that the current minimal Search
  source already implements ignore rules, UTF-16 decoding or raw-byte search.
  Those are later implementation scopes.
- The scale group records aggregate expected units rather than duplicating 32
  per-file entries in JSON; each generated file is still independently hashed,
  and the aggregate span count is checked.
- The corpus is bounded test data, not a benchmark or a production filesystem
  snapshot. Performance and backend equivalence remain later W03 scopes.
- The repository-wide suite retains its existing classified skips and package
  advisories; no package was published.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~SyntheticSearchCorpusTests`: exit 0; 1 passed, 0 failed, 0 skipped.
- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 33 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 project summaries; 1,317 discovered, 1,286 passed, 0 failed, 31 skipped.
- `git diff --check`: passed before evidence and commit finalization.
