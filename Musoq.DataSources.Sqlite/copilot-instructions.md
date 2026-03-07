# Sqlite plugin guide

## Purpose
- SQLite counterpart to the shared database plugins, using dynamic tables resolved from `SQLITE_CONNECTION_STRING`.

## Read first
- `SqliteSchema.cs`
- `SqliteTable.cs`
- `SqliteRowSource.cs`
- `SqliteLibrary.cs`
- `Visitors/ToStringWhereQueryPartVisitor.cs`
- `Visitors/ToStringWhereQueryPartTraverseVisitor.cs`

## Patterns to preserve
- Keep SQLite-specific metadata lookup, identifier handling, and type coercion here.
- `SqliteTable` uses `PRAGMA table_info(...)` for schema discovery.
- `desc #sqlite` intentionally returns no schema-wide catalog, while `desc #sqlite.someTable` exposes the dynamic signature.
- Predicate translation is narrow and mirrors the Postgres plugin; expand support only with test coverage.
- Type mapping is explicit and limited to the supported SQLite storage classes used today.

## Integrations
- `Microsoft.Data.Sqlite`
- `Dapper`
- `Musoq.DataSources.Databases`

## Validate with
- `Musoq.DataSources.Sqlite.Tests/SqliteQueryTests.cs`
- `Musoq.DataSources.Sqlite.Tests/SqliteSchemaDescribeTests.cs`