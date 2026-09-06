# W02-S04 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchDiagnostics.cs`
- `Musoq.DataSources.Search/SearchContracts.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search/SearchSchema.cs`
- `Musoq.DataSources.Search.Tests/SearchSchemaTests.cs`
- direct `RowSourceBase.Chunks` and compiled-query negative paths

## Findings

No material correctness, ownership-boundary, or provenance findings remain for
this scope.

Search owns a stable `SEARCH-*` diagnostic catalog with five explicit phases,
bounded message/explanation/fix text, bounded path and argument locations, and
typed exceptions for request, pattern, encoding, resource, source-access,
source-read, and output failures. No reserved core `MQ` identifier was added or
reused.

Request construction now validates source arguments before filesystem work.
Missing roots fail instead of becoming successful empty scans. File-open,
file-read, and result-publication failures are classified separately, while
operation cancellation and already-typed Search failures are preserved.
Readers remain owned by the source and are disposed on the success and failure
paths established by W02-S03.

The compiled tests retain the typed Search exception inside the existing core
data-source lifecycle envelope. The envelope reports the existing core
`MQ7011` read boundary and does not expose a path in its safe message; the
plugin diagnostic remains available on the typed inner exception. This honors
the scope requirement not to fabricate reserved MQ codes.

## Residual boundaries

- The current public Search constructor is intentionally the minimal two-string
  literal entry point. Regex and encoding modes are not yet public Search
  features, so their diagnostic validators are exercised directly; adding an
  unapproved mode solely to manufacture a compiled test would change the later
  feature contract.
- The current core lifecycle envelope does not project an arbitrary plugin
  diagnostic identifier into its fixed MQ enum. Host-level diagnostic transport
  remains a later integration concern.
- The output-failure wrapper is implemented at the actual chunk-writer boundary;
  the sealed `RowSourceBase` path does not provide an injectable production
  writer, so its catalog contract is covered directly and the wrapper is
  reviewed against the runtime interface.
- Traversal policies, encoding application, budgets, progress isolation and
  terminal outcome transport remain later scopes.
- The Release build retains existing package vulnerability/missing-readme
  advisories; no package was published.
- The repository-wide suite retains 31 existing expected skips.

## Verification

- `dotnet build Musoq.DataSources.Search\\Musoq.DataSources.Search.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release`: exit 0; 27 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 project summaries; 1,311 discovered, 1,280 passed, 0 failed, 31 skipped.
- `git diff --check`: passed before evidence and commit finalization.
