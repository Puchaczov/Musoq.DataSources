# W13-S04 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope composes `search.bytes` candidate rows with the established binary
interpretation APPLY forms. The review covered candidate-versus-structure
separation, strict validation, `TryInterpret` candidate retention, nested
over-read evidence, bounded-window completeness, query-shape compatibility,
tests, and documentation consistency.

## Findings

- No blocking correctness, ownership, resource-isolation, compatibility, or
  documentation findings were identified.
- The candidate row remains the source of path, match coordinates, window
  coordinates, and completeness. Interpretation contributes parsed fields or
  parse evidence without replacing the locating evidence.
- `Interpret` is exercised on a complete bounded record and proves that a
  length-driven payload is consumed before the trailing constant is parsed.
- `OUTER APPLY TryInterpret` keeps false magic and invalid-length candidates in
  the result while leaving invalid interpreted fields null.
- `PartialInterpret` preserves a qualified nested error field, diagnostic
  message, and bytes consumed for a truncated nested record.
- The tests use the direct Search source row as the APPLY argument. This is an
  intentional evaluator boundary: `byte[]` cannot be projected through a CTE
  or emitted as a top-level query output column, but is valid as an
  interpretation APPLY argument. The documentation records this constraint.

## Residual boundaries

- The composition does not add a new Search constructor or alter the bounded
  scanner implemented by W13-S03.
- A complete-looking signature is only a candidate until interpretation
  validation succeeds; a clipped window remains explicitly incomplete.
- Consumers that need invalid candidates must use `OUTER APPLY
  TryInterpret`; strict `Interpret` intentionally fails the query on invalid
  input.
- No sibling repository, package, publication, or external process state was
  changed.

## Verification

- Focused interpretation-composition tests: 3 passed, 0 skipped, 0 failed.
- Search Release suite: 333 total, 330 passed, 3 classified platform/reparse
  skips, 0 failed.
- Search test-project Release build with warnings-as-errors: 0 warnings,
  0 errors.
- Contract harnesses: 27 scenarios and 314 assertions, all passed.
- Owning-repository Release suite: 1,617 total, 1,583 passed, 34 classified
  skips, 0 failed, exit code 0.
- `git diff --check` passed after the implementation and documentation
  changes.

## Known non-blocking baseline warnings

The repository-wide Release command retains existing package-vulnerability,
generated-code, and compiler warnings outside this scope. They do not change
the composition behavior or its focused qualification.
