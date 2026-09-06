# W04-S02 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchContracts.cs`
- `Musoq.DataSources.Search/SearchFileTraversal.cs`
- `Musoq.DataSources.Search/SearchMatchesSource.cs`
- `Musoq.DataSources.Search/SearchScopeTraversal.cs`
- `Musoq.DataSources.Search.Tests/SearchScopeTests.cs`
- W04-S02 requirements, the frozen scope contract, repository ignore policy,
  and the resulting test/evidence diff

## Findings

No material correctness, ownership, resource-lifecycle or evidence-integrity
finding remains for this scope.

Include and exclude globs are compiled once per scope enumeration. Ordinary
file globs match the candidate basename while path-qualified and anchored
patterns match normalized relative paths. Directory-only excludes are the
only convenience excludes that prune a directory; an extension include still
allows traversal into directories that may contain matching descendants.

Repository rules are loaded lazily by directory, in ancestor-to-descendant
order, and retain source path, precedence, directory depth, line number and
rule order on every compiled rule. The selected rule therefore has the
provenance needed by a later scope explanation/fingerprint surface. The
implemented precedence is `.gitignore` < `.ignore` < `.rgignore`, with later
matching rules winning within the same source/level. An ignored parent is
pruned before its descendant ignore file can be loaded, so a descendant
negation cannot reopen that parent.

The policy callback is integrated into the W04-S01 explicit directory
frontier. It skips denied entries before descent, disposes the existing
frontier on callback failure/cancellation, and never materializes a directory
wide candidate list. An explicitly supplied file root remains the complete
candidate set and bypasses directory-scoped ignore rules as required by the
root-file contract.

The public Search source constructor and SQL shape remain unchanged. The
configured global-rule collection is an internal frozen seam for the future
request/configuration binding; global ignores remain disabled by default.
Row multiplicity and content scanning are unchanged after policy eligibility
is decided.

## Required coverage

The focused fixtures cover nested negation, ignored-parent pruning, anchored
paths, directory-only rules, escaped `#`/`!` literals, later-rule ordering,
source precedence, include/exclude filtering without unsafe directory
pruning, configured global-ignore opt-in, repository-ignore disablement,
explicit file-root override, and frozen global-rule input.

The independent proposed scope-semantics harness also passed all six
scenarios and 44 assertions. Existing pinned ripgrep protocol/comparator
tests remain green in the repository-wide validation.

## Residual boundaries

- Hidden-entry, symbolic-link/reparse-point, special-file and explicit
  inaccessible-entry policies remain later implementation scopes; this scope
  does not silently claim those flags are complete.
- The public request binder does not yet expose the internal configured global
  rule collection, and this scope does not add a machine-global ignore-file
  lookup. That binding/configuration and the scope fingerprint belong to later
  contract scopes.
- Rule evaluation is intentionally bounded by the loaded rule list, but the
  full candidate decision manifest and source-content hashes are not yet
  emitted as an audit result.
- Filesystem enumeration remains live rather than snapshot-atomic, and SQL
  result order still requires an explicit `ORDER BY`.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 63 passed, 0 failed, 0 skipped.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,347 discovered, 1,316 passed, 0 failed, 31 existing expected skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks\\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `pwsh -NoProfile -File scripts\\search\\Test-SearchScopeSemantics.ps1`: exit 0; 6 scenarios, 44 assertions.
- `git diff --check`: passed before evidence and commit finalization.
