# SeparatedValues plugin guide

## Purpose

- Streams UTF-8 comma-, tab-, semicolon-, and explicitly delimited files through a bounded, byte-native pipeline.
- The public constructors are `#separatedvalues.comma(path, hasHeader, skipLines)`, `tab(...)`, and `semicolon(...)`.
- `#separatedvalues.delimited(path, delimiter, hasHeader, skipLines)` selects one explicit ASCII delimiter; delimiter and header detection are never guessed.
- Inputs are file paths only. Stream input and archive cross-apply are not supported.

## Read first

- `SeparatedValuesSchema.cs`
- `SeparatedValuesBoundedSchemaResolver.cs`
- `SeparatedValuesFormat.cs`
- `SeparatedValuesUtf8Reader.cs`
- `SeparatedValuesScanPipeline.cs`
- `SeparatedValuesParallelBlockScanPipeline.cs`
- `SeparatedValuesTable.cs`
- `SeparatedValuesSourcePlanner.cs`

## Schema contract

- A concrete `TABLE` contract is authoritative. Metadata resolution reads only enough to map the header (or obtain the first headerless width); it does not infer declared types from data rows.
- Direct dynamic sources infer from a bounded sample and stop at the first of 1 MiB, 4,096 complete data records, or 10 ms. The three limits are configurable with `separatedvalues.inference_max_bytes`, `separatedvalues.inference_max_rows`, and `separatedvalues.inference_max_time_ms`.
- The time limit is cooperative between reads and records; a blocking filesystem read is not a hard real-time deadline. If a complete required header or first record does not fit, resolution fails with guidance to provide a typed `TABLE` contract or increase the limits.
- Sampled value columns are conservatively nullable. A later value that contradicts a sampled type fails with file, row, column, expected type, and observed token; types never widen during execution.
- UTF-8 BOM is accepted; other encodings and malformed UTF-8 are rejected.
- `skipLines` skips physical preamble lines before header or data parsing.
- Header names are preserved exactly and compared ordinally. Empty or duplicate headers are errors; special names require bracket-quoted SQL identifiers.
- Headerless dynamic sources use `Column1`, `Column2`, and so on through the maximum sampled width. A concrete headerless TABLE binds its declared names by source ordinal.
- Short rows expose nulls. A headered row wider than its header is malformed.
- Under strict defaults an unquoted empty field is null, a quoted empty field is an empty string, and whitespace is preserved.
- Inference supports `bool`, `long`, `decimal`, `double`, and `string`; conflicts inside the sample widen to `string`.
- Headerless width is fixed by the resolved sample. A wider later record is schema drift.
- Missing files, malformed input, schema drift, and conversion failures are errors.

## Hot-path rules

- Keep the sequential parser buffered and span-based, and the large-file path on pooled random-access byte blocks. Do not reintroduce `StreamReader`, CsvHelper, per-field delegates, or strings for skipped fields.
- Keep field location, sampled-schema validation, accepted scalar predicates, and projection fused. Rejected rows must not allocate row arrays, strings, or boxed values.
- Large files (currently at least 64 MiB) use asynchronous read-ahead, quote-state block summaries, shared process-wide CPU permits, dynamic workers, and a bounded ordered output window. Preserve complete CSV grammar including multiline quoted fields.
- Preserve source order, cancellation, early `TAKE`, progress, bounded buffers, and deterministic error propagation.
- Completed scans may publish memory-only coarse block summaries and an exact count under the current file identity. Never add sidecars or a persistent cache.
- `separatedvalues.max_parallelism`: missing or `0` is automatic, `1` is sequential, and positive values cap workers.
- The generic source accepts normalized runtime settings: `separatedvalues.quote_char` (`"` or `none`), `escape_mode` (`double`, `backslash`, `none`), `whitespace_mode` (`preserve`, `trim`), `blank_record_mode` (`skip`, `emit`), `comment_prefix`, `null_tokens` (JSON string array), `value_culture`, `record_endings` (`lf_crlf`, `any`), `max_record_bytes`, and `max_buffered_bytes`. Existing comma/tab/semicolon calls retain strict defaults.
- `null_tokens` apply only to unquoted fields; quoted tokens remain strings. Empty unquoted fields remain null and quoted empty fields remain empty strings.
- Keep discovery state in the shared linked structured-source files and execution loops format-specific.

## Unsupported legacy behavior

- No stream-backed source, archive-content cross-apply, alternate encoding, automatic delimiter/header detection, permissive malformed-input handling, or failed-conversion-to-null path. Culture and trimming are explicit generic-source settings, not implicit heuristics.
- SeparatedValues does not depend on CsvHelper or AsyncRowsSource.

## Validate with

- `Musoq.DataSources.SeparatedValues.Tests/CsvTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesDynamicSchemaTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesSchemaInferenceTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesBoundedInferenceTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesParallelBlockPipelineTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesStructuralSummaryTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesRuntimeV2ProjectionTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesParallelExecutionTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesDecimalParserTests.cs`
