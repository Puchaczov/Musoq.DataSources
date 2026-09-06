# W09-S04 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope adds executable Search-to-text-interpretation composition coverage
and the corresponding cookbook. The tested shape carries Search rows through
an intermediate CTE, then applies `Parse`, `TryParse`, or `PartialParse` once
per candidate line while retaining path, origin, physical line number and
occurrence count.

## Findings

- No blocking correctness, ownership, compatibility or maintainability
  findings were identified.
- Strict `Parse` failure is materialized and asserted as a typed parse failure;
  `OUTER APPLY TryParse` retains malformed candidates with null interpreted
  fields; `PartialParse` retains the candidate and exposes the failed field,
  message and consumed-byte/character count.
- The `ERROR` literal is explicitly documented as candidate-only. The valid
  `INFO` negative control prevents an empty prefiltered result from being
  mistaken for proof that no valid record exists.
- The CTE boundary is deliberate. The packaged evaluator revision can hang on
  the unwrapped source-field-plus-parser projection shape, so that shape is
  not advertised as a supported cookbook and no sibling engine source was
  edited.

## Residual risk and test gap

The local Search line sink currently supplies a null `Origin`; the tests prove
that this source identity column survives the composition boundary, but a
non-null virtual-source origin needs a future adapter-specific fixture. That
does not alter the current local-source contract or block this scope.

## Validation reviewed

- Composition-focused tests: 5 passed, 0 skipped, 0 failed.
- Search Release suite: 220 total, 217 passed, 3 expected skips, 0 failed.
- Exact repository-wide Release suite: 21 projects, 1,504 total, 1,470
  passed, 34 classified skips, 0 failed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed before evidence commit.

## Boundary

This scope changes only the owning repository's Search tests and composition
documentation, plus its campaign evidence. It does not change the public
Search schema, the evaluator, sibling repositories, package versions,
publication state or installation state. No package was published, pushed,
released or installed.
