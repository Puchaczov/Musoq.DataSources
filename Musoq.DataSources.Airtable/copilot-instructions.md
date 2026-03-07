# Airtable plugin guide

## Purpose
- Exposes Airtable metadata and records as `bases`, `base`, and dynamic `records` sources.

## Read first
- `AirtableSchema.cs`
- `AirtableApi.cs`
- `IAirtableApi.cs`
- `Sources/`
- `Helpers/`

## Patterns to preserve
- `bases()` and `base()` use static table shapes, while `records(string tableName)` is dynamic and built from Airtable metadata.
- Keep new Airtable logic behind `IAirtableApi` instead of embedding network calls into row sources.
- Record value mapping goes through helper/type-mapping logic; keep metadata lookup and value conversion aligned.
- Schema XML docs and describe tests define the public constructor surface.

## Integrations
- Airtable SDK
- JSON serialization and metadata endpoints
- Environment variables: `MUSOQ_AIRTABLE_API_KEY`, `MUSOQ_AIRTABLE_BASE_ID`

## Validate with
- `Musoq.DataSources.Airtable.Tests/AirtableTests.cs`
- `Musoq.DataSources.Airtable.Tests/AirtableSchemaDescribeTests.cs`