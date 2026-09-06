# W10-S01 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope defines the Search source predicate matrix and wires accepted scalar
predicates into the emitted-row path. The review covered source-name and alias
normalization, accepted/residual partitioning, SQL NULL behavior, numeric
literal conversion, reverse comparisons, row-shape selectors, and the
interaction with exact count rows and path traversal.

## Findings

- No blocking correctness, ownership, compatibility or maintainability
  findings were identified.
- The planner accepts only scalar row fields with comparison, literal-member
  IN/NOT IN, IS NOT NULL, and recursively supported logical forms. Unsupported
  OR expressions remain whole, while unsupported AND conjuncts remain in the
  residual predicate.
- The accepted predicate is embedded identically in the result and execution
  plan, and the source filters before row chunks are written. Match and line
  ordinals remain based on the complete scan, so filtering does not renumber
  surviving rows.
- Count predicates run after the exact per-file accumulator has completed;
  filtering cannot turn a partial count into a complete row.
- String comparisons use ordinal semantics, numeric comparisons use invariant
  conversion, and SQL UNKNOWN is not selected for ordinary NULL comparisons or
  NULL-containing NOT IN results.
- The compiled outer-join regression confirms that a source predicate does not
  change the final NULL-extension semantics; IS NULL is also retained as
  residual at the provider boundary because the provider has no join
  nullability context.

## Residual risk and test gap

- The normal planner boundary for `search.many` remains reject-all in this
  scope. Its source can consume an explicitly supplied accepted plan, but
  normal many-pattern pushdown needs a later scope with its own planning and
  join/correlation qualification.
- Accepted filtering is row-level filtering, not filesystem traversal pruning;
  it does not reduce directory enumeration or content opening for metadata
  predicates.
- The evaluator intentionally supports the scalar types present in Search
  rows. Unsupported source expression forms and incompatible non-numeric type
  pairs evaluate as residual/UNKNOWN rather than being used as a source-side
  match.

## Validation reviewed

- Predicate-planning tests: 8 passed, 0 skipped, 0 failed.
- Search Release suite: 233 total, 230 passed, 3 known platform skips, 0
  failed.
- Exact repository-wide Release suite: 21 projects, 1,517 total, 1,483
  passed, 34 classified skips, 0 failed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed before the evidence commit.

## Boundary

This scope changes only the owning repository's Search planner, row-filtering
seams, predicate tests and predicate-planning documentation, plus campaign
evidence. It does not change sibling repositories, package versions, public
constructor signatures, publication state or installation state. No package
was published, pushed, released or installed.
