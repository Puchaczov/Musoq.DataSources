# FlatFile plugin guide

## Purpose
- Minimal text-file plugin: one row per line, no header inference, and no typed-column coupling.

## Read first
- `FlatFileSchema.cs`
- `FlatFileSource.cs`
- `FlatFileTable.cs`
- `FlatFileEntity.cs`
- `FlatFileHelper.cs`
- `Musoq.DataSources.FlatFile.Tests/FlatFileTests.cs`
- `Musoq.DataSources.FlatFile.Tests/FlatFileSchemaDescribeTests.cs`

## Patterns to preserve
- Keep this plugin simple; header/type-aware behavior belongs in `SeparatedValues`, not here.
- Preserve file order exactly as `StreamReader.ReadLine()` sees it.
- Preserve empty-line behavior: blank physical lines are valid rows with `Line = string.Empty`; there is no header skipping or trimming.
- Keep line semantics intentionally narrow: only `LineNumber` and `Line` are part of the schema surface.
- Keep `ReportDataSourceBegin/End(...)` wrapped around the whole read.

## Line semantics and chunking
- `FlatFileSource` reads the file sequentially and yields `FlatFileEntity` rows in source order.
- Current chunking is synchronous and local to this plugin, not shared with `AsyncRowsSource`: it buffers rows in a `List<EntityResolver<FlatFileEntity>>` and emits after roughly 1000 rows.
- Be careful when changing chunk logic. The current implementation uses the same counter for emitted `LineNumber` values and chunk flushing, so any fix here must preserve existing observable behavior or come with explicit regression tests and doc updates.
- Empty files and missing files are both treated as zero-row sources; missing files simply return without throwing.
- Cancellation is only observed when adding chunks through the runtime token, so if you refactor reading flow, keep cancellation semantics aligned with the current tests.

## Empty-line behavior
- `Musoq.DataSources.FlatFile.Tests/TestMultilineFile.txt` is the canonical fixture: it starts with an empty line and contains another internal empty line.
- `HasSelectedAllLinesTest()` asserts that those blanks are surfaced as real rows, not skipped.
- The fixture also shows the intended contract around ordering: blank rows keep their original positions.

## Extension points
- `FlatFileSchema` exposes exactly one constructor today: `#flat.file(string path)`. If you add constructors, update both the XML docs and the `GetRawConstructors()` / `GetConstructors()` surface.
- `FlatFileEntity` + `FlatFileHelper` define the public column set. If you add or rename columns, update:
	- entity properties,
	- `FlatFileHelper.FlatNameToIndexMap`,
	- `FlatFileHelper.FlatIndexToMethodAccessMap`,
	- `FlatFileHelper.FlatColumns`,
	- `FlatFileTable`,
	- schema XML docs,
	- describe tests.
- `FlatFileLibrary` is currently empty. Add SQL-callable helpers there only if they are truly flat-file-specific; do not turn this plugin into a typed parser.
- If a feature needs delimiters, headers, inferred types, or stream/coupled inputs, it probably belongs in `SeparatedValues`, not here.

## Integrations
- No major external parser library; mostly Musoq core packages.

## Validate with
- `Musoq.DataSources.FlatFile.Tests/FlatFileTests.cs`
- `Musoq.DataSources.FlatFile.Tests/FlatFileSchemaDescribeTests.cs`

## Most representative tests
- `HasSelectedAllLinesTest()` is the main behavior test for row order, blank lines, and basic column projection.
- `FlatFileSource_CancelledLoadTest()` covers pre-cancelled execution returning zero rows.
- `FlatFileSource_FullLoadTest()` is the simplest direct source smoke test.
- `DescSchema_ShouldListAllAvailableMethods()` and `DescFileWithArgs_ShouldReturnTableSchema()` guard the schema/docs contract.
- There is no current regression test for files larger than one chunk. Add one before changing chunk size or line-number semantics.