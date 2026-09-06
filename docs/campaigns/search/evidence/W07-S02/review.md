# W07-S02 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- Regex requests use the internal `Regex` pattern mode while the existing
  public two-argument constructors remain literal. The request validates and
  compiles the regex before scope enumeration, so malformed or unsupported
  patterns cannot cause a root traversal or source open.
- `SearchRegexScanner` accumulates one complete physical record across reader
  blocks and evaluates it once. It does not use a fixed overlap or expose
  reader-block boundaries to `^`, `$`, greedy alternatives or zero-width
  progression. LF is the record delimiter, CRLF is normalized for regex input,
  and a lone CR remains content.
- Record growth is checked before appending non-delimiter characters and is
  capped at 1,048,576 UTF-16 characters. The compiled regex keeps the backend's
  one-second match timeout; timeout and overlong-record failures remain typed
  resource diagnostics.
- Match spans retain absolute UTF-16 positions, physical line numbers,
  optional matched text and lossless original-byte ranges. Zero-width matches
  receive zero length and mapped boundary offsets when the decoder can prove
  them. Line sinks complete a matching physical record once after all its
  matches.
- Whole-word filtering uses the declared Unicode scalar policy, including
  supplementary scalars and combining marks. Match text is materialized only
  when the occurrence projection requests it; literal sinks retain their
  existing fallback behavior.
- Focused tests cover anchors, source-order and greedy alternatives,
  cross-record isolation, zero-width progress, irregular reader blocks, line
  grouping, Unicode whole-word boundaries, UTF-8 coordinates, empty input and
  the maximum record bound. The full owning-repository suite is also green.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource or
scope-boundary finding remains open.

The scanner intentionally materializes one bounded record because arbitrary
regex cannot be made correct with a guessed fixed overlap across input blocks.
The record bound and backend timeout make that choice explicit and bounded;
future capture projection remains the separate W07-S03 scope.

## Boundary

Only the owning Search source, tests, Search documentation and W07-S02
evidence were changed. No sibling repository, release, publication,
installation or public SQL/XML surface was changed.
