# SeparatedValues plugin guide

## Purpose
- Main CSV/TSV/semicolon plugin with header handling, stream support, query-side typing, and dynamic columns.

## Read first
- `SeparatedValuesSchema.cs`
- `SeparatedValuesTable.cs`
- `SeparatedValuesFromFileRowsSource.cs`
- `SeparatedValuesFromStreamRowsSource.cs`
- `InitiallyInferredTable.cs`
- `SeparatedValuesHelper.cs`

## Input modes
- Public constructors are only `#separatedvalues.comma(path, hasHeader, skipLines)`, `tab(...)`, and `semicolon(...)`.
- `GetRowSource()` supports three first-argument shapes internally: `string` path, `Stream`, or `IReadOnlyTable`.
- `IReadOnlyTable` input is the coupling/cross-apply mode and expects rows shaped like `(filePath, hasHeader, skipLines)`.
- Stream mode is mainly used through archive cross-apply scenarios; it depends on externally provided column metadata rather than self-inference.

## Dynamic table rules
- This is the clearest dynamic-table example in the repo.
- If `runtimeContext.QuerySourceInfo.HasExternallyProvidedTypes` is true, `GetTableByName()` returns `InitiallyInferredTable`; otherwise the table is built from file headers plus inferred types.
- File-backed inference opens the file, skips configured leading lines and blank lines, then builds columns from the first logical row.
- Headerless files become `Column1`, `Column2`, and so on.
- Stream mode does not infer its schema directly from the stream content; it relies on the runtime column set.

## Patterns to preserve
- Preserve header sanitization via `SeparatedValuesHelper.MakeHeaderNameValidColumnName()`.
- Preserve chunked async reading, cancellation, and `RuntimeContext.ReportDataSourceBegin/End(...)`.
- `Stream` and `IReadOnlyTable` inputs are part of the query contract and support coupling/cross-apply scenarios.

## Header and type behavior
- Header names are normalized by `SeparatedValuesHelper.MakeHeaderNameValidColumnName()`; tests treat this as part of the public contract.
- Type conversion lives in `ParseHelpers.ParseRecords()` and is culture-sensitive.
- Parse failures usually become `null` rather than hard failures for supported scalar types.
- `object`-typed inferred columns fall back to `string` when constructing schema columns.
- Duplicate headers after sanitization currently collide in dictionary maps; treat that behavior carefully.

## Runtime and chunking behavior
- File mode uses `AsyncRowsSourceBase` with large chunking and explicit runtime reporting under the `separated_values` source name.
- Multi-file `IReadOnlyTable` input is processed with `Parallel.ForEachAsync`, so cross-file output order is not guaranteed.
- Missing files are treated as empty input rather than immediate errors.
- File mode suppresses CsvHelper `BadDataFound`, so malformed CSV is tolerated more than a strict parser would be.

## Common pitfalls
- Stream mode is safest when the query already supplies types; do not assume it will self-infer like file mode.
- Culture matters. Tests intentionally pin culture for predictable numeric/date parsing.
- Empty or nearly empty files can still fail during header processing even though missing files are tolerated.
- If you change header normalization or scalar coercion, update both schema construction and row materialization paths together.

## Safe extension points
- Add new scalar conversion rules in `ParseHelpers.cs` first.
- Extend header normalization in `SeparatedValuesHelper.cs` only if table/schema/name maps stay aligned.
- Add plugin-specific helper methods in `SeparatedValuesLibrary.cs`.
- If you add constructor overloads or modes, update both XML docs and `GetRawConstructors()` / describe tests.

## Integrations
- `CsvHelper`
- `AsyncRowsSource`

## Validate with
- `Musoq.DataSources.SeparatedValues.Tests/CsvTests.cs`
- `Musoq.DataSources.SeparatedValues.Tests/SeparatedValuesSchemaDescribeTests.cs`

## Cross-project contract
- `Musoq.DataSources.Archives.Tests/ArchivesAndSeparatedValuesTests.cs` is the best stream/cross-apply integration test.