# W13-S03 review

Review method: separate grill-code review pass (no independent subagent was available).

Verdict: approved

## Scope reviewed

This scope extends the versioned raw-byte pattern with an optional bounded
before/after window and exposes clipped window coordinates, completeness and
optional bytes on each occurrence row. The review covered JSON validation and
resource limits, rolling-buffer lifetime, block-boundary behavior, overlapping
windows, BOF/EOF clipping, projection-aware materialization, row immutability,
predicate/schema/XML consistency and documentation.

## Findings

- No blocking correctness, ownership, compatibility, resource-isolation or
  documentation findings were identified.
- The window scanner retains a bounded rolling region, pauses a candidate until
  its requested after-range is available, and reuses the same bytes for the
  match and window. It never loads the file or rereads it for individual row
  fields.
- The requested interval is rooted at `ByteOffset`, includes the matched
  pattern, clips to the available file range at BOF/EOF, and reports
  `WindowComplete=false` whenever clipping occurs. Window bytes are copied at
  the public row boundary and are only materialized when projected.
- The total requested window, including the pattern, is bounded before file
  access. Output staging accounts for both matched bytes and window bytes, and
  the existing cancellation, source-observation, coordinator and disposal
  seams remain in use.
- Scalar window metadata is accepted by predicate planning and the compiled
  query/schema tests verify the public surface. Byte-array output remains a
  direct typed-row concern because Core does not permit `byte[]` as a top-level
  SQL primitive result.

## Residual boundaries

- Window requests are carried inside `patternJson`; no new SQL constructor
  overload or typed interpretation is introduced by this scope.
- A clipped window is explicitly marked incomplete rather than rejected; a
  consumer requiring the full requested range must filter on `WindowComplete`.
- Raw bytes still do not infer lines, UTF-16 coordinates or decoded text;
  interpretation remains owned by W13-S04.

## Verification

- Window/parser/source/schema focused tests: 54 passed, 0 skipped, 0 failed.
- Search Release suite: 330 total, 327 passed, 3 expected platform/reparse
  skips, 0 failed.
- Search test-project Release build with warnings-as-errors: 0 warnings,
  0 errors.
- Contract harnesses: 27 scenarios and 314 assertions, all passed.
- Owning-repository Release suite: 1,614 total, 1,580 passed, 34 classified
  skips, 0 failed, exit code 0.
- `git diff --check` and source-contract JSON parsing passed after the final
  implementation changes.
- No production process execution, package publication, push, release,
  install, sibling-repository edit or external source change was introduced.

## Known non-blocking baseline warnings

The repository-wide Release command retains existing package-vulnerability,
generated-code and compiler warnings outside this scope. They are not changed
by the bounded raw-byte window implementation.
