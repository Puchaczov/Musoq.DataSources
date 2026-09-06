# W04-S04 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass (no independent subagent was
available)

## Scope reviewed

- `Musoq.DataSources.Search/SearchFileTraversal.cs`
- `Musoq.DataSources.Search/SearchScopeTraversal.cs`
- `Musoq.DataSources.Search.Tests/SearchLinkPolicyTests.cs`
- the W04-S04 requirements, the W01-S03 scope contract, prior Search scope
  boundaries, and the resulting test/evidence diff

## Findings

No material correctness, ownership, resource-lifecycle or evidence-integrity
finding remains for this scope.

The existing `ScopePolicy.FollowLinks` option is now enforced. Reparse and
special entries are classified before they can become candidates; the default
policy skips them, and explicit follow mode resolves only supported link
targets. Explicit file roots are prepared by the scope matcher first, so a
file or directory reparse root cannot bypass the no-follow default.

Follow mode checks the resolved target against the physical search root before
the candidate reaches include, exclude, ignore, metadata or content handling.
Directory targets are tracked by normalized physical identity, which stops a
link back to an ancestor and avoids traversing two aliases of one directory.
Resolution also detects broken links and bounded link cycles. Hard-linked files
remain distinct lexical path candidates, matching the existing scope contract;
the implementation does not silently replace path evidence with file-ID
deduplication.

The classifier uses filesystem attributes and link-target metadata without
opening content. Device-marked special entries are excluded and no FIFO/socket
mode is exposed. The Windows host did not provide the privilege required for
live symbolic-link fixtures, so the three symlink-dependent tests are
explicitly inconclusive rather than being relabeled as passes. The available
hard-link, UNC containment and synthetic special/reparse policy checks passed.

Link-resolution loops check cancellation between resolution steps. Existing
frontier disposal, reader disposal, typed source diagnostics and row lifecycle
tests remained green. Diagnostic exceptions raised while classifying an entry
are no longer accidentally wrapped a second time as a directory-open error.

The public Search constructor and SQL shape remain unchanged. No sibling
repository, query-engine adapter, release, publish, install or external
filesystem state was modified.

## Required coverage

The focused additions cover default no-follow policy, explicit follow policy,
symlink loops, duplicate physical directories, outside-root targets, broken
links, reparse entries, hard-link path identity, UNC share-boundary
containment, and special-entry exclusion. The three live symbolic-link cases
are blocked by the current Windows process privilege and are recorded as
platform-conditional skips in the report; hard-link and UNC checks are live.

## Residual boundaries

- The current Windows test process cannot create symbolic links, so live
  symlink loop, reparse, broken-link and outside-target execution is not
  claimed as observed on this host.
- FIFO/socket fixtures are not available on Windows. The implementation has
  no separate special-file mode and excludes entries reported by the runtime
  as `FileAttributes.Device`; Unix special-file fixtures remain an applicable
  platform qualification.
- Physical directory identity is normalized resolved-path identity. Hard-link
  files intentionally retain separate path candidates, as required by the
  scope contract.
- The link policy remains an internal implementation seam until the later
  request binder exposes explicit scope options; no public API is claimed here.
- The repository-wide suite retains 31 existing classified skips, plus the
  three explicitly platform-blocked symlink fixtures from this scope; no
  package was published.

## Verification

- `dotnet test Musoq.DataSources.Search.Tests\\Musoq.DataSources.Search.Tests.csproj --configuration Release --no-restore`: exit 0; 72 discovered, 69 passed, 0 failed, 3 platform-conditional skips.
- `dotnet test --configuration Release`: exit 0; 21 test projects; 1,356 discovered, 1,322 passed, 0 failed, 34 skips, consisting of 31 existing classified skips and 3 platform-conditional symlink fixture skips.
- `dotnet build Musoq.DataSources.Search.Benchmarks\\Musoq.DataSources.Search.Benchmarks.csproj --configuration Release --no-restore`: exit 0; 0 warnings, 0 errors.
- `git diff --check`: passed before evidence and commit finalization.
