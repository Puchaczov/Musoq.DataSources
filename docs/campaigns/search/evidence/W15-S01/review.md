# W15-S01 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope adds the Search capability card and a snapshot test that checks the
card against generated static XML examples and runtime `SearchSchema`
constructors/descriptions.

## Findings

- No blocking correctness, ownership, or maintainability findings were
  identified.
- The card is bounded to the eight sources actually registered by
  `SearchSchema`: paths, files, lines, matches, many, counts, bytes and
  audit. It gives each source one decision use, result unit, cost/completeness
  boundary and optional-use condition rather than dumping all columns.
- The card distinguishes candidate scans from complete totals and terminal
  audit evidence. It does not turn empty match/file/line output into a
  universal negative claim.
- The request and dialect notes preserve the implemented boundary: scalar
  JSON for `many`/`bytes`, literal-only `many`, explicit byte windows, and
  separate strict/tolerant text interpretation.
- The snapshot test reads the generated Search XML, asserts the exact eight
  virtual-constructor shapes, compares runtime constructor order, and obtains
  a non-empty runtime descriptor for every source. It also checks the card's
  cost, optional and completeness markers.

## Residual boundaries

- The card is a decision guide, not a substitute for the versioned source,
  request, coordinate, resource and completion contracts linked from it.
- Runtime descriptions provide source columns and signatures; they do not
  prove that a particular deployment has installed the Search plugin or that
  an external root is readable.
- Scan cost and completeness remain dependent on scope, encoding, policy and
  resource settings supplied by the query.

## Verification

- Capability-card snapshot: 1 passed, 0 skipped, 0 failed.
- Full Search suite: 340 passed, 3 expected platform skips, 0 failed, 343
  total.
- Search build with `-warnaserror`: 0 warnings, 0 errors.
- All five registered Search contract harnesses passed: 27 scenarios and 314
  assertions.
- Owning-repository Release suite: 1,631 total, 1,597 passed, 0 failed and 34
  expected skips across 21 projects; the final filtered rerun exited 0.
- `git diff --check` passed before the scope commit.
