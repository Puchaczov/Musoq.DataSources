# JsonHelpers guide

## Purpose
- Shared JSON parsing and object-resolution utilities used by JSON-aware plugins.
- The core surface is `JsonParser` plus `JsonObjectResolver`.

## Read first
- `JsonParser.cs`
- `JsonObjectResolver.cs`

## Patterns to preserve
- Keep this project reusable; plugin-specific schema behavior belongs in consuming plugins, not here.
- Preserve current shape rules: JSON objects become nested expandos/dictionaries, arrays stay arrays/lists, and scalars keep native values where possible.
- Property names and traversal behavior are part of the contract because consuming sources build column maps from parsed objects.
- Preserve parse cancellation checks and tolerant handling of normal JSON variants.

## Integrations
- Built on `Newtonsoft.Json`.
- Main consumer is `Musoq.DataSources.Json/JsonSource.cs`, but any plugin using JSON-shaped object access can be affected.

## Validate with
- `Musoq.DataSources.Json.Tests/JsonTests.cs`
- `Musoq.DataSources.Json.Tests/JsonSchemaDescribeTests.cs`