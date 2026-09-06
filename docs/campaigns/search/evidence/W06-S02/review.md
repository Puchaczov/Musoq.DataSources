# W06-S02 review

## Verdict

Approved for completion.

Reviewer: separate `grill-code` review pass (no independent subagent was available).

## Scope checked

- File-backed text readers retain a bounded, source-owned coordinate map while
  decoding; UTF-8 byte lengths, UTF-16 code-unit positions and 64-bit offsets
  are kept distinct.
- UTF-8 supplementary scalars are represented by a shared four-byte range and
  cannot be reported as a lossless byte span when a literal starts or ends on
  only one surrogate code unit. UTF-16 code units map to their original
  two-byte units.
- The matcher preserves byte mappings through direct fast-path matches,
  KMP/ring-buffer matches and reader-block boundaries without reconstructing
  source bytes from decoded text.
- Occurrence rows expose nullable `ByteOffset`/`ByteLength` and `Utf16Length`,
  and the schema helper, static XML and compiled `SELECT *` contract agree on
  their order and types.
- Line rows use LF as the only physical boundary. CRLF is retained in the
  completed line and advances the next line after LF; lone CR remains content;
  EOF without LF completes the final line. Line byte offsets are derived from
  the mapped LF end or remain null for injected decoded readers.
- BOM origins, multibyte UTF-8 text, combining marks, tabs, Polish text,
  CRLF split at the bounded reader block, no-final-newline input and large
  64-bit values are covered by focused tests.
- Existing two-argument SQL constructors and the prior request/encoding
  boundary remain intact; no sibling repository, release, publication or
  binary-search surface was changed.

## Findings and disposition

No material correctness, ownership, diagnostic, compatibility, resource,
performance or scope-boundary finding required a follow-up change.

## Boundary

The byte mapping is intentionally produced for the current strict file-backed
text reader. An injected `TextReader` is already-decoded input and therefore
does not receive fabricated original-byte coordinates. Optional-column cost
elision remains the later W06-S05 responsibility.
