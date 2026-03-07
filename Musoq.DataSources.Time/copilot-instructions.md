# Time plugin guide

## Purpose
- Generates deterministic time-series rows for interval-based reporting and joins.

## Read first
- `TimeSchema.cs`
- `TimeSource.cs`
- `TimeTable.cs`
- `TimeHelper.cs`
- `TimeLibrary.cs`

## Patterns to preserve
- The public source is `#time.interval(startAt, stopAt, resolution)`.
- Supported resolutions are fixed strings like `seconds`, `minutes`, `hours`, `days`, `months`, and `years`.
- `TimeSource` adjusts the stop value by one smaller unit so interval generation is effectively inclusive for the requested resolution.
- Keep generation deterministic and preserve `ReportDataSourceBegin/End(...)` behavior.

## Integrations
- No major external SDKs beyond Musoq core packages.

## Validate with
- `Musoq.DataSources.Time.Tests/TimeTests.cs`
- `Musoq.DataSources.Time.Tests/TimeSchemaDescribeTests.cs`