# Roslyn plugin guide

## Purpose
- Full C# code-analysis plugin that loads solutions and exposes projects, documents, types, methods, metrics, diagnostics, and NuGet/package metadata as queryable data.

## Read first
- `CSharpSchema.cs`
- `CSharpLibrary.cs`
- `Components/`
- `Entities/`
- `RowsSources/`
- `Tables/`
- `CliCommands/SolutionOperationsCommand.cs`

## Architecture map
- `CSharpSchema.GetRowSource()` is the real composition root: it validates environment variables, wires HTTP/cache/rate-limit handlers, selects immediate-load vs in-memory solution flow, and defines the public XML-doc contract.
- `CSharpLibrary` is the safest extension point for new SQL-callable helpers over existing entities.
- `RowsSources/` contains solution-loading and query-optimization logic. Read `CSharpImmediateLoadSolutionRowsSource.cs`, `CSharpInMemorySolutionRowsSource.cs`, and `RoslynWhereNodeHelper.cs` first.
- The main entity chain is `SolutionEntity -> ProjectEntity -> DocumentEntity -> type/member entities`. Most user-visible behavior comes from entity properties rather than special row-source code.
- `Tables/` is secondary here; the primary table metadata entry is `CSharpSolutionTable.cs`.

## Data flow
- `#csharp.solution(path)` enters `CSharpSchema.GetRowSource()`.
- If the solution is already preloaded in `SolutionOperationsCommand.Solutions`, the schema uses the in-memory path; otherwise it opens the solution directly through Roslyn/MSBuild workspace logic.
- Row sources create a `SolutionEntity` and attach NuGet metadata retrieval services; `SolutionEntity.Projects` then lazily materializes `ProjectEntity` objects.
- `ProjectEntity.Documents` lazily creates `DocumentEntity` objects, and `DocumentEntity.InitializeAsync()` is the gateway to syntax tree and semantic model work.
- `DocumentEntity` fans out into `ClassEntity`, `InterfaceEntity`, `EnumEntity`, `StructEntity`, `DelegateEntity`, diagnostics, and related graph nodes.
- Member metrics and deep analysis mostly live in entities like `MethodEntity`, not in row-source code.
- NuGet/package metadata is a separate flow from syntax analysis and is driven through `ProjectEntity` plus the `Components/NuGet/` services.

## Patterns to preserve
- This is the most complex plugin in the repo; keep schema wiring, analysis logic, NuGet/network integration, and CLI/bootstrap concerns separated.
- `CSharpSchema.cs` XML docs are a major part of the public contract for `desc #csharp` and additional entity tables.
- Many query surfaces are entity-graph based (`Projects`, `Documents`, `Classes`, `Methods`, etc.), so entity property names and table metadata are user-visible.
- External metadata and package-resolution behavior rely on existing fallback strategies; do not simplify them casually.
- Rate limiting, cache/bucket behavior, and solution lifecycle hooks are part of runtime behavior, not just internal plumbing.

## Caches, lifecycle, and rate limiting
- `LifecycleHooks.cs` performs module initialization, MSBuild registration, exception logging, and CLI command bootstrap.
- `SolutionOperationsCommand.cs` owns the static preloaded-solution cache and the load/unload/cache-control command surface.
- `RateLimitingOptions.json` and `BannedPropertiesValues.json` are behavior files, not incidental assets.
- Persistent HTTP caching and per-query duplicate-request suppression are separate layers; keep both intact when changing request flow.
- `SingleOperationCache` is a concurrency gate with short-lived entries, not a durable memoizer.
- Package-version concurrency and domain throttling are independent subsystems; avoid merging or bypassing them casually.

## Common pitfalls
- `MUSOQ_SERVER_HTTP_ENDPOINT` is required for `GetRowSource()` even when the query mostly looks local.
- `DocumentEntity` members often assume initialization has already happened; ad hoc entity creation must respect that.
- `RoslynWhereNodeHelper` only supports narrow pushdown for simple equality filters joined by `AND`; `OR` is intentionally ignored for optimization.
- Timeout-tolerant analysis helpers return fallback values like `null` or `-1` instead of failing the entire query; preserve that behavior.
- Entity property names and XML docs are public query surface. Renaming them is a breaking change.
- NuGet metadata retrieval contains ordered fallbacks and banned-value filtering; “simplifying” it often changes observable results.

## Safe extension points
- Add new SQL-callable helper methods in `CSharpLibrary.cs` first.
- Add new entity-derived surfaces in `Entities/`, then reflect them in `CSharpSchema.cs` XML docs.
- Extend solution/project filter optimization by updating both `RoslynWhereNodeHelper` and the row-source project-matching logic.
- Swap external behaviors through interfaces in `Components/` and `Components/NuGet/`, not by editing callers throughout the entity graph.
- Extend CLI/cache behavior through `LifecycleHooks.cs` and `SolutionOperationsCommand.cs`, not by introducing scattered static state.

## Integrations and config
- `Microsoft.CodeAnalysis.*`
- `Microsoft.Build.Locator`
- CLI/bootstrap packages and HTTP helpers
- Packaged config files: `RateLimitingOptions.json`, `BannedPropertiesValues.json`
- Environment variables: `GITHUB_API_KEY`, `GITLAB_API_KEY`, `EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT`

## Validate with
- `Musoq.DataSources.Roslyn.Tests/RoslynToSqlTests.cs`
- `Musoq.DataSources.Roslyn.Tests/CSharpSchemaDescribeTests.cs`
- `Musoq.DataSources.Roslyn.Tests/RoslynWhereNodeOptimizationTests.cs`
- `Musoq.DataSources.Roslyn.Tests/NugetRetrievalTests.cs`
- `Musoq.DataSources.Roslyn.Tests/NugetResolveRawTests.cs`

## Key fixture set
- `Musoq.DataSources.Roslyn.Tests/TestsSolutions/Solution1` is the canonical behavior fixture.
- Use it when validating classes/interfaces/enums, partials, modern C# features, interface-usage patterns, and metric computation.