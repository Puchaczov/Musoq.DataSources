# Search projection policy v1

The Search source keeps its relational result unit independent of SQL
projection. Selecting fewer columns may reduce detail work, but it must not
change occurrence multiplicity, file identity, line numbers, UTF-16
coordinates, original-byte coordinates, or count values.

## Optional decoded text

Runtime V2 supplies the source with the requested source columns through
`SourceExecutionContext.AllColumns`. The text sources use that set as follows:

- `SearchMatch.MatchText` is retained only when the projection requests
  `MatchText`.
- `SearchLine.LineText` is retained only when the projection requests
  `LineText`.
- `SearchMatch.Captures` is materialized only when the projection requests
  `Captures`; literal rows always expose an empty collection.
- `SearchMatch.Context` is materialized only when the projection requests
  `Context` and the internal request has a non-zero bounded context window;
  otherwise it is an empty collection.
- an empty `AllColumns` set retains all optional values for compatibility with
  direct source callers that do not provide projection metadata.

Context is physical-line evidence, not another result unit. The occurrence
sink buffers only the bounded before/after window needed to complete a match,
then emits exactly one `SearchMatch` row per occurrence. Context rows carry a
non-zero signed `RelativeLine`, an exact one-based `LineNumber`, and decoded
`LineText` when retained. The matching line is not repeated in `Context`.

The internal context request uses a total before/after line limit and a UTF-8
byte budget per emitted window. The budget is divided conservatively across
the requested context lines; a long line is prefix-truncated or has null text
when its share is exhausted. Line identity and occurrence counting continue
even when context text is truncated. The scanner also bounds its temporary
line builder to that same per-line share.

The source still decodes and scans the complete eligible file. Match spans,
line boundaries, strict decoder validation, binary classification, and exact
count accounting are never skipped merely because optional text is omitted.
For a compact `SearchLine` row, the scanner tracks LF boundaries and matching
line completion without allocating a line `StringBuilder`.

The matches schema has a typed `Captures` column. Regex rows materialize
capture objects only when that column is requested (or when direct callers
provide no projection metadata), keeping compact scans free from capture
allocations.

## Verification

`SearchProjectionTests` compares compact and text-retaining direct sources and
compiled queries over the same files. The tests assert identical row counts,
occurrence multiplicity, physical line numbers, and coordinates, while also
asserting that the requested optional text is present only in the retaining
projection. `SearchContextTests` additionally compares context-enabled and
compact occurrence identities, overlapping windows, BOF/EOF, CRLF split across
reader blocks, long-line byte budgets, and the absent-context projection.

`SearchDecodingBenchmarkTests` verifies the complete result set separately for
ASCII, UTF-8, and UTF-16 little-endian-with-BOM fixtures. The executable's
`measure-decoding` command performs one warmup and seven measured trials per
cohort. It is a diagnostic observation of the standalone managed benchmark
runner; filesystem cache state is unknown, and it makes no cross-backend or
superiority claim about production Search.
