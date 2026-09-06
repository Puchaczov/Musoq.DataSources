# W09-S02 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope shares immutable physical-line storage among pending occurrence
rows and adds a bounded internal context-expansion seam. The seam retains a
content-hashed source identity, uses strict decoding with a rented scanner
buffer whose contents never escape, and validates the source before and after
an expansion read. The public Search constructor signatures and relational row
units remain unchanged.

## Findings

- No blocking correctness, ownership, compatibility or maintainability
  findings were identified.
- Pending matches hold references to one immutable line record per completed
  physical line; relative offsets remain per-occurrence values, so repeated
  matches share line text without sharing mutable query-visible collections.
- Context rows are materialized into owned immutable `SearchContextLine`
  objects before they enter an output chunk. The rented scanner and hash
  buffers are disposed/returned before the row can be retained by downstream
  grouping or joining.
- Expansion is bounded by the existing context line and UTF-8 byte limits and
  streams only through a capped line builder. It rejects missing, replaced,
  unreadable or content-changed sources rather than reading a newer file for
  an older match.
- Cancellation is checked around source hashing and expansion reads; the
  normal Search fast path does not create evidence state when Context is not
  projected or context is disabled.

## Validation reviewed

- Context-focused tests: 8 passed, 0 skipped, 0 failed.
- Search Release suite: 207 total, 204 passed, 3 platform-conditional
  skips, 0 failed.
- Exact repository-wide Release suite: 21 projects, 1,491 total, 1,457
  passed, 34 classified skips, 0 failed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed.

## Known boundary

The expansion seam is internal and intentionally does not add a new SQL
constructor argument or claim an atomic snapshot for a live filesystem. It is
available only for rows backed by the default file reader; injected readers
retain materialized context but do not receive a false file identity. Full
mutation fault-injection coverage remains in the later W12-S05 scope.
