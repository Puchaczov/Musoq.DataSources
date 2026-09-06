# Search occurrence, line and file sinks v1

The `matches`, `lines` and `files` sources share one bounded text scan but
publish different relational units. Projection does not change those units.

`matches` emits every non-overlapping occurrence in scan order. Its
`MatchIndex` is a zero-based ordinal within the relative path, so selecting
only `Path` deliberately repeats a path when a file contains multiple hits.

`lines` groups occurrences by physical line. It emits one row for each
matching line, keeps the decoded line text including its terminator when one
was read, and reports the number of occurrences in `OccurrenceCount`. A
physical newline is a record boundary and is not itself a searchable match.
The file-backed reader maps `ByteOffset` to the original byte position of the
line start. A line emitted through an injected already-decoded `TextReader`
has no trustworthy source-byte origin, so its `ByteOffset` remains null.

`files` is existential. It emits one row after the first qualifying
occurrence and stops reading that file. A zero-hit file produces no row, and
later occurrences cannot increase its cardinality.

`counts` is exhaustive per eligible file. It reads each file to successful EOF
and emits one row containing 64-bit occurrence and matching-line counts, the
original file byte length scanned, and `Complete = true`. It emits a zero row
for a fully processed file with no occurrence. If opening, reading or
cancellation prevents completion, no exact count row is emitted for that
file; partial work is never presented as a zero or complete count.

The content sinks receive the same `LiteralMatcher` spans from the pooled
8,192-character reader buffer. Rows are copied into owned chunks before the
bounded staging list is reused. The focused Search tests cover repeated
occurrences, multiple hits on one line, zero-hit files, projection
invariance, and the file sink's early-read termination.

The scanner delivers populated matcher output as a synchronous match batch.
The batch is borrowed only for the duration of the sink call and may be reused
afterward; a consumer that crosses an asynchronous or native boundary must
copy the spans before returning. The built-in sinks consume the batch
synchronously and materialize rows into owned writer chunks. This repository
does not claim a native Search backend; the batch seam is the measured,
ownership-explicit integration point for a future candidate.

Occurrence rows additionally expose nullable `ByteOffset` and `ByteLength`
when the strict source decoder can prove a lossless original-byte span, plus
`Utf16Length` for the matched UTF-16 code-unit span. These coordinates are
source offsets, not offsets in a re-encoded copy of the decoded text.

When a context projection is requested with a non-zero internal context
window, each occurrence row also carries `Context`. It is an immutable,
bounded collection of adjacent physical lines ordered from oldest before-line
to newest after-line. `RelativeLine` is negative before the match and positive
after it; zero and the matching line itself are never included. Repeated
occurrences on one line retain separate match rows and may have equal context
collections. Context truncation affects only retained `LineText`, never match
cardinality, line numbers or occurrence coordinates.

Internally, pending occurrences reference shared immutable line storage. The
storage is materialized into separate immutable `SearchContextLine` rows only
when an occurrence is emitted, so multiple matches on one line do not retain
duplicate line-text objects and no pooled scanner buffer escapes. A retained
row may request another bounded context window through the internal evidence
seam; source identity is checked before and after that read, and stale source
versions fail instead of returning unrelated text.
