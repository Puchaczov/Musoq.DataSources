# Archives plugin guide

## Purpose
- Exposes archive entries as rows through `#archives.file(path)`.
- The public row shape is static; content access happens through helper methods rather than inferred columns.

## Read first
- `ArchivesSchema.cs`
- `ArchivesRowSource.cs`
- `ArchivesTable.cs`
- `EntryWrapper.cs`
- `ArchivesLibrary.cs`

## Patterns to preserve
- Keep enumeration streaming-friendly and single-pass where possible.
- `EntryWrapper.NameToIndexMap` and `ArchivesTable` must stay aligned.
- Helper methods like `GetTextContent()`, `GetContent()`, and `GetStreamContent()` are part of the user-facing query contract.
- Preserve `RuntimeContext.ReportDataSourceBegin/End(...)` around archive processing.

## Integrations
- Main external dependency is `SharpCompress`.

## Validate with
- `Musoq.DataSources.Archives.Tests/ArchivesTests.cs`
- `Musoq.DataSources.Archives.Tests/ArchivesAndSeparatedValuesTests.cs`
- `Musoq.DataSources.Archives.Tests/ArchivesSchemaDescribeTests.cs`