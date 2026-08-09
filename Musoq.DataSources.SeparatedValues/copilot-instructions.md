# SeparatedValues plugin guide

## Purpose

- Streams strict UTF-8 comma-, tab-, and semicolon-separated files after exact source-driven schema discovery.
- The public constructors are `#separatedvalues.comma(path, hasHeader, skipLines)`, `tab(...)`, and `semicolon(...)`.
- Inputs are file paths only. Stream input and archive cross-apply are not supported.

## Read first

- `SeparatedValuesSchema.cs`
- `SeparatedValuesSchemaDiscovery.cs`
- `SeparatedValuesUtf8Reader.cs`
- `SeparatedValuesFromFileRowsSource.cs`
- `SeparatedValuesTable.cs`
- `SeparatedValuesSourcePlanner.cs`

## Schema contract

- Discovery scans every logical record. Sampling and first-record-only inference are prohibited.
- UTF-8 BOM is accepted; other encodings and malformed UTF-8 are rejected.
- `skipLines` skips physical preamble lines before header or data parsing.
- Header names are preserved exactly and compared ordinally. Empty or duplicate headers are errors; special names require bracket-quoted SQL identifiers.
- Headerless sources use `Column1`, `Column2`, and so on through the maximum discovered width.
- Short rows expose nulls. A headered row wider than its header is malformed.
- An unquoted empty field is null; a quoted empty field is an empty string. Whitespace is preserved.
- Inference supports `bool`, `long`, `decimal`, `double`, and `string`; conflicts widen to `string`.
- Missing values make value types nullable. Explicit non-`object` `TABLE` types may override inference, but names and widths are still validated.
- Missing files, malformed input, schema drift, and conversion failures are errors.

## Hot-path rules

- Keep the parser synchronous, buffered, and span-based. Do not reintroduce `StreamReader`, CsvHelper, per-field delegates, or strings for skipped fields.
- Keep projection and accepted scalar predicates pushed into the reader. Rejecting a row must happen before projection allocation.
- Preserve the metadata-backed zero-column count path, bounded snapshot-local string reuse, cancellation, progress, and ordered partition draining.
- `separatedvalues.max_parallelism`: missing or `0` is automatic, `1` is sequential, and positive values cap workers.
- Keep discovery state in the shared linked structured-source files and execution loops format-specific.

## Unsupported legacy behavior

- No stream-backed source, archive-content cross-apply, alternate encoding, culture/codec/format/trim modifier, permissive malformed-input handling, or failed-conversion-to-null path.
- SeparatedValues does not depend on CsvHelper or AsyncRowsSource.

## Validate with

- `Musoq.DataSources.SeparatedValues.Tests/CsvTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesDynamicSchemaTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesSchemaDiscoveryTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesRuntimeV2ProjectionTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesParallelExecutionTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesDecimalParserTests.cs`
