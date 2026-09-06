# Search log search-to-parse recipe v1

Status: implemented and verified for `W14-S04`.

This recipe composes a literal Search candidate source with a tolerant text
parser for line-oriented records. It is a candidate-to-record recipe, not a
language parser or a claim that every valid log record contains the search
literal.

## Candidate boundary and one-pass parsing

`search.lines(root, literal)` emits one row per matching physical line. Its
`OccurrenceCount` records how many matches were found in that line; it is
not a request to expand the line into repeated input rows. Keep the Search
row in a CTE and apply the parser once to `LineText`:

```sql
text LogRecord {
    EventId: until ' ',
    Timestamp: until ' ',
    Level: until ':',
    Separator: literal ' ',
    Message: rest trim
};

with candidates as (
    select line.Path, line.Origin, line.LineNumber,
        line.OccurrenceCount, line.LineText
    from search.lines('/var/log', 'ERROR') line
)
select candidates.Path, candidates.Origin, candidates.LineNumber,
    candidates.OccurrenceCount, candidates.LineText,
    log.EventId, log.Timestamp, log.Level, log.Message
from candidates
outer apply TryParse<LogRecord>(candidates.LineText) log
```

The CTE makes the candidate boundary, provenance and physical coordinates
explicit. `OccurrenceCount` stays attached to the physical line, so a line
containing two `ERROR` tokens is parsed once and contributes two search
occurrences rather than two parsed records.

## Tolerant failures and aggregation

Use `OUTER APPLY TryParse` when malformed candidate lines must remain in the
audit. Successful primitive fields are projected beside `Path`,
`LineNumber`, `OccurrenceCount` and the raw `LineText`. Derive a status such
as `parsed`/`malformed` from the required parsed field, and retain the raw
line for review. A malformed candidate therefore remains evidence even when
its parsed fields are null.

Aggregate successful identifiers only after preserving the line-level
records, for example by grouping non-null `EventId` values. Aggregate
malformed candidates separately by count or line coordinate. Never multiply
the parsed rows by `OccurrenceCount` unless a downstream report explicitly
needs token-level output and makes that expansion visible.

`Parse` is the strict alternative when any malformed candidate should fail
the query. `PartialParse` is the diagnostic alternative when evaluator-side
error fields are needed; its dictionary of parsed fields is not a valid
query-output column in this packaged evaluator. The tested recipe therefore
uses `TryParse`'s primitive fields and retains raw input as the portable
failure evidence.

## Completeness and coordinates

The literal passed to `search.lines` is a candidate prefilter. The fixture's
`INFO` line has no `ERROR` token and is consequently absent from the result;
that absence is not a proof that no valid non-ERROR record exists. A missing
mandatory token must be reported as an uncovered candidate boundary unless a
separate complete scan establishes the relevant universe.

When a report also needs match positions, use `search.matches` and retain its
UTF-16 coordinates. In the multibyte fixture, the two `ERROR` matches after
`café 🚀` retain their exact zero-based UTF-16 columns and five-code-unit
lengths. Those columns are decoded UTF-16 positions, not byte offsets; the
coordinate policy remains authoritative for byte provenance and line rules.

The recipe test covers repeated tokens on one line, exact successful IDs and
occurrence totals, a malformed record with its raw line retained, the
missing-prefilter-token boundary, and multibyte UTF-16 coordinates.

Source authority: `S14`, `S15`, `S16`; related contracts:
[`search-interpretation-composition-v1.md`](search-interpretation-composition-v1.md),
[`search-source-contract-v1.md`](search-source-contract-v1.md), and
[`search-coordinate-policy-v1.md`](search-coordinate-policy-v1.md).
