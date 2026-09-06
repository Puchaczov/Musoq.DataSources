# W12-S05 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds deterministic live-source mutation observation to the Search
file-processing boundary, a typed source-change terminal diagnostic, stronger
source identity for retained evidence, and focused recovery tests. The review
covered `SearchSourceObservation`, Windows handle identity capture, terminal
accounting, evidence expansion error mapping, the schema diagnostic inventory,
the mutation tests and the live-source contract documentation.

## Findings

- No blocking correctness, ownership, compatibility, cancellation or cleanup
  findings were identified.
- Each eligible file records its observed length and timestamps before work and
  validates them before `FilesCompleted` is recorded. Windows also compares the
  volume/file identity from the open handle, so a replacement path is not
  silently accepted when metadata is restored. A detected mutation becomes
  `SEARCH-SOURCE-005` / `source-changed`, leaves the execution incomplete and
  keeps `CountsExact` false.
- The guard is placed after both binary classification and text scanning. A
  mutation cannot be converted into a completed binary skip or a successful
  match/file/count result. Rows already staged before a failure remain observed
  prefix evidence and the terminal outcome communicates that they are not
  exhaustive.
- Context expansion retains its existing full content-hash validation and now
  maps decoder/read I/O failures to the typed source-read diagnostic without
  swallowing cancellation or stale-source failures.
- The deterministic reader seam exercises append, truncate, rename, replace,
  permission/open denial, read failure, decoder failure and stale evidence
  handles without timing-dependent races. The same-size replacement test
  preserves metadata on Windows and therefore exercises the file-identity
  comparison.
- The production Search boundary contains no external-process execution. The
  only process APIs in the Search tree remain in the test-only ripgrep oracle,
  which uses the previously reviewed argument-vector and shell-disabled path.

## Residual boundaries

- This is an observed-read guard, not an atomic live-filesystem snapshot. A
  mutation after the final validation can still occur; immutable input is
  required when a caller needs snapshot semantics.
- Non-Windows platforms use metadata checks without claiming a portable inode
  identifier. An in-place edit that preserves all observed metadata can remain
  undetected, which is why the contract does not advertise atomicity.
- The extra bounded metadata/identity checks add file-system boundary work per
  eligible file; no new throughput claim is made by this scope.

## Verification

- Mutation-focused tests: 14 passed, 0 skipped, 0 failed.
- Search Release suite: 312 total, 309 passed, 3 platform-conditional skips,
  0 failed.
- Contract harnesses: 27 scenarios and 314 assertions, all passed.
- Exact repository-wide Release suite: 21 projects, 1,596 total, 1,562
  passed, 34 classified skips, 0 failed.
- Search test-project build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed.
- The process-boundary scan found only the test-only ripgrep comparator process
  APIs; no production Search automatic execution path was introduced.

## Known non-blocking baseline warnings

The full repository command retains existing package-vulnerability and compiler
warnings outside this scope. They did not cause failures and were not changed
as part of W12-S05.
