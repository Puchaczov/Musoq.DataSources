# Json plugin guide

## Purpose
- Reads JSON files as rows while taking the projected shape from a separate schema JSON file.

## Read first
- `JsonSchema.cs`
- `JsonSource.cs`
- `JsonTable.cs`
- `JsonLibrary.cs`
- `../Musoq.DataSources.JsonHelpers/JsonParser.cs`

## Patterns to preserve
- `#json.file(jsonPath, schemaPath)` is schema-file-driven, not row-inference-driven.
- Object roots become one row; array roots become many rows.
- Keep JSON traversal and flattening in `JsonHelpers` instead of duplicating it locally.
- Helper methods like `MakeFlat()` are part of the user-visible query surface.

## Integrations
- `Newtonsoft.Json`
- `Musoq.DataSources.JsonHelpers`

## Validate with
- `Musoq.DataSources.Json.Tests/JsonTests.cs`
- `Musoq.DataSources.Json.Tests/JsonSchemaDescribeTests.cs`