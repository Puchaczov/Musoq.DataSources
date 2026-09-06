# Search interpretation composition v1

Status: implemented and verified for `W09-S04`.

`search.lines(root, literal)` is a candidate source for text interpretation.
It emits one row per matching physical line. The row's `Path`, optional
`Origin`, `LineNumber`, `OccurrenceCount`, and retained `LineText` remain
source evidence and should be selected beside the interpreted value.

## Tested composition shape

Keep the Search row in an intermediate CTE, then apply the text interpreter to
that row. This is the verified packaged-evaluator binding shape for this
repository revision:

```sql
text LogEntry {
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
    candidates.OccurrenceCount, log.Timestamp, log.Level, log.Message
from candidates
cross apply Parse<LogEntry>(candidates.LineText) log
```

The CTE makes the candidate boundary and provenance explicit before
`CROSS APPLY`. Parse is evaluated once for each candidate line; an
`OccurrenceCount` greater than one does not create additional parsed rows.
`LineText` includes its physical LF/CRLF terminator when one is available;
`rest trim` removes that terminator in this example. Use `rest` instead when
the exact trailing text is part of the record contract.

The direct shape that combines source-field projection and a parser argument
in one source query is not part of the verified contract for this packaged
evaluator revision. The CTE shape above is the supported cookbook form until
that evaluator binding boundary is independently corrected and qualified.

## Parse failure choices

Use the interpretation operation that matches the desired evidence policy:

```sql
-- Strict: a malformed candidate fails the query.
cross apply Parse<LogEntry>(candidates.LineText) log

-- Tolerant: preserve the candidate and expose NULL interpreted fields.
outer apply TryParse<LogEntry>(candidates.LineText) log

-- Diagnostic: preserve parsed fields and expose the failed field/message.
cross apply PartialParse<LogEntry>(candidates.LineText) parsed
```

`TryParse` must be paired with `OUTER APPLY` when malformed candidates must
remain visible. `PartialParse` exposes `ErrorField`, `ErrorMessage`, and
`BytesConsumed`; neither operation silently turns a parse failure into a
successful record. Keep `Path`, `Origin`, and coordinates in every form so a
failure can still be located.

## Raw-byte windows and binary interpretation

`search.bytes` can locate a candidate signature and provide a bounded
`WindowBytes` value for a binary interpretation schema. The candidate row and
the interpreted structure are separate evidence: retain the Search path and
byte coordinates beside the interpreted fields.

The verified shape uses the Search row directly. A `byte[]` value cannot be
projected through a CTE or emitted as a query output column in this evaluator;
it is valid as the right-hand-side argument of an interpretation `APPLY`.

```sql
binary BoundedRecord {
    Magic: byte[2] magic [0xCA, 0xFE],
    Length: byte check Length >= 1 and Length <= 4,
    Payload: byte[Length],
    Trailer: byte const 0x7F
};

select candidate.Path, candidate.ByteOffset,
    candidate.WindowStartByteOffset, candidate.WindowByteLength,
    candidate.WindowComplete, record.Length, record.Trailer
from search.bytes('/var/data',
    '{"version":1,"bytes":"ca ??","window":{"beforeBytes":0,"afterBytes":4}}') candidate
cross apply Interpret<BoundedRecord>(candidate.WindowBytes) record
```

The wildcard signature intentionally produces both true and false
signatures. `Interpret` is strict: a wrong magic value, a failed length check,
or an over-read of the bounded window fails the query. Use `OUTER APPLY` with
`TryInterpret` when the candidate must remain visible:

```sql
outer apply TryInterpret<BoundedRecord>(candidate.WindowBytes) record
```

The candidate's path, offset, window coordinates, and `WindowComplete` value
remain populated while the interpreted fields are `NULL` for an invalid
candidate. A clipped window is not a proof that the record is invalid; it is
an incomplete input boundary and should be handled according to the query's
evidence policy.

For malformed or nested records, use `PartialInterpret` to preserve parse
evidence without converting a failure into a valid structure:

```sql
cross apply PartialInterpret<OuterRecord>(candidate.WindowBytes) parsed
```

Select `parsed.ErrorField`, `parsed.ErrorMessage`, and `parsed.BytesConsumed`
beside the Search coordinates. Nested over-read reports the qualified field
path (for example, `Payload.Value`) and retains the bytes consumed before the
failure. The composition tests cover a valid bounded record, a false magic
signature, an invalid length, and a nested over-read.

## Literal prefilters

The literal argument to `search.lines` is a cheap prefilter, not automatically
a completeness proof. The `ERROR` literal in the example is therefore
candidate-only: a valid record such as `2026-09-07 INFO: ...` may not contain
it and will not be returned. A cookbook may call a prefilter necessary only
when the record contract proves that every valid target record contains that
literal, and a fixture test covers the claim. Otherwise document the result as
candidate coverage and do not infer that an empty result means no valid record
exists.

The composition tests cover valid candidates, a strict malformed candidate,
`TryParse` null preservation, `PartialParse` diagnostics, source identity,
and a valid record intentionally lacking the proposed prefilter token.
