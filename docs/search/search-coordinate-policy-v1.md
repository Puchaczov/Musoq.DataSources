# Search text coordinate policy v1

Status: implemented for the current text sources in `W06-S02`. This policy
defines how decoded characters, physical lines and original source bytes are
related for `search.matches` and `search.lines`.

## Match coordinates

`SearchMatch` reports:

| Column | Meaning |
|---|---|
| `ByteOffset` | Nullable zero-based original-byte start of the match |
| `ByteLength` | Nullable original-byte span length |
| `LineNumber` | One-based physical line number |
| `Utf16Column` | Zero-based UTF-16 code-unit column within that line |
| `Utf16Length` | Match length in UTF-16 code units |

The byte fields are populated only when every decoded code unit in the match
has a contiguous, source-owned mapping. `Utf16Column` and `Utf16Length` are
always measured in decoded UTF-16 code units; they are never byte offsets.
All counters and offsets are signed 64-bit values and advance with checked
arithmetic.

For UTF-8, an ordinary scalar maps to its encoded byte range. The two UTF-16
code units of a supplementary scalar share one four-byte range; a match that
starts or ends in the middle of that scalar is therefore not reported as a
lossless byte span. UTF-16 maps each code unit to its two original bytes and
includes the validated BOM in the initial byte origin.

An injected `TextReader` is already-decoded input and carries no source-byte
provenance. Matches from that reader retain their line and UTF-16 coordinates
but expose null byte fields.

## Physical lines

Line numbering follows LF boundaries:

- LF terminates the current line and is included in `SearchLine.LineText`.
- CRLF is one physical newline; the CR and LF are both included in the line
  text, and the next line starts immediately after LF.
- A lone CR is ordinary line content and does not reset the line or column.
- A final line without LF is emitted at successful EOF without a terminator.

For mapped file readers, a line's `ByteOffset` is the original byte start of
its first decoded character. The first line begins at byte zero for a
no-BOM UTF-8 file, or after the validated BOM for a BOM-backed file. After an
LF, the next line starts at the exclusive byte end of that LF. If any part of
the line came from an unmapped reader, the line byte offset is null.

## Buffering boundary

The mapping is produced beside each bounded decoded reader block and is
passed to the matcher without reconstructing the input from decoded text.
Consequently CRLF, multibyte UTF-8 sequences and supplementary scalars remain
correct when a reader block ends between their decoded code units. The
acceptance tests cover emoji, combining marks, tabs, Polish text, CRLF split
across buffers, no-final-newline input and 64-bit coordinate storage.

Source authority: `S14`, `S20`, `S22`, `S24`; related contracts:
[`search-encoding-policy-v1.md`](search-encoding-policy-v1.md) and
[`search-sink-contract-v1.md`](search-sink-contract-v1.md).
