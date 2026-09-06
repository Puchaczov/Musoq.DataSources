# W14-S02 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds a bounded lexical proximity recipe over labeled
`search.many` occurrences and an executable fixture. The review challenged
same-file identity, forward span ordering, line and same-line bounds,
all-pairs cardinality, nearest-right determinism, absent partners, repeated
same-line matches, SQL projection fidelity and temporary fixture cleanup.

## Findings

- No blocking correctness, ownership, or maintainability findings were
  identified.
- Pairing requires equal repository-relative `Path`; a left occurrence in one
  file cannot pair with a right occurrence in another file.
- Same-line pairs require the right span to begin at or after the left span's
  end. Later-line pairs use an inclusive `MaxFollowingLines` bound, while
  same-line pairs use an inclusive `MaxSameLineGap` bound.
- All-pairs mode preserves every eligible repeated right occurrence in
  deterministic path, line, UTF-16-column and match-index order. It fails
  closed when `MaxPairsPerLeft` would be exceeded instead of returning a
  partial relation.
- Nearest-right mode chooses the first ordered candidate and emits at most one
  pair per left. It does not reinterpret lexical order as semantic distance.
- The fixture independently asserts six expected same-file pairs, two
  same-line right matches, line-window exclusion, cross-file exclusion and a
  right-only file with no fabricated partner. Invalid windows and the
  all-pairs cap are also asserted.
- The query projects only the current many-source fields required for the
  relation, and each test disposes its unique temporary fixture in `finally`.

## Residual boundaries

- The result is lexical proximity only; it does not establish declaration,
  call, assertion, documentation, symbol or control-flow relationships.
- The cap is a fail-fast completeness boundary, not a permission to silently
  truncate output. Callers must choose a cap appropriate to their scope.
- A later-line pair carries no same-line gap; byte offsets are not inferred
  from UTF-16 coordinates.

## Verification

- Proximity recipe focused suite: 2 passed, 0 skipped, 0 failed.
- The focused tests exercise multiple right matches per left, same-line ties,
  file boundaries, absent partners, nearest selection and invalid/capped
  windows.
- `git diff --check` passed for the implementation and documentation changes.
