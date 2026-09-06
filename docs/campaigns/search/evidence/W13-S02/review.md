# W13-S02 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds the streaming raw-byte scanner, the public `search.bytes`
schema/source contract, immutable byte-occurrence rows, projection-aware
matched-byte retention, and independent boundary tests. The review covered
masked comparison semantics, leftmost non-overlapping selection, carry state
across read blocks, file traversal/coordinator integration, mutation guards,
resource staging, cancellation/disposal, schema metadata and contract docs.

## Findings

- No blocking correctness, ownership, compatibility, resource-isolation or
  documentation findings were identified.
- The scanner retains at most `pattern.Length - 1` bytes between blocks and
  evaluates candidate starts in increasing absolute-byte order. It emits a
  match only when `(value & mask) == (pattern & mask)`, then advances the
  next eligible start by the pattern length, preserving the declared
  leftmost, non-overlapping semantics without decoding text.
- Source rows reset `MatchIndex` per path, preserve exact `ByteOffset` and
  `ByteLength`, leave text/Unicode coordinates null, and expose empty,
  immutable capture/context collections. Requested `MatchedBytes` are copied
  on ingress and on access so scanner buffers cannot escape into a row.
- File processing reuses the existing scope traversal, parallel coordinator,
  observed-source guard, cancellation token, resource budget and bounded
  output-channel lifecycle. Staged rows are released after each channel write
  and all file handles are owned by `using` scopes.
- Constructor metadata, XML, schema columns, predicate pushdown and the
  machine-readable/human-readable source documents agree. The compiled query
  fixture verifies the scalar raw-coordinate surface; direct row tests verify
  byte materialization that Core does not permit as a top-level primitive
  query result.

## Residual boundaries

- The two-argument public source uses the established default scope and
  resource limits. Bounded byte windows and typed interpretation remain
  owned by W13-S03 and W13-S04.
- An all-zero mask is intentionally a valid high-cardinality wildcard. The
  scanner remains streaming and output staging is bounded by the existing
  coordinator/channel seam; this scope does not claim a new semantic match
  cap or a whole-file snapshot.
- Raw byte matches intentionally do not infer lines, UTF-16 coordinates or
  decoded text. Any such interpretation must be added as a separate,
  explicitly typed later contract.

## Verification

- Byte source and schema focused tests: 49 passed, 0 skipped, 0 failed.
- Search Release suite: 325 total, 322 passed, 3 expected platform/reparse
  skips, 0 failed.
- Search test-project Release build with warnings-as-errors: 0 warnings,
  0 errors.
- Contract harnesses: 27 scenarios and 314 assertions, all passed.
- The exact owning-repository Release suite completed with 1,609 discovered
  tests, 1,575 passed, 34 expected skips and 0 failures; the no-build receipt
  rerun also exited 0.
- `git diff --check` and source-contract JSON parsing passed after the
  evidence/report files were finalized.
- No production process execution, package publication, push, release,
  install, sibling-repository edit or external source change was introduced.

## Known non-blocking baseline warnings

The repository-wide Release command retains existing package-vulnerability,
generated-code and compiler warnings outside this scope. They are not changed
by the raw-byte implementation.
