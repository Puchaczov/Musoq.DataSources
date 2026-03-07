# Databases helper guide

## Purpose
- Shared base layer for SQL-backed plugins with dynamic columns and chunked row loading.
- This project exists to keep provider-neutral database mechanics out of `Postgres` and `Sqlite`.

## Read first
- `DatabaseRowSource.cs`
- `DatabaseTable.cs`
- `Helpers/DatabaseHelpers.cs`
- `Visitors/RawTraverseVisitor.cs`

## Patterns to preserve
- Keep provider-neutral logic here; quoting, dialect quirks, and provider-specific type handling belong in the consumer plugin.
- `DatabaseTable` expects metadata queries to return `name` and `type`; do not change that contract without updating all consumers.
- `DatabaseHelpers.GetDataFromDatabase(...)` currently batches rows in chunks; preserve chunking unless all database plugins are updated together.
- `DynamicObjectResolver` is the common bridge between materialized database rows and Musoq column access.

## Integrations
- Main external dependency is `Dapper`.
- `IDbConnection` comes from provider plugins.
- Parser traversal hooks come from Musoq parser visitors.

## Validate with
- `Musoq.DataSources.Postgres.Tests/PostgresQueryTests.cs`
- `Musoq.DataSources.Sqlite.Tests/SqliteQueryTests.cs`