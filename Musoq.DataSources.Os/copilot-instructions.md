# Os plugin guide

## Purpose
- Large local-system plugin for files, directories, processes, metadata extraction, DLL inspection, ZIP contents, and directory comparison.

## Read first
- `OsSchema.cs`
- `Files/`
- `Directories/`
- `Process/`
- `Metadata/`
- `Compare/`
- `Zip/`

## Domain map
- `Files/` is the best reference for file-scan sources and static entity/table shape.
- `Directories/` shows directory traversal and the lighter directory-specific optimization path.
- `Metadata/` contains the safest metadata-extraction surface for mixed-content folders.
- `Compare/Directories/` defines directory diff semantics.
- `Zip/`, `Dlls/`, and `Process/` are narrow resource surfaces with simpler source implementations.
- `EnumerateFilesSourceBase.cs` is the shared scan core for file-oriented sources.

## Schema and overload conventions
- Most sources expose constructor metadata directly from their source types via `TypeHelper.GetSchemaMethodInfosForType<T>()`.
- `metadata` is the main manual overload family: it has 1-arg, 2-arg, and 3-arg signatures wired explicitly in `OsSchema.cs`.
- Public discoverability depends on both XML `<virtual-constructors>` and `GetRawConstructors()` staying aligned.

## Patterns to preserve
- Keep work in the nearest domain folder instead of expanding `OsLibrary.cs` or `OsSchema.cs` with unrelated logic.
- The schema surface is broad but mostly static; overload selection in `OsSchema.cs` is part of the contract.
- XML docs in `OsSchema.cs` drive discoverability and `desc #os`; update them with any table or overload change.
- Preserve behavior around missing paths, file scans, and cross-apply-friendly metadata extraction.

## Optimization and metadata quirks
- Simple pushdown is centralized in `OsWhereNodeHelper.cs`.
- File pushdown is intentionally narrow: it only translates simple equality on file name-like fields and extensions.
- Directory pushdown is lighter than file pushdown and mostly uses name extraction.
- `OR` conditions are intentionally ignored for pushdown.
- `#os.metadata(...)` is safer than file-bound metadata helper methods for mixed-content folders because it skips unsupported files before reading metadata.
- Default one-argument `#os.metadata(path)` is stricter than the explicit overloads because it throws on metadata-read errors.

## Common pitfalls
- File-name pushdown uses file-system pattern lookup, so wildcard-looking values can behave more like glob patterns than pure equality.
- ZIP enumeration skips explicit directory entries even though the schema includes `IsDirectory`.
- DLL scanning silently skips unloadable assemblies.
- Tests sometimes use legacy `#disk` queries through a schema provider alias, but the production surface is `#os`.
- `FileEntity.DirectoryName` and `DirectoryPath` are not interchangeable; preserve current semantics if you touch file entities.

## Safe extension points
- For a new file-based surface, reuse `EnumerateFilesSourceBase<TEntity>` and add a dedicated domain folder with `Entity`, schema helper, source, and table types.
- Keep all source/table registration and manual overload wiring in `OsSchema.cs`.
- Add SQL helpers in `OsLibrary.cs` with bindable methods and injected source entities where appropriate.
- If you extend pushdown, do it in `OsWhereNodeHelper.cs` first rather than ad hoc in individual sources.

## Integrations
- `MetadataExtractor`
- `AsyncRowsSource`
- .NET IO, compression, process, and reflection APIs

## Validate with
- `Musoq.DataSources.Os.Tests/QueryDiskTests.cs`
- `Musoq.DataSources.Os.Tests/ZipTests.cs`
- `Musoq.DataSources.Os.Tests/ImagesTests.cs`
- `Musoq.DataSources.Os.Tests/OsWhereNodeOptimizationTests.cs`
- `Musoq.DataSources.Os.Tests/OsSchemaDescribeTests.cs`