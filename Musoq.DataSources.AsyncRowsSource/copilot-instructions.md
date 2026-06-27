# AsyncRowsSource helper guide

## Purpose
- Shared runtime for plugins that emit typed rows asynchronously in chunks.
- The public surface is centered on `AsyncRowsSourceBase<T>`.

## Read first
- `AsyncRowsSourceBase.cs`
- `ChunkedSource.cs`
- `ChunkEnumerator.cs`

## Patterns to preserve
- Producers implement `CollectChunksAsync(...)` and yield full `IReadOnlyList<T>` chunks through the runtime-v2 row-source base.
- Keep linked cancellation, exception handoff, and buffered-row semantics intact; consumers rely on cancellation without losing already collected chunks.
- Empty chunks are valid and must not break enumeration.
- Do not swallow producer exceptions; the base class is responsible for surfacing them.

## Cross-project impact
- Changes here affect many plugins, especially `SeparatedValues`, `Roslyn`, `Git`, `GitHub`, `Jira`, and `Os` row sources.
- `Musoq.DataSources.JsonHelpers` is referenced here, so object-resolution changes can leak into multiple consumers.

## Validate with
- `Musoq.DataSources.AsyncRowsSource.Tests/AsyncRowsSourceBaseTests.cs`
- A real consumer such as `Musoq.DataSources.SeparatedValues/SeparatedValuesFromFileRowsSource.cs`
