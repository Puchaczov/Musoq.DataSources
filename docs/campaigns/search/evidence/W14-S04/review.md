# W14-S04 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds a line-oriented log Search-to-parse recipe and its executable
contract tests. The review challenged candidate cardinality, repeated-token
handling, tolerant malformed-record preservation, mandatory-token coverage,
UTF-16 coordinates, and fixture cleanup.

## Findings

- No blocking correctness, ownership, or maintainability findings were
  identified.
- The CTE preserves one row per matching physical line and applies
  `TryParse<LogRecord>` once. `OccurrenceCount` is asserted separately from
  parsed-row cardinality, preventing duplicate parsing or counting caused by
  repeated tokens on a line.
- `OUTER APPLY` keeps the malformed candidate visible, retains its raw
  `LineText`, and leaves required parsed fields null. The test does not turn a
  null tolerant result into a successful record.
- Successful IDs and occurrence totals are exact: two parsed IDs, three
  candidate lines, five matching occurrences, and one malformed candidate.
- The INFO fixture proves that a missing `ERROR` prefilter token is outside
  the candidate result; the recipe does not make an unsupported negative
  completeness claim.
- The separate `search.matches` assertion verifies zero-based UTF-16 columns
  for a line containing both accented text and an emoji.
- Each fixture uses an isolated temporary root and deletes it in `finally`.

## Residual boundaries

- The literal Search source is a candidate prefilter. Complete absence of a
  record requires an independently complete scope, not an empty candidate
  result.
- `TryParse` null fields preserve tolerant failure evidence, but this recipe
  intentionally does not expose `PartialParse`'s evaluator-side dictionary;
  the packaged evaluator rejects that dictionary as a query output type.
- The recipe parses line-oriented records and makes no claim about a formal
  log language, semantic validation, or runtime configuration correctness.

## Verification

- Log Search-to-parse focused suite: 2 passed, 0 skipped, 0 failed.
- The test covers repeated tokens, exact IDs and counts, malformed raw-line
  retention, missing candidate-token semantics, and multibyte coordinates.
- `git diff --check` is required before the scope commit.
