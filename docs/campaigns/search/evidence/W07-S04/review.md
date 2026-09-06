# W07-S04 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- `BoundedMultiline` is an explicit internal record mode. The default
  physical-line regex path and the public two-argument literal constructors
  remain unchanged; no silent mode or dialect switch was introduced.
- The multiline scanner accumulates one complete file record across all reader
  blocks and evaluates it only after EOF. LF is record content in this mode,
  so patterns can match across physical lines without a guessed overlap or
  truncated regex input.
- The record is rejected before regex execution when it exceeds 1,048,576
  original bytes for mapped files. Already-decoded readers use the selected
  encoding's byte counter, and invalid limits are rejected. Overflow is a
  typed `recordBytes` resource diagnostic with an actionable remedy.
- Match rows retain leftmost regex selection, match-start physical line and
  UTF-16 column, lossless byte spans when available, projection-aware text and
  the W07-S03 typed capture collection. Matching-file, count and line sinks
  preserve their existing row units; line completion is performed after the
  bounded record has been processed.
- Empty files produce no record, nonempty unterminated EOF records are
  evaluated, and zero-width end-boundary results are emitted once by the
  existing .NET regex enumeration. Cancellation is checked around reads and
  match processing; reader disposal and typed error translation remain intact.
- Focused tests cover cross-buffer multiline matching, byte-limit rejection,
  unterminated EOF records and a zero-width final-boundary match. The exact
  Release repository suite is also green.

## Findings and disposition

No material correctness, semantic, diagnostic, compatibility, resource or
scope-boundary finding remains open.

The mode deliberately uses a bounded whole-file record rather than claiming
unbounded streaming regex support. Its fixed safety ceiling and explicit
request mode make the memory and newline semantics inspectable; future record
framing or larger/unbounded modes require a separate contract.

## Boundary

Only the owning Search source, tests, Search documentation and W07-S04
evidence were changed. No sibling repository, release, publication,
installation or unrelated campaign state was staged.
