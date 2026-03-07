# System plugin guide

## Purpose
- Small utility schema providing `dual` and numeric `range`.

## Read first
- `SystemSchema.cs`
- `DualRowSource.cs`
- `RangeSource.cs`
- `DualTable.cs`
- `RangeTable.cs`

## Patterns to preserve
- Keep this plugin small and predictable; unrelated helpers do not belong here.
- Table shapes are static.
- `RangeSource` is half-open on the upper bound because it yields while `i < max`.
- `range` reports known row counts through runtime context; keep that behavior stable.

## Integrations
- No notable external SDKs beyond Musoq core packages.

## Validate with
- `Musoq.DataSources.System.Tests/DualTests.cs`
- `Musoq.DataSources.System.Tests/RangeTests.cs`
- `Musoq.DataSources.System.Tests/SystemSchemaDescribeTests.cs`