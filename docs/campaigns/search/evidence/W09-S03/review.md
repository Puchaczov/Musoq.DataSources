# W09-S03 review

Verdict: approved

Reviewer: separate grill-code review pass (no independent subagent was available)

## Scope reviewed

This scope adds an internal explicit framer for bounded multiline regex
records. Start and end delimiters are exact whole physical lines, structural
lines are excluded from the logical payload, and each emitted frame carries
its source origin, UTF-16 payload range, first physical line, byte range when
available and termination state. The existing unframed bounded mode remains
the compatibility path.

## Findings

- No blocking correctness, ownership, compatibility or maintainability
  findings were identified.
- Delimiters can cross reader-buffer boundaries without being recognized from
  delimiter-like substrings inside payload lines. Unexpected end delimiters,
  nested starts and strict missing-end EOF are reported through the typed
  `SEARCH-SYNTAX-002` diagnostic.
- The framer keeps physical line processing separate from logical record
  processing. Global source offsets and physical line numbers are restored
  when a record is scanned, including records after earlier records and
  records terminated at EOF when explicitly allowed.
- Logical payload growth is bounded by the existing record-byte limit; source
  coordinates are retained only for mapped readers, and no pooled scanner
  memory escapes into a frame or match.
- Cancellation is checked while reading, framing and matching. An
  existential sink stop propagates through the framer so a satisfied query does
  not continue scanning later records.
- The two-argument public Search source shape and the existing unframed
  bounded multiline behavior remain unchanged. Framing is an internal regex
  request/scanner seam pending a separately specified public SQL surface.

## Validation reviewed

- Framing-focused Search tests: 12 passed, 0 skipped, 0 failed.
- Search Release suite: 215 total, 212 passed, 3 skipped, 0 failed.
- Exact repository-wide Release suite: 21 projects, 1,499 total, 1,465
  passed, 34 classified skips, 0 failed.
- Search production build with warnings-as-errors: 0 warnings, 0 errors.
- `git diff --check`: passed.

## Required scenario coverage

The focused tests cover delimiters split across reader buffers, delimiter-like
payload text, explicit frame origin/ranges, strict missing terminators,
allowed unterminated EOF records, maximum-record violations, mapped byte
coordinates and early sink termination.

## Known boundary

Framing recognizes only exact whole-line start/end delimiters and ignores text
outside framed records. Injected readers do not claim physical byte identity;
the public SQL/API constructor surface and richer record fields remain outside
this scope. No package was published, pushed, released or installed.
