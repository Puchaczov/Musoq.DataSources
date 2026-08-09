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
- `GetTextContent()`, `GetContent()`, and `GetStreamContent()` are user-facing archive helpers.
- Preserve runtime-v2 data-source begin/end reporting around archive processing.
- JSON and SeparatedValues accept file paths only. Archive content streams cannot be passed into those sources; extract an entry to a strict UTF-8 file first.

## Integrations

- Main external dependency is `SharpCompress`.

## Validate with

- `Musoq.DataSources.Archives.Tests/ArchivesTests.cs`
- `Musoq.DataSources.Archives.Tests/ArchivesSchemaDescribeTests.cs`
- `Musoq.DataSources.Archives.Tests/ArchivesRuntimeV2PushdownTests.cs`
