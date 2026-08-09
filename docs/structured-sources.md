# Dynamic JSON and SeparatedValues sources

JSON and SeparatedValues treat strict UTF-8 files as dynamic Musoq tables. A UTF-8 BOM is allowed. The source scans every uncached file during metadata discovery, freezes an immutable schema snapshot, and then parses only the columns required by the compiled query.

## Constructors

```sql
#json.file(string path)

#separatedvalues.comma(string path, bool hasHeader, int skipLines)
#separatedvalues.tab(string path, bool hasHeader, int skipLines)
#separatedvalues.semicolon(string path, bool hasHeader, int skipLines)
```

Inputs must be readable file paths. Stream input, archive-content cross-apply, alternate encodings, and parsing modifiers are not supported. `skipLines` counts physical UTF-8 preamble lines before CSV header or data parsing.

## Discovery and binding

Discovery reads the complete source; it never samples rows. A column that appears anywhere belongs to the schema. If a discovered column is absent from one row, that row exposes `NULL`. A name that does not occur anywhere is an unknown-column compilation error.

Explicit projections use a compact dense layout containing only referenced source columns. `SELECT *` and `DESC` expose the complete discovered union. Snapshots are cached in process memory by canonical file identity and parser options. If the file changes after compilation, the source revalidates its identity and either accepts the compatible layout or reports schema drift before emitting rows.

Value inference recognizes `bool`, `long`, `decimal`, `double`, and `string`. Integers become `long`; ordinary fitting fractions become `decimal`; exponent forms or decimal overflow become `double`. Dates, times, GUIDs, and culture-specific representations are strings. Missing or null values make value types nullable. Conversion failures after binding are errors.

An optional Musoq `TABLE` coupling can supply explicit non-`object` types. It is not needed for dynamic discovery, and the source still validates names, widths, and conversions.

## JSON contract

- A root object is one row. A root array must contain objects.
- Columns are the union of top-level properties in first-seen order.
- Property names are exact, ordinal, and case-sensitive.
- Compatible scalar values infer the common numeric or scalar type. Conflicts widen to `object`.
- Nested objects and arrays are materialized only when their top-level property is requested.
- Duplicate properties, comments, trailing commas, multiple root documents, primitive root arrays, malformed JSON, and invalid UTF-8 are errors.

```sql
select Name, Age
from #json.file('./people.json')
where Age >= 18
```

## SeparatedValues contract

- Header names are preserved exactly. Empty or duplicate headers are errors; use bracket-quoted SQL identifiers for special names, for example `[Account name]`.
- Headerless files use `Column1`, `Column2`, and so on through the maximum width found anywhere in the source.
- Short records expose nulls. A headered record wider than the header is malformed.
- An unquoted empty field is `NULL`; a quoted empty field is an empty string. Whitespace is preserved.
- LF and CRLF records, quoted delimiters, doubled quotes, multiline fields, trailing fields, and blank-line skipping are supported.
- Type conflicts widen to `string`.

```sql
select Station, Min(Temperature), Max(Temperature), Avg(Temperature)
from #separatedvalues.semicolon('./measurements.csv', true, 0)
group by Station
```

## Runtime settings

The optional `json.max_parallelism` and `separatedvalues.max_parallelism` source settings cap file-scan workers. Missing or `0` selects an adaptive default; `1` forces sequential scanning. Small files and skip/take-sensitive plans remain sequential, and parallel results preserve source order.

See [structured-source performance evidence](performance/structured-sources.md) for the measured parser, source, and compiled-query layers. Migration from earlier constructors and permissive readers is covered in [breaking changes](structured-sources-breaking-changes.md).
