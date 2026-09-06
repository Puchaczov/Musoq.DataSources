# W08-S03 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope adds the bounded scalar-JSON literal subset of `search.many`, exposes
labeled `SearchMatch` rows through the Search schema, and documents ordinary SQL
aggregation for per-file any/all/none and at-least-k answers. It does not claim
regex execution, completion/partial-result options, named constructor binding or
table-valued request input.

## Findings

- No blocking correctness or ownership findings were identified.
- `PatternId` is carried on each many row and `MatchIndex` is maintained
  independently for each `(Path, PatternId)`. Equal literal text under distinct
  labels therefore remains distinct.
- The matcher is constructed before scope traversal, so unsupported regex,
  `take`, partial-result and observed-prefix requests fail before candidate
  enumeration or reader access. The scan uses the existing traversal, reader
  lifetime and coordinate path, with bounded chunk staging and cancellation
  checks.
- The many source deliberately emits occurrence rows only. Any/all/at-least-k
  use grouped SQL over distinct labels, while none uses `search.paths` plus an
  anti-join. The complete-scope test proves that a negative answer does not
  arise from an early prefix.
- Current Runtime-v2 constructor metadata is positional-only. The implementation
  and request documentation record that named SQL binding is not executable until
  reflected metadata is available; no named binding claim is made in the tests.

## Validation reviewed

- Search focused Release tests: 189 discovered, 186 passed, 3 skipped, 0 failed.
- Exact repository-wide Release tests: 21 projects, 1,473 discovered, 1,439
  passed, 34 skipped, 0 failed.
- Search request contract validator: 4 scenarios, 45 assertions, passed.
- Search match semantics validator: 6 scenarios, 49 assertions, passed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed.

## Known boundary

The broader proposed source contract contains future occurrence details beyond
the current `SearchMatch` table (for example origin, line text and context).
This scope intentionally implements and documents only the current typed
literal-occurrence surface; those fields and other execution modes remain owned
by later scopes.
