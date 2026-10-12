# Json plugin guide

## Purpose
- Streams strict UTF-8 JSON files as rows after exact source-driven schema discovery.

## Read first
- `JsonSchema.cs`
- `JsonQueryRowSource.cs`
- `JsonTable.cs`
- `JsonSchemaDiscovery.cs`
- `JsonRecordFramer.cs`
- `JsonQueryRowProcessor.cs`
- `JsonRowLayout.cs`
- `JsonPredicateEvaluator.cs`
- `JsonLibrary.cs`

## Patterns to preserve
- `json.file(jsonPath)` discovers its exact top-level union and scalar types from the complete UTF-8 source.
- Object roots become one row; array roots become many rows.
- Property names are exact, ordinal, and case-sensitive. Missing properties are null; source-wide unknown properties are compilation errors.
- Reject duplicate properties, comments, trailing commas, multiple root documents, primitive array elements, malformed UTF-8, and schema drift.
- Keep discovery cold-path state separate from format-specific execution hot loops.
- Skip unselected values without decoding them and evaluate accepted scalar predicates before materialization.
- Helper methods like `MakeFlat()` are part of the user-visible query surface.
- `json.max_parallelism`: missing or `0` is automatic, `1` is sequential, and positive values cap workers.

## Unsupported legacy behavior

- No schema-file argument, stream input, alternate encoding, permissive parsing, or failed-conversion-to-null path.

## Integrations
- `System.Text.Json`

## Validate with
- `Musoq.DataSources.Json.Tests/JsonTests.cs`
- `Musoq.DataSources.Json.Tests/JsonSchemaDescribeTests.cs`
- `Musoq.DataSources.Json.Tests/JsonSchemaDiscoveryTests.cs`
- `Musoq.DataSources.Json.Tests/JsonStreamingExecutionTests.cs`

## Native row contract

- File execution requires query-scoped concrete rows and a typed materializer; ordinary erased transfer is rejected.
- Structured and mixed inferred fields use `JsonElement`; SQL outputs remain scalar. `JsonText`, `Length` and `MakeFlat` consume that concrete type.
- Bind numeric predicates to closed typed operations. Fractional literals on integral source fields remain residual; rounding the literal during pushdown is forbidden.
