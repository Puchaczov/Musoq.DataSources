# Musoq.DataSources agent guide

## Big picture
- This repo is a set of Musoq data source plugins plus a few shared runtime/helper libraries. Most production folders follow `Schema` + `Library` + `Table` + `RowSource`/`Source` patterns.
- Start at the plugin `*Schema` class first. That is where sources and tables are registered and where dynamic behavior is usually wired.
- Schema XML docs are part of the product surface. Tags like `<virtual-constructors>` and `<additional-tables>` drive `desc #schema` output, so keep them aligned with code.
- The engine itself is outside this repo. Stay focused on plugin-side schema, row shaping, SQL-callable helpers, and integration code.

## Repository-wide conventions
- Dynamic plugins commonly override `GetTableByName()` / `GetRowSource()` and may return `InitiallyInferredTable` when query-side types are supplied.
- Static table classes are intentionally thin: expose `Entity.Columns` and `SchemaTableMetadata(typeof(EntityType))`.
- SQL-callable helpers live in `LibraryBase` subclasses and use `[BindableClass]` / `[BindableMethod]`; injected source entities often use `[InjectSpecificSource(typeof(...))]`.
- Streaming sources often inherit `AsyncRowsSourceBase<T>` and should preserve chunking plus `RuntimeContext.ReportDataSourceBegin/End(...)` behavior.

## Build and test workflow
- Default full validation is `dotnet test --configuration Release` from repo root; CI runs that on Windows.
- Focused validation should target the matching test project for the plugin being changed.
- Most tests compile Musoq scripts through `Musoq.DataSources.Tests.Common/InstanceCreatorHelpers.cs` with mocked environment variables and explicit culture setup.

## Detailed project guides

### Core helpers
- AsyncRowsSource: `Musoq.DataSources.AsyncRowsSource/copilot-instructions.md`
- Databases: `Musoq.DataSources.Databases/copilot-instructions.md`
- JsonHelpers: `Musoq.DataSources.JsonHelpers/copilot-instructions.md`
- LLMHelpers: `Musoq.DataSources.LLMHelpers/copilot-instructions.md`
- Roslyn.CommandLineArguments: `Musoq.DataSources.Roslyn.CommandLineArguments/copilot-instructions.md`

### File and local system plugins
- Archives: `Musoq.DataSources.Archives/copilot-instructions.md`
- CANBus: `Musoq.DataSources.CANBus/copilot-instructions.md`
- FlatFile: `Musoq.DataSources.FlatFile/copilot-instructions.md`
- Json: `Musoq.DataSources.Json/copilot-instructions.md`
- Os: `Musoq.DataSources.Os/copilot-instructions.md`
- SeparatedValues: `Musoq.DataSources.SeparatedValues/copilot-instructions.md`
- System: `Musoq.DataSources.System/copilot-instructions.md`
- Time: `Musoq.DataSources.Time/copilot-instructions.md`

### Database and repository plugins
- Git: `Musoq.DataSources.Git/copilot-instructions.md`
- Postgres: `Musoq.DataSources.Postgres/copilot-instructions.md`
- Sqlite: `Musoq.DataSources.Sqlite/copilot-instructions.md`

### Remote/API plugins
- Airtable: `Musoq.DataSources.Airtable/copilot-instructions.md`
- Docker: `Musoq.DataSources.Docker/copilot-instructions.md`
- GitHub: `Musoq.DataSources.GitHub/copilot-instructions.md`
- Jira: `Musoq.DataSources.Jira/copilot-instructions.md`
- Kubernetes: `Musoq.DataSources.Kubernetes/copilot-instructions.md`

### LLM plugins
- Ollama: `Musoq.DataSources.Ollama/copilot-instructions.md`
- OpenAI: `Musoq.DataSources.OpenAI/copilot-instructions.md`

### Roslyn
- Roslyn: `Musoq.DataSources.Roslyn/copilot-instructions.md`

## When in doubt
- Use the project-local `copilot-instructions.md` first, then cross-check the corresponding test project for expected query behavior.
- Keep public query syntax aligned with `RepresentativeQueries.md` and schema XML metadata.
