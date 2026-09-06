# W14-S01 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds a labeled `search.many` diagnostic traceability recipe and an
executable test fixture. The review challenged the result-field contract,
pattern-label parsing, path provenance, lexical-versus-semantic claims,
comment handling, missing-evidence behavior, SQL/JSON escaping and temporary
fixture cleanup.

## Findings

- No blocking correctness, ownership, or maintainability findings were
  identified.
- The query projects only fields exposed by the current `search.many` schema:
  path, pattern identity, line, UTF-16 column and match text. The recipe does
  not claim that Search exposes an `Origin` column.
- `Origin` is explicitly derived from a caller-owned `src/`, `tests/` and
  `docs/` path manifest. That boundary is stated in the versioned recipe and
  paths outside the manifest remain `other` rather than being silently
  assigned provenance.
- Pattern labels supply the category and diagnostic identifier. The test
  asserts non-empty `Code`, `Category`, `Origin` and `Evidence` for every
  finding rather than stopping at query compilation.
- Comment-only declaration text remains a lexical comment and is excluded
  from proven declaration evidence. The fixture proves a complete `1001`
  relation while keeping the missing declaration, test and docs for `1002`
  explicit.
- Proven coverage requires code declaration, code emission, test and
  documentation evidence. The recipe documents that these are lexical
  relations, not syntax-tree, symbol-resolution, control-flow or runtime
  execution proof.
- The test escapes both the repository root and JSON request when embedding
  them in a SQL string, and disposes its unique temporary fixture in `finally`.

## Residual boundaries

- A complete negative conclusion requires a complete path manifest and
  successful Search coverage; an empty or partial lexical result is not proof
  that evidence is absent.
- The recipe does not infer language structure from `MatchText` or line text.
- Proven coverage remains a review relation assembled by the recipe; it is not
  a new Search source column or a guarantee that a named test exercises the
  intended runtime behavior.

## Verification

- Traceability recipe test: 1 passed, 0 skipped, 0 failed.
- The focused test asserts findings, the comment false-positive boundary, the
  complete `1001` relation and the incomplete `1002` relation.
- `git diff --check` passed for the implementation and documentation changes.
