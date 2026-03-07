# Postgres plugin guide

## Purpose
- Dynamic PostgreSQL table plugin over the shared `Databases` infrastructure.
- `#postgres.tableName('schemaName')` resolves metadata at runtime and executes translated SQL against PostgreSQL.

## Read first
- `PostgresSchema.cs`
- `PostgresTable.cs`
- `PostgresRowSource.cs`
- `Visitors/ToStringWhereQueryPartVisitor.cs`
- `Visitors/ToStringWhereQueryPartTraverseVisitor.cs`

## Patterns to preserve
- Keep provider-specific SQL, quoting, and type behavior here; shared mechanics belong in `Musoq.DataSources.Databases`.
- `PostgresTable` depends on `information_schema.columns` and the current name/type metadata contract.
- `GetRawConstructors(methodName, ...)` exposes a single `schemaName` parameter; `desc` behavior is part of the public contract.
- Predicate translation is intentionally limited today; adding support should update both visitor files and tests.
- Type mapping is explicit and narrow; unsupported PostgreSQL types currently throw rather than silently guessing.

## Integrations
- `Npgsql`
- `Dapper`
- `Musoq.DataSources.Databases`

## Validate with
- `Musoq.DataSources.Postgres.Tests/PostgresQueryTests.cs`
- `Musoq.DataSources.Postgres.Tests/PostgresSchemaDescribeTests.cs`