# CANBus plugin guide

## Purpose
- Turns DBC definitions and CAN frame dumps into queryable rows for raw frames, decoded messages, and signals.

## Read first
- `CANBusSchema.cs`
- `Components/CANBusApi.cs`
- `Messages/`
- `Signals/`
- `SeparatedValuesFromFile/`

## Patterns to preserve
- Keep DBC parsing in `CANBusApi`, row production in source classes, and schema wiring in `CANBusSchema`.
- `#can.separatedvalues(...)` is the dynamic surface: decoded message names and signal columns come from DBC metadata.
- Unknown frames are part of the contract; preserve `IsWellKnown`, null `Message`, and raw-data behavior.
- Avoid moving decoding logic into helper/library methods.

## Integrations
- `CsvHelper`
- `DbcParserLib`
- `AsyncRowsSource`
- Device-related packages from the project file

## Validate with
- `Musoq.DataSources.CANBus.Tests/SeparatedValuesTests.cs`
- `Musoq.DataSources.CANBus.Tests/MessagesOrSignalsTests.cs`
- `Musoq.DataSources.CANBus.Tests/CANBusSchemaDescribeTests.cs`