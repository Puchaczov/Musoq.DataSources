# W09-S01 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope adds a typed immutable `SearchMatch.Context` collection and an
internal bounded before/after context execution seam. Context is activated only
when the collection is projected and the request supplies a non-zero window.
The occurrence sink retains pending matches until their after window is
complete, then emits the original occurrence rows in scan order. The scanner
uses bounded line text and physical-line completion so context does not add
match rows or change match coordinates.

## Findings

- No blocking correctness, ownership or maintainability findings were
  identified.
- Before lines are retained in a bounded FIFO ring; after lines are attached
  until the requested distance is reached, with an EOF flush for short windows.
- The matching line is excluded from `Context`; each context row carries a
  non-zero signed relative line and exact one-based line number. Repeated hits
  on one line remain separate occurrence rows with equal context values.
- UTF-8 text budgeting is conservative: the configured window budget is
  divided across requested context lines, long text is prefix-truncated or
  null, and line identity remains available. Context is copied into immutable
  query-visible rows before the sink can reuse its staging state.
- Existing two-argument SQL constructors and the many-pattern request shape
  remain unchanged. The internal request seam is documented as such; no
  unsupported public option or host route was advertised.

## Validation reviewed

- Context-focused tests: 6 passed, 0 skipped, 0 failed.
- Search focused Release suite: 205 total, 202 passed, 3 platform-conditional
  skips, 0 failed.
- Exact repository-wide Release suite: 21 projects, 1,489 total, 1,455
  passed, 34 classified skips, 0 failed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- Contract JSON parsing and `git diff --check` passed.

## Known boundary

The current public two-argument SQL request keeps context disabled; enabling a
non-zero window is intentionally an internal execution seam for this scope.
`search.many` exposes the typed schema column but its ratified scalar request
has no context option and therefore emits an empty collection. Shared line
storage and any stale-source expansion protocol remain owned by W09-S02.

No package was published, pushed, released or installed.
