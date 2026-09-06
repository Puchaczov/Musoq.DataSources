# W14-S03 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds migration and configuration comparison recipes over labeled
`search.many` candidates, `search.paths` manifest resolution and the
`search.audit` completeness boundary. The review challenged comment and
string-literal handling, duplicate occurrences, anti-join cardinality,
proven-path set difference, missing manifest files and incomplete scans.

## Findings

- No blocking correctness, ownership, or maintainability findings were
  identified.
- Deprecated and replacement rows retain pattern identity, occurrence index,
  coordinates and match text. API-shaped comments and quoted strings remain
  lexical candidates but are excluded from the proven code path sets.
- The SQL anti-join is explicitly a lexical candidate result. The recipe does
  not treat a path with no replacement row as proven-unmigrated until both
  sides have been filtered to accepted code evidence.
- Set difference occurs after occurrence counting. Duplicate deprecated calls
  remain separate findings while the final path relation is deduplicated only
  at the intentional set boundary.
- Configuration status is derived from an explicit bounded manifest and the
  resolved `search.paths` set. A duplicate key retains its count, a
  comment-only key remains an unproven candidate, and an absent manifest path
  is distinct from an eligible file with a missing key.
- Negative conclusions are gated by `Complete`, `ScopeExhausted` and
  `CountsExact`. The missing-root audit fixture proves that an incomplete scan
  cannot authorize a no-key conclusion.
- Each fixture uses an isolated temporary root and cleans it in `finally`.

## Residual boundaries

- The line classifier is deliberately lexical and is not a programming-
  language or configuration parser. A `code-mention` or `configuration-key`
  is recipe-qualified evidence, not symbol or runtime proof.
- A complete audit is required before applying missing-key or missing-file
  status as a scope conclusion; partial or failed scans must remain typed
  incomplete evidence.
- Configuration fixtures are text fixtures. Their duplicate JSON keys are
  intentional lexical test data and are not a claim that duplicate keys are
  accepted by a downstream JSON parser.

## Verification

- Migration/configuration recipe focused suite: 3 passed, 0 skipped, 0
  failed.
- The tests cover comments, string literals, duplicate migration/configuration
  occurrences, anti-join candidate paths, proven set difference, missing
  manifest files and incomplete audits.
- `git diff --check` passed for the implementation and documentation changes.
