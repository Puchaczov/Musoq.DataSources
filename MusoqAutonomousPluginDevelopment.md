# Musoq .NET Data Source Plugin - Autonomous Runtime V2 Migration Guide

This guide is written for an autonomous coding agent or developer building a Musoq data source plugin in a repository that does not contain the Musoq source tree. It intentionally includes the contracts, file layout, implementation snippets, migration checks, tests, packaging scripts, and troubleshooting notes needed to move an old runtime-v1 datasource to the runtime-v2 engine.

Runtime-v2 is the only target in this guide. Do not preserve runtime-v1 compatibility unless a separate host explicitly requires it.

## Table of Contents

1. [Phase 0 - Pre-Flight Checks](#phase-0---pre-flight-checks)
2. [Phase 1 - Scaffolded Execution Plan](#phase-1---scaffolded-execution-plan)
3. [Phase 2 - Plugin Architecture Overview](#phase-2---plugin-architecture-overview)
4. [Phase 3 - Step-by-Step Implementation](#phase-3---step-by-step-implementation)
5. [Phase 4 - XML Documentation (Critical)](#phase-4---xml-documentation-critical)
6. [Phase 5 - Unit Tests](#phase-5---unit-tests)
7. [Phase 6 - Build and Package](#phase-6---build-and-package)
8. [Phase 7 - Import and Install Scripts](#phase-7---import-and-install-scripts)
9. [Appendix A - Troubleshooting and Common Pitfalls](#appendix-a---troubleshooting-and-common-pitfalls)
10. [Appendix B - Complete File Reference](#appendix-b---complete-file-reference)
11. [Appendix C - NuGet Package Version Resolution](#appendix-c---nuget-package-version-resolution)
12. [Appendix D - Predicate Pushdown in Runtime V2](#appendix-d---predicate-pushdown-in-runtime-v2)

## Phase 0 - Pre-Flight Checks

### 0.1 Detect Existing Solution

Start in the plugin repository root.

```powershell
Get-ChildItem -Filter *.sln
Get-ChildItem -Recurse -Filter *.csproj
```

If a solution exists, add the plugin and test project to it. If no solution exists, create one:

```powershell
dotnet new sln -n MyPlugin
dotnet new classlib -n Musoq.DataSources.MyPlugin -f net10.0
dotnet new mstest -n Musoq.DataSources.MyPlugin.Tests -f net10.0
dotnet sln add Musoq.DataSources.MyPlugin/Musoq.DataSources.MyPlugin.csproj
dotnet sln add Musoq.DataSources.MyPlugin.Tests/Musoq.DataSources.MyPlugin.Tests.csproj
dotnet add Musoq.DataSources.MyPlugin.Tests/Musoq.DataSources.MyPlugin.Tests.csproj reference Musoq.DataSources.MyPlugin/Musoq.DataSources.MyPlugin.csproj
```

Use `net10.0`. The current Musoq source train is built with the .NET SDK `10.0.300` feature band (`rollForward: latestFeature`). In a standalone plugin repository, add this `global.json`:

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

### 0.2 Determine Musoq Package Versions

Prefer one consistent Musoq package train. The current known train when this guide was refreshed is `17.0.0-alpha.1`, but do not hardcode it forever. Resolve versions from the target host, package feed, or sibling projects.

Required packages for a datasource plugin:

- `Musoq.Schema` - schema contracts, row sources, planning contexts
- `Musoq.Plugins` - `LibraryBase`, built-in methods, bindable method attributes

Common test packages:

- `Musoq.Converter` - `InstanceCreator` query compilation APIs
- `Musoq.Evaluator` - `CompilationOptions`, compiled query result types
- `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`

Example project references using the current train:

```xml
<ItemGroup>
  <PackageReference Include="Musoq.Schema" Version="17.0.0-alpha.1">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <PackageReference Include="Musoq.Plugins" Version="17.0.0-alpha.1">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
</ItemGroup>
```

The host normally provides the Musoq runtime assemblies. Keep compile assets, but exclude runtime assets from plugin output to avoid duplicate assembly loading.

Example test references:

```xml
<ItemGroup>
  <PackageReference Include="Musoq.Converter" Version="17.0.0-alpha.1" />
  <PackageReference Include="Musoq.Evaluator" Version="17.0.0-alpha.1" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="MSTest.TestAdapter" Version="3.8.2" />
  <PackageReference Include="MSTest.TestFramework" Version="3.8.2" />
</ItemGroup>
```

### 0.3 Verify Prerequisites

```powershell
dotnet --version
dotnet restore --nologo --verbosity quiet
```

Required:

- .NET SDK `10.0.300` or newer compatible `10.0` feature band
- Access to the NuGet feed containing the selected Musoq package train
- A Musoq host or CLI version compatible with the same package train

If the repository still targets `net8.0`, migrate the plugin and tests to `net10.0` before changing datasource code.

## Phase 1 - Scaffolded Execution Plan

Fill this checklist before editing code. It gives an autonomous agent the decisions it needs.

| Decision | Value |
|----------|-------|
| Plugin assembly name | `Musoq.DataSources.<Name>` |
| Schema name used in SQL | `<schema>` in `#<schema>.<source>()` |
| Source method names | Example: `items`, `records`, `events` |
| Row shape | Typed entity, dictionary row, or both |
| Runtime settings | None, API token, endpoint, tenant, profile-specific settings |
| Planning support | Reject all, projection only, predicate, order, skip, take |
| Read modifiers | None, encoding, culture, format, trim, source-specific keys |
| Publish target RIDs | `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`, etc. |
| Tests | Direct source tests, compiled query tests, diagnostics tests |

Default choices for a migration:

1. Use typed entities for stable datasource schemas.
2. Use `IReadOnlyDictionary<string, object?>` only when columns are declared by `TABLE` or inferred at runtime.
3. Return `SourcePlanResult.RejectAll(request)` until pushdown is implemented and tested.
4. Move secrets and environment-specific values to source runtime settings.
5. Maintain XML docs as a critical static discovery contract, and keep them synchronized with runtime-v2 table metadata.

## Phase 2 - Plugin Architecture Overview

Runtime-v2 datasource execution has four explicit stages.

```text
SQL
  -> schema provider resolves #schema
  -> schema returns table metadata and source description
  -> planner asks schema what work the source can accept
  -> execution asks schema for RowSource<T> with accepted source plan and settings
  -> row source emits IReadOnlyList<T> chunks
```

### Runtime V2 Public Contract Surface

Every runtime-v2 schema implements `ISchema`. Most plugins should derive from `SchemaBase`.

```csharp
public interface ISchema
{
    string Name { get; }

    ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object?[] parameters);

    SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object?[] parameters);

    IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object?[] parameters);

    SourcePlanResult TryPlanSource(
        string name,
        SourcePlanRequest request,
        params object?[] parameters);

    RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object?[] parameters);
}
```

The schema provider remains simple:

```csharp
public interface ISchemaProvider
{
    ISchema GetSchema(string schema);
}
```

### Runtime Contexts

`SourceMetadataContext` is available during metadata lookup:

```csharp
public class SourceMetadataContext
{
    public string QueryId { get; }
    public CancellationToken EndWorkToken { get; }
    public IReadOnlyCollection<ISchemaColumn> AllColumns { get; }
    public IReadOnlyDictionary<string, string> SourceRuntimeSettings { get; }
    public ILogger Logger { get; }
}
```

`SourceExecutionContext` extends metadata context and adds the accepted plan, diagnostics, and progress callbacks:

```csharp
public class SourceExecutionContext : SourceMetadataContext
{
    public SourceExecutionPlan Plan { get; }
    public SourceDiagnostics Diagnostics { get; }

    public void ReportDataSourceBegin(string dataSourceName);
    public void ReportDataSourceRowsKnown(string dataSourceName, long totalRows);
    public void ReportDataSourceRowsRead(string dataSourceName, long rowsProcessed, long? totalRows = null);
    public void ReportDataSourceEnd(string dataSourceName, long? totalRowsProcessed = null);
}
```

### Retired Runtime V1 APIs

These names are invalid in active runtime-v2 datasource code:

| Retired API | Runtime-v2 replacement |
|-------------|------------------------|
| `RuntimeContext` | `SourceMetadataContext` or `SourceExecutionContext` |
| `QuerySourceInfo` | `SourceIdentity`, `SourceDescriptor`, and `SourceExecutionPlan` |
| `QueryHints` | `SourcePlanRequest` and `SourcePlanResult` |
| `IObjectResolver` | typed rows or dictionary rows |
| `EntityResolver` | typed entity properties and table metadata |
| `BlockingCollection` row chunks | `RowSource<T>.Chunks` as `IEnumerable<IReadOnlyList<T>>` |
| non-generic `GetRowSource(...)` | generic `GetRowSource<T>(...)` |
| `WhereNodeHelper` | `SourcePredicateExpression` planning in `TryPlanSource` |

Do not keep these names in implementation snippets except in migration notes or tests that intentionally assert they are gone.

## Phase 3 - Step-by-Step Implementation

The examples below build a standalone weather datasource. Replace names and data fetching with the real source.

### 3.1 Project File

`Musoq.DataSources.Weather/Musoq.DataSources.Weather.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <AssemblyName>Musoq.DataSources.Weather</AssemblyName>
    <RootNamespace>Musoq.DataSources.Weather</RootNamespace>
    <PackageId>Musoq.DataSources.Weather</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Product>Musoq</Product>
    <Description>Runtime-v2 weather datasource for Musoq.</Description>
    <PackageProjectUrl>https://example.org/weather-plugin</PackageProjectUrl>
    <PackageTags>musoq;datasource;weather;runtime-v2</PackageTags>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <!-- Critical: copy XML documentation for referenced NuGet assemblies when
       they are available. Musoq hosts and registry tooling can read XML
       metadata without loading arbitrary plugin DLLs. -->
  <Target Name="_ResolveCopyLocalNuGetPackageXmls" AfterTargets="ResolveReferences">
    <ItemGroup>
      <ReferenceCopyLocalPaths
        Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')"
        Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)' != '' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
    </ItemGroup>
  </Target>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
    <PackageReference Include="Musoq.Schema" Version="17.0.0-alpha.1">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Musoq.Plugins" Version="17.0.0-alpha.1">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

Critical project settings:

| Setting | Why it matters |
|---------|----------------|
| `TargetFramework=net10.0` | matches the runtime-v2 host train and `.NET 10.0.300` feature band |
| `EnableDynamicLoading` | allows dynamic plugin loading in hosts that use collectible load contexts |
| `GenerateDocumentationFile` | emits the XML catalog Musoq can inspect without loading the DLL |
| `_ResolveCopyLocalNuGetPackageXmls` | preserves XML metadata for referenced packages when present |
| `ExcludeAssets=runtime` on host-provided `Musoq.*` packages | prevents duplicate Musoq assemblies in `Plugin.zip` |
| package metadata | lets package registries and import scripts display useful plugin information |

Do not use `ExcludeAssets=runtime` in the test project unless the test host also supplies those assemblies. Test projects normally reference `Musoq.Converter` and `Musoq.Evaluator` directly.

### 3.2 Assembly Registration Files

Most Musoq plugin hosts discover schema names by assembly metadata before they load a plugin. This file is not optional for standalone plugin packages.

`Properties/AssemblyInfo.cs`:

```csharp
using System.Reflection;
using Musoq.Schema.Attributes;

[assembly: PluginSchemas("weather")]
[assembly: AssemblyTitle("Musoq Weather Data Source")]
[assembly: AssemblyDescription("Runtime-v2 weather datasource for Musoq.")]
[assembly: AssemblyCompany("Example")]
[assembly: AssemblyProduct("Musoq.DataSources.Weather")]
```

`PluginSchemas("weather")` is the static discovery key for SQL such as `select * from #weather.current('Warsaw')`. Keep it lowercase and keep it identical to the schema name passed to `SchemaBase`.

`Assembly.cs`:

```csharp
using Musoq.Schema;

namespace Musoq.DataSources.Weather;

public sealed class Assembly
{
    public static ISchemaProvider CreateSchemaProvider()
    {
        return new WeatherSchemaProvider();
    }
}
```

`PluginSchemas` is the discovery contract. `Assembly.CreateSchemaProvider()` is a common host factory convention. If your host expects a different factory type, keep the runtime-v2 schema code unchanged and adapt only this registration file.

### 3.3 Entity Class

Use typed entities when the datasource owns its schema.

`WeatherEntity.cs`:

```csharp
namespace Musoq.DataSources.Weather;

public sealed class WeatherEntity
{
    public string City { get; init; } = string.Empty;

    public DateTimeOffset ObservedAt { get; init; }

    public decimal TemperatureC { get; init; }

    public decimal HumidityPercent { get; init; }

    public string Condition { get; init; } = string.Empty;
}
```

Runtime-v2 reads properties directly from typed rows. You no longer need a resolver map to extract values by index.

### 3.4 Table Metadata

`ISchemaTable` describes column names, indexes, types, and the row type.

`WeatherTable.cs`:

```csharp
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Weather;

public sealed class WeatherTable : ISchemaTable
{
    public WeatherTable()
    {
    }

    public WeatherTable(string city)
    {
        _ = city;
    }

    public ISchemaColumn[] Columns { get; } =
    [
        new SchemaColumn(nameof(WeatherEntity.City), 0, typeof(string)),
        new SchemaColumn(nameof(WeatherEntity.ObservedAt), 1, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(WeatherEntity.TemperatureC), 2, typeof(decimal)),
        new SchemaColumn(nameof(WeatherEntity.HumidityPercent), 3, typeof(decimal)),
        new SchemaColumn(nameof(WeatherEntity.Condition), 4, typeof(string))
    ];

    public SchemaTableMetadata Metadata { get; } = new(typeof(WeatherEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column =>
            string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase));
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns
            .Where(column => string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
```

The `SchemaTableMetadata(typeof(WeatherEntity))` value is important. The engine uses it to request `GetRowSource<WeatherEntity>`. If metadata says one row type and the row source returns another, runtime-v2 fails fast.

### 3.5 RowSource Class

`RowSource<T>` returns chunks. Each chunk is an `IReadOnlyList<T>`.

Use `RowSourceBase<T>` when data is produced inside `CollectChunks`:

```csharp
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Weather;

public sealed class WeatherRowSource(
    WeatherClient client,
    SourceExecutionContext executionContext,
    string city)
    : RowSourceBase<WeatherEntity>
{
    protected override void CollectChunks(IChunkWriter<WeatherEntity> writer)
    {
        const string dataSourceName = "weather.current";

        executionContext.ReportDataSourceBegin(dataSourceName);

        var rows = client.GetCurrent(city, writer.CancellationToken);
        var plannedRows = ApplyAcceptedPlan(rows, executionContext.Plan).ToArray();

        executionContext.ReportDataSourceRowsKnown(dataSourceName, plannedRows.Length);
        writer.Write(plannedRows);
        executionContext.ReportDataSourceRowsRead(dataSourceName, plannedRows.Length, plannedRows.Length);
        executionContext.ReportDataSourceEnd(dataSourceName, plannedRows.Length);
    }

    private static IEnumerable<WeatherEntity> ApplyAcceptedPlan(
        IEnumerable<WeatherEntity> rows,
        SourceExecutionPlan plan)
    {
        var query = rows;

        if (plan.AcceptedPredicate != null)
            query = query.Where(row => WeatherPredicateEvaluator.Evaluate(plan.AcceptedPredicate, row));

        foreach (var order in plan.AcceptedOrderBy.Reverse())
            query = ApplyOrder(query, order);

        if (plan.AcceptedSkip.HasValue)
            query = query.Skip((int)plan.AcceptedSkip.Value);

        if (plan.AcceptedTake.HasValue)
            query = query.Take((int)plan.AcceptedTake.Value);

        return query;
    }

    private static IEnumerable<WeatherEntity> ApplyOrder(
        IEnumerable<WeatherEntity> rows,
        OrderByExpression order)
    {
        Func<WeatherEntity, object?> keySelector = order.Column.Name switch
        {
            nameof(WeatherEntity.City) => row => row.City,
            nameof(WeatherEntity.ObservedAt) => row => row.ObservedAt,
            nameof(WeatherEntity.TemperatureC) => row => row.TemperatureC,
            nameof(WeatherEntity.HumidityPercent) => row => row.HumidityPercent,
            nameof(WeatherEntity.Condition) => row => row.Condition,
            _ => throw new InvalidOperationException($"Unsupported order column '{order.Column.Name}'.")
        };

        return order.Direction == OrderDirection.Descending
            ? rows.OrderByDescending(keySelector)
            : rows.OrderBy(keySelector);
    }
}
```

Use `EntitySource<T>` when rows are already materialized into chunks:

```csharp
var chunks = RowChunking.FromEnumerableOutput(rows);
return EnsureSourceType<T, WeatherEntity>(
    name,
    new EntitySource<WeatherEntity>(
        chunks,
        new Dictionary<string, int>(),
        new Dictionary<int, Func<WeatherEntity, object?>>()));
```

The two dictionaries are retained for compatibility with the helper class constructor but are ignored by runtime-v2. Do not rebuild old resolver maps just to populate them.

### 3.6 Library Class

Use libraries for SQL-callable helper functions, not for datasource row access.

`WeatherLibrary.cs`:

```csharp
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Weather;

public sealed class WeatherLibrary : LibraryBase
{
    [BindableMethod]
    public decimal CelsiusToFahrenheit(decimal celsius)
    {
        return (celsius * 9m / 5m) + 32m;
    }
}
```

### 3.7 SchemaProvider Class

`WeatherSchemaProvider.cs`:

```csharp
using Musoq.Schema;

namespace Musoq.DataSources.Weather;

public sealed class WeatherSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        if (!string.Equals(schema, "weather", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(schema, "#weather", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Schema '{schema}' is not supported.");
        }

        return new WeatherSchema(new WeatherClient());
    }
}
```

### 3.8 Schema Class

`WeatherSchema.cs`:

```csharp
using Musoq.Plugins;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Weather;

public sealed class WeatherSchema : SchemaBase
{
    private const string Current = "current";
    private readonly WeatherClient _client;

    public WeatherSchema(WeatherClient client)
        : base("weather", CreateLibrary())
    {
        _client = client;

        // Registers constructor metadata used by desc #weather and desc #weather.current.
        // Keep WeatherTable constructors aligned with supported source parameters.
        AddTable<WeatherTable>(Current);
    }

    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object?[] parameters)
    {
        EnsureSourceName(name);
        return new WeatherTable();
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object?[] parameters)
    {
        EnsureSourceName(name);
        var table = GetTableByName(name, context.MetadataContext, parameters);
        return new SourceDescriptor
        {
            Identity = context.Identity,
            RowType = table.Metadata.TableEntityType,
            Columns = table.Columns
        };
    }

    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object?[] parameters)
    {
        EnsureSourceName(name);
        return
        [
            new SourceRuntimeSettingRequirement(
                "WEATHER_API_KEY",
                Required: true,
                Secret: true,
                SourceRuntimeSettingPhase.Execution,
                "API key used to call the weather service."),
            new SourceRuntimeSettingRequirement(
                "WEATHER_ENDPOINT",
                Required: false,
                Secret: false,
                SourceRuntimeSettingPhase.Execution,
                "Optional weather API endpoint override.")
        ];
    }

    public override SourcePlanResult TryPlanSource(
        string name,
        SourcePlanRequest request,
        params object?[] parameters)
    {
        EnsureSourceName(name);

        var acceptedPredicate = WeatherPredicatePlanner.TryAccept(request.Predicate);
        var acceptsPredicate = acceptedPredicate != null || request.Predicate == null;
        var acceptedOrder = request.OrderBy.Where(IsSupportedOrder).ToArray();
        var acceptsAllOrder = acceptedOrder.Length == request.OrderBy.Count;
        var acceptsSlice = acceptsPredicate && acceptsAllOrder;

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = request.RequiredColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = acceptedOrder,
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null
            },
            AcceptedColumns = request.RequiredColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = acceptsPredicate ? null : request.Predicate,
            AcceptedOrderBy = acceptedOrder,
            ResidualOrderBy = acceptsAllOrder ? [] : request.OrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Cardinality = CardinalityEstimate.Unknown("Weather API cardinality is request-dependent.")
        };
    }

    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object?[] parameters)
    {
        EnsureSourceName(name);

        if (parameters.Length != 1 || parameters[0] is not string city)
            throw new ArgumentException("#weather.current(city) requires one string city argument.");

        return EnsureSourceType<T, WeatherEntity>(
            name,
            new WeatherRowSource(_client.WithSettings(executionContext.SourceRuntimeSettings), executionContext, city));
    }

    private static bool IsSupportedOrder(OrderByExpression order)
    {
        return string.Equals(order.Column.Name, nameof(WeatherEntity.ObservedAt), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(order.Column.Name, nameof(WeatherEntity.City), StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureSourceName(string name)
    {
        if (!string.Equals(name, Current, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Source '{name}' is not supported.");
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        methodsManager.RegisterLibraries(new LibraryBase());
        methodsManager.RegisterLibraries(new WeatherLibrary());
        return new MethodsAggregator(methodsManager);
    }
}
```

If planning is not implemented yet, use:

```csharp
public override SourcePlanResult TryPlanSource(
    string name,
    SourcePlanRequest request,
    params object?[] parameters)
{
    return SourcePlanResult.RejectAll(request);
}
```

### 3.8.1 Raw Constructors and `desc` Support

Musoq uses raw constructor metadata for `desc #schema`, `desc #schema.method`, and some static package catalogs. Runtime-v2 uses these exact signatures:

```csharp
public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext);

public override SchemaMethodInfo[] GetRawConstructors(
    string methodName,
    SourceMetadataContext metadataContext);
```

If you call `AddTable<WeatherTable>(Current)` and `WeatherTable` has constructors that match the public source parameters, `SchemaBase` can produce this metadata automatically. The table constructors do not have to perform I/O; they only describe accepted parameters for metadata and `desc`.

```csharp
public sealed class WeatherTable : ISchemaTable
{
    public WeatherTable()
    {
    }

    public WeatherTable(string city)
    {
        _ = city;
    }

    // Columns and Metadata omitted.
}
```

Override the raw constructor methods only when the source shape is generated dynamically or you intentionally do not use `AddTable<T>()`:

```csharp
using Musoq.Schema.Helpers;
using Musoq.Schema.Reflection;

public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
{
    ArgumentNullException.ThrowIfNull(metadataContext);
    return TypeHelper.GetSchemaMethodInfosForType<WeatherTable>(Current);
}

public override SchemaMethodInfo[] GetRawConstructors(
    string methodName,
    SourceMetadataContext metadataContext)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
    return GetRawConstructors(metadataContext)
        .Where(constructor => string.Equals(constructor.MethodName, methodName, StringComparison.Ordinal))
        .ToArray();
}
```

Do not use the retired `GetRawConstructors(RuntimeContext)` signature. Runtime-v2 passes `SourceMetadataContext`.

### 3.9 Predicate Planner and Evaluator

Runtime-v2 predicate pushdown receives `SourcePredicateExpression` records, not parser AST nodes.

```csharp
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Weather;

internal static class WeatherPredicatePlanner
{
    public static SourcePredicateExpression? TryAccept(SourcePredicateExpression? predicate)
    {
        return predicate switch
        {
            null => null,
            SourcePredicateComparison comparison when IsSupportedComparison(comparison) => comparison,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                TryAcceptAnd(logical),
            _ => null
        };
    }

    private static SourcePredicateExpression? TryAcceptAnd(SourcePredicateLogical logical)
    {
        var left = TryAccept(logical.Left);
        var right = TryAccept(logical.Right);

        if (left == null || right == null)
            return null;

        return new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right);
    }

    private static bool IsSupportedComparison(SourcePredicateComparison comparison)
    {
        return comparison.Left is SourcePredicateColumn { Column.Name: nameof(WeatherEntity.City) } &&
               comparison.Right is SourcePredicateLiteral &&
               comparison.Operator is SourcePredicateComparisonOperator.Equal or SourcePredicateComparisonOperator.NotEqual;
    }
}
```

This simple planner accepts a predicate only when the whole supported expression can be applied by the source. If you want partial `AND` pushdown, return both the accepted and residual pieces explicitly from your planner and set `AcceptedPredicate` and `ResidualPredicate` accordingly; never drop the unsupported side.

For in-memory tests or local filtering:

```csharp
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Weather;

internal static class WeatherPredicateEvaluator
{
    public static bool Evaluate(SourcePredicateExpression predicate, WeatherEntity row)
    {
        return predicate switch
        {
            SourcePredicateComparison comparison => EvaluateComparison(comparison, row),
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Evaluate(logical.Left, row) && Evaluate(logical.Right, row),
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.Or } logical =>
                Evaluate(logical.Left, row) || Evaluate(logical.Right, row),
            SourcePredicateIn inPredicate => EvaluateIn(inPredicate, row),
            SourcePredicateNullCheck nullCheck =>
                (EvaluateValue(nullCheck.Expression, row) == null) ^ nullCheck.IsNegated,
            _ => throw new InvalidOperationException($"Unsupported predicate '{predicate.GetType().Name}'.")
        };
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, WeatherEntity row)
    {
        var left = EvaluateValue(comparison.Left, row);
        var right = EvaluateValue(comparison.Right, row);
        var compare = Comparer<object>.Default.Compare(left, right);

        return comparison.Operator switch
        {
            SourcePredicateComparisonOperator.Equal => Equals(left, right),
            SourcePredicateComparisonOperator.NotEqual => !Equals(left, right),
            SourcePredicateComparisonOperator.GreaterThan => compare > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => compare >= 0,
            SourcePredicateComparisonOperator.LessThan => compare < 0,
            SourcePredicateComparisonOperator.LessOrEqual => compare <= 0,
            _ => false
        };
    }

    private static bool EvaluateIn(SourcePredicateIn predicate, WeatherEntity row)
    {
        var value = EvaluateValue(predicate.Expression, row);
        var contains = predicate.Values.Any(item => Equals(EvaluateValue(item, row), value));
        return predicate.IsNegated ? !contains : contains;
    }

    private static object? EvaluateValue(SourcePredicateExpression expression, WeatherEntity row)
    {
        return expression switch
        {
            SourcePredicateColumn column => GetColumnValue(row, column.Column.Name),
            SourcePredicateLiteral literal => literal.Value,
            _ => throw new InvalidOperationException($"Unsupported value expression '{expression.GetType().Name}'.")
        };
    }

    private static object? GetColumnValue(WeatherEntity row, string column)
    {
        return column switch
        {
            nameof(WeatherEntity.City) => row.City,
            nameof(WeatherEntity.ObservedAt) => row.ObservedAt,
            nameof(WeatherEntity.TemperatureC) => row.TemperatureC,
            nameof(WeatherEntity.HumidityPercent) => row.HumidityPercent,
            nameof(WeatherEntity.Condition) => row.Condition,
            _ => throw new InvalidOperationException($"Unsupported column '{column}'.")
        };
    }
}
```

### 3.10 Dynamic Dictionary Rows

Use dictionary rows for sources whose row shape comes from `TABLE`, `COUPLE`, or runtime discovery.

```csharp
public sealed class DynamicRecordTable(ISchemaColumn[] columns) : ISchemaTable
{
    public ISchemaColumn[] Columns => columns;

    public SchemaTableMetadata Metadata { get; } =
        new(typeof(IReadOnlyDictionary<string, object>));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column =>
            string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase));
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns
            .Where(column => string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}

public sealed class DynamicRecordSource(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    : RowSourceBase<IReadOnlyDictionary<string, object?>>
{
    protected override void CollectChunks(IChunkWriter<IReadOnlyDictionary<string, object?>> writer)
    {
        writer.Write(rows);
    }
}
```

When a `TABLE` statement supplies the columns, the schema receives those columns through `metadataContext.AllColumns` and `executionContext.AllColumns`.

```csharp
public override ISchemaTable GetTableByName(
    string name,
    SourceMetadataContext metadataContext,
    params object?[] parameters)
{
    return new DynamicRecordTable(metadataContext.AllColumns.ToArray());
}

public override RowSource<T> GetRowSource<T>(
    string name,
    SourceExecutionContext executionContext,
    params object?[] parameters)
{
    var rows = LoadRows(executionContext.AllColumns);
    return EnsureSourceType<T, IReadOnlyDictionary<string, object?>>(
        name,
        new DynamicRecordSource(rows));
}
```

### 3.11 Read Modifiers and Contract Diagnostics

`TABLE` column modifiers are preserved in `ISchemaColumn.ReadModifiers` during metadata and execution, and in `SourceColumnRef.ReadModifiers` during planning.

Statement order matters in a query batch:

1. `TABLE` definitions first.
2. `COUPLE` statements after the `TABLE` definitions they reference.
3. CTEs and the final query after `TABLE` / `COUPLE`.

Known modifier keys:

| Syntax | Key | Value |
|--------|-----|-------|
| `encoding 'utf-8'` | `encoding` | `utf-8` |
| `culture 'pl-PL'` | `culture` | `pl-PL` |
| `format 'yyyy-MM-dd'` | `format` | `yyyy-MM-dd` |
| `trim` | `trim` | `true` |
| `source codec 'base64'` | `source.codec` | `base64` |

Return contract diagnostics when a contract cannot be honored. `ColumnReadModifiers` is in `Musoq.Schema`; `SourceContractDiagnostic` is in `Musoq.Schema.Optimization`.

```csharp
return new SourceDescriptor
{
    Identity = context.Identity,
    RowType = typeof(IReadOnlyDictionary<string, object>),
    Columns = columns,
    ContractDiagnostics =
    [
        SourceContractDiagnostic.Warning(
            "Encoding modifier 'windows-1250' is ignored by this source.",
            "UnsupportedEncoding") with
        {
            ColumnName = "Name",
            ModifierKey = ColumnReadModifiers.Encoding
        }
    ]
};
```

Diagnostic severity behavior:

| Severity | Engine behavior |
|----------|-----------------|
| `Info` | appears in planning/inspection output |
| `Warning` | reported as `MQ5013_SourceContractWarning` |
| `Error` | reported as `MQ3071_SourceContractError` and stops compilation |

### 3.12 Source Runtime Settings

Runtime settings replace environment-variable-style hardcoding. The datasource declares requirements; the host resolves values. SQL may select a settings profile through `COUPLE`.

Manual declaration:

```csharp
public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
    string name,
    SourceRuntimeSettingsDescribeContext context,
    params object?[] parameters)
{
    return
    [
        new SourceRuntimeSettingRequirement(
            "API_TOKEN",
            Required: true,
            Secret: true,
            SourceRuntimeSettingPhase.All,
            "Token used to call the remote API.")
    ];
}
```

Attribute declaration on table/source constructors also works when using `SchemaBase.AddTable<T>()` and `SchemaBase.AddSource<T>()`:

```csharp
[SourceRuntimeSetting(
    "API_TOKEN",
    Secret = true,
    Description = "Token used to call the remote API.")]
public ApiSource(SourceExecutionContext context)
{
    Context = context;
}
```

During execution:

```csharp
var token = executionContext.SourceRuntimeSettings["API_TOKEN"];
```

Host-side test resolver:

```csharp
public sealed class DictionarySettingsResolver(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> profiles)
    : ISourceRuntimeSettingsResolver
{
    public IReadOnlyDictionary<string, string> Resolve(SourceRuntimeSettingsResolutionRequest request)
    {
        var profile = request.ProfileName ?? "default";
        return profiles.TryGetValue(profile, out var settings)
            ? settings
            : new Dictionary<string, string>();
    }
}
```

SQL:

```sql
couple weather.current with settings prod as CurrentWeather;
select City, TemperatureC from CurrentWeather('Warsaw');
desc settings CurrentWeather;
```

## Phase 4 - XML Documentation (Critical)

XML documentation is mandatory for production plugins. Musoq tooling can read the XML file next to the plugin assembly to determine available schemas, source methods, parameters, tables, and columns without loading the plugin DLL. This is important for plugin indexes, `desc`-style discovery, help systems, offline catalog generation, and safe inspection of untrusted packages.

Runtime-v2 execution still validates metadata through `GetTableByName`, `DescribeSource`, `DescribeSourceRuntimeSettings`, `TryPlanSource`, and `GetRowSource<T>`. The XML file is the static discovery contract; the runtime-v2 schema methods are the execution contract. They must describe the same sources and columns.

### 4.1 Full XML Structure Reference

Enable documentation output in the project:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

Document schema purpose at schema class level and source method shapes on the schema constructor. The XML structure below is the minimum static catalog a Musoq program can parse without loading the DLL:

```csharp
/// <description>
/// Provides current weather observations.
/// </description>
/// <short-description>
/// Weather datasource.
/// </short-description>
/// <project-url>https://example.org/weather-plugin</project-url>
public sealed class WeatherSchema : SchemaBase
{
    private readonly WeatherClient _client;

    /// <virtual-constructors>
    ///   <virtual-constructor>
    ///     <virtual-param>City name, coordinates, or address.</virtual-param>
    ///     <examples>
    ///       <example>
    ///         <from>#weather.current(string city)</from>
    ///         <description>Returns the current weather observation for one city.</description>
    ///         <columns>
    ///           <column name="City" type="string">Resolved city name.</column>
    ///           <column name="ObservedAt" type="DateTimeOffset">Observation timestamp.</column>
    ///           <column name="TemperatureC" type="decimal">Temperature in Celsius.</column>
    ///           <column name="HumidityPercent" type="decimal">Relative humidity from 0 to 100.</column>
    ///           <column name="Condition" type="string">Short weather condition text.</column>
    ///         </columns>
    ///       </example>
    ///     </examples>
    ///   </virtual-constructor>
    /// </virtual-constructors>
    public WeatherSchema(WeatherClient client)
        : base("weather", CreateLibrary())
    {
        _client = client;
        AddTable<WeatherTable>("current");
    }
}
```

For a source with runtime-v2 settings, document the setting names in the example description or in an `environmentVariables` block if your Musoq host still reads that legacy XML node for static help. The authoritative runtime-v2 settings contract remains `DescribeSourceRuntimeSettings`.

```xml
<environmentVariables>
  <environmentVariable name="WEATHER_API_KEY" isRequired="true">API token resolved by the host.</environmentVariable>
</environmentVariables>
```

Also document schema provider, entity properties, and SQL-callable library methods with ordinary XML comments for maintainability.

```csharp
namespace Musoq.DataSources.Weather;

/// <summary>
/// Provides the #weather schema.
/// </summary>
public sealed class WeatherSchemaProvider : ISchemaProvider
{
    /// <summary>
    /// Gets the weather schema.
    /// </summary>
    /// <param name="schema">Schema name. Use weather or #weather.</param>
    /// <returns>Weather schema instance.</returns>
    public ISchema GetSchema(string schema)
    {
        return new WeatherSchema(new WeatherClient());
    }
}
```

### 4.2 Column Type Strings

Use stable type strings in `<column type="...">`. Keep them synchronized with `ISchemaTable.Columns`.

| .NET type | XML `type` value |
|-----------|------------------|
| `string` | `string` |
| `int` | `int` |
| `long` | `long` |
| `double` | `double` |
| `decimal` | `decimal` |
| `bool` | `bool` |
| `DateTime` | `DateTime` |
| `DateTimeOffset` | `DateTimeOffset` |
| `TimeSpan` | `TimeSpan` |
| `Guid` | `Guid` |
| `byte[]` | `byte[]` |
| `string[]` | `string[]` |

For nullable or generic types, use XML-safe names such as `DateTime?`, `IList&lt;string&gt;`, or `IDictionary&lt;string, object&gt;`.

For query-local `TABLE` declarations, the common keyword mapping is:

| TABLE keyword | Runtime column type |
|---------------|---------------------|
| `string` | `string` |
| `byte` | `byte?` |
| `sbyte` | `sbyte?` |
| `short` | `short?` |
| `int` | `int?` |
| `long` | `long?` |
| `ushort` | `ushort?` |
| `uint` | `uint?` |
| `ulong` | `ulong?` |
| `float` | `float?` |
| `double` | `double?` |
| `decimal` / `money` | `decimal?` |
| `bool` / `boolean` / `bit` | `bool?` |
| `char` | `char?` |
| `datetime` | `DateTime?` |
| `datetimeoffset` | `DateTimeOffset?` |
| `timespan` | `TimeSpan?` |
| `guid` | `Guid?` |
| `object` | `object` |

Value types are nullable in `TABLE` context because dynamic sources can omit or null a value.

### 4.3 Dynamic Columns

For dynamic sources, mark the catalog as dynamic because a static XML scanner cannot know the final shape without query-local `TABLE`, `COUPLE`, parameters, or external data:

```xml
<columns isDynamic="true"></columns>
```

The description should explain how callers provide a shape:

```xml
<description>
Reads records using columns declared by a query-local TABLE statement. Column read
modifiers such as encoding, culture, format, trim, and source codec are available
through ISchemaColumn.ReadModifiers during execution.
</description>
```

### 4.4 Multiple Overloads

If the source supports multiple parameter sets, add one `<virtual-constructor>` block per SQL shape.

```xml
/// <virtual-constructors>
///   <virtual-constructor>
///     <examples>
///       <example>
///         <from>#weather.current()</from>
///         <description>Uses the default city from host-resolved settings.</description>
///         <columns>...</columns>
///       </example>
///     </examples>
///   </virtual-constructor>
///   <virtual-constructor>
///     <virtual-param>City name.</virtual-param>
///     <examples>
///       <example>
///         <from>#weather.current(string city)</from>
///         <description>Uses the supplied city.</description>
///         <columns>...</columns>
///       </example>
///     </examples>
///   </virtual-constructor>
/// </virtual-constructors>
```

### 4.5 Where XML Docs Go - Summary

| File | XML docs required |
|------|-------------------|
| `WeatherSchemaProvider.cs` | schema provider purpose |
| `WeatherSchema.cs` class | `<description>`, `<short-description>`, `<project-url>` |
| `WeatherSchema.cs` constructor | `<virtual-constructors>` with every source shape and static columns |
| `WeatherEntity.cs` | important columns, units, nullability |
| `WeatherLibrary.cs` | SQL-callable methods |
| `WeatherClient.cs` | optional, useful for maintainers |

### 4.6 Verifying XML Generation

```powershell
dotnet build --configuration Release --nologo --verbosity quiet
Test-Path .\Musoq.DataSources.Weather\bin\Release\net10.0\Musoq.DataSources.Weather.xml
```

Also verify the XML is packaged next to the DLL:

```powershell
Expand-Archive .\artifacts\Musoq.DataSources.Weather-windows-x64.zip -DestinationPath .\artifacts\verify -Force
Expand-Archive .\artifacts\verify\Plugin.zip -DestinationPath .\artifacts\verify\plugin -Force
Test-Path .\artifacts\verify\plugin\Musoq.DataSources.Weather.xml
```

If the XML file is missing from the package, Musoq tooling may be forced to load the DLL for discovery or may show no table/column catalog at all.

## Phase 5 - Unit Tests

### 5.1 Test Project Structure

```text
Musoq.DataSources.Weather.Tests/
  WeatherSchemaTests.cs
  WeatherQueryTests.cs
  WeatherRuntimeSettingsTests.cs
  WeatherPlanningTests.cs
  TestLoggerResolver.cs
  DictionarySettingsResolver.cs
```

### 5.2 Test Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Musoq.Converter" Version="17.0.0-alpha.1" />
    <PackageReference Include="Musoq.Evaluator" Version="17.0.0-alpha.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.8.2" />
    <PackageReference Include="MSTest.TestFramework" Version="3.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Musoq.DataSources.Weather\Musoq.DataSources.Weather.csproj" />
  </ItemGroup>
</Project>
```

### 5.3 Test Infrastructure

```csharp
using Microsoft.Extensions.Logging;
using Musoq.Evaluator;

namespace Musoq.DataSources.Weather.Tests;

public sealed class TestLoggerResolver : ILoggerResolver
{
    public ILogger ResolveLogger()
    {
        return LoggerFactory
            .Create(static builder => builder.SetMinimumLevel(LogLevel.None))
            .CreateLogger("Tests");
    }
}
```

Compile and run helper:

```csharp
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;

namespace Musoq.DataSources.Weather.Tests;

internal static class QueryRunner
{
    public static Table Run(
        string query,
        ISchemaProvider provider,
        CompilationOptions? options = null)
    {
        var compiled = options == null
            ? InstanceCreator.CompileForExecution(
                query,
                Guid.NewGuid().ToString("N"),
                provider,
                new TestLoggerResolver())
            : InstanceCreator.CompileForExecution(
                query,
                Guid.NewGuid().ToString("N"),
                provider,
                new TestLoggerResolver(),
                options);

        return compiled.Run();
    }
}
```

### 5.4 Functional Query Test

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Weather.Tests;

[TestClass]
public sealed class WeatherQueryTests
{
    [TestMethod]
    public void Current_WhenCityIsPassed_ReturnsWeatherRows()
    {
        var provider = new WeatherSchemaProvider();

        var rows = QueryRunner.Run(
            "select City, TemperatureC from #weather.current('Warsaw')",
            provider);

        Assert.AreEqual("Warsaw", rows[0][0]);
    }
}
```

### 5.5 Schema Description Tests

```csharp
[TestMethod]
public void GetTableByName_WhenCurrentSource_ReturnsTypedWeatherMetadata()
{
    var schema = new WeatherSchema(new WeatherClient());
    var metadataContext = TestContexts.Metadata();

    var table = schema.GetTableByName("current", metadataContext);

    Assert.AreEqual(typeof(WeatherEntity), table.Metadata.TableEntityType);
    Assert.IsNotNull(table.GetColumnByName(nameof(WeatherEntity.City)));
}
```

Verify assembly discovery metadata:

```csharp
using Musoq.Schema.Attributes;

[TestMethod]
public void Assembly_WhenInspected_HasPluginSchemasAttribute()
{
    var attribute = typeof(WeatherSchema).Assembly
        .GetCustomAttributes(typeof(PluginSchemasAttribute), inherit: false)
        .Cast<PluginSchemasAttribute>()
        .SingleOrDefault();

    Assert.IsNotNull(attribute);
    CollectionAssert.Contains(attribute.Schemas.ToArray(), "weather");
}
```

Verify raw constructors for `desc` support:

```csharp
[TestMethod]
public void GetRawConstructors_WhenAskedForCurrent_ReturnsSourceParameters()
{
    var schema = new WeatherSchema(new WeatherClient());
    var metadataContext = TestContexts.Metadata();

    var constructors = schema.GetRawConstructors("current", metadataContext);

    Assert.IsTrue(constructors.Any());
    Assert.IsTrue(constructors.Any(constructor =>
        constructor.ConstructorInfo.Arguments.Any(argument =>
            argument.Name == "city" && argument.Type == typeof(string))));
}
```

Verify `desc` through SQL, not only by direct method calls:

```csharp
[TestMethod]
public void DescSchema_ShouldListCurrentMethod()
{
    var rows = QueryRunner.Run(
        "desc #weather",
        new WeatherSchemaProvider());

    Assert.IsTrue(rows.Any(row => string.Equals((string)row[0], "current", StringComparison.Ordinal)));
}

[TestMethod]
public void DescMethodWithArgs_ShouldReturnColumns()
{
    var rows = QueryRunner.Run(
        "desc #weather.current('Warsaw')",
        new WeatherSchemaProvider());

    Assert.IsTrue(rows.Any(row => string.Equals((string)row[0], nameof(WeatherEntity.City), StringComparison.Ordinal)));
    Assert.IsTrue(rows.Any(row => string.Equals((string)row[0], nameof(WeatherEntity.TemperatureC), StringComparison.Ordinal)));
}
```

Verify generated XML before packaging:

```csharp
[TestMethod]
public void XmlDocumentation_WhenBuilt_ContainsStaticCatalog()
{
    var assemblyPath = typeof(WeatherSchema).Assembly.Location;
    var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

    Assert.IsTrue(File.Exists(xmlPath), $"Missing XML documentation file: {xmlPath}");

    var xml = File.ReadAllText(xmlPath);
    StringAssert.Contains(xml, "virtual-constructors");
    StringAssert.Contains(xml, "#weather.current(string city)");
    StringAssert.Contains(xml, nameof(WeatherEntity.TemperatureC));
}
```

Create a simple metadata context in tests:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Weather.Tests;

internal static class TestContexts
{
    public static SourceMetadataContext Metadata(
        IReadOnlyCollection<ISchemaColumn>? columns = null,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        return new SourceMetadataContext(
            Guid.NewGuid().ToString("N"),
            CancellationToken.None,
            columns ?? [],
            settings ?? new Dictionary<string, string>(),
            NullLogger.Instance);
    }
}
```

### 5.6 Runtime Settings Tests

```csharp
[TestMethod]
public void Query_WhenSettingsProfileIsSelected_PassesResolvedSettingsToSource()
{
    var resolver = new DictionarySettingsResolver(
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["prod"] = new Dictionary<string, string>
            {
                ["WEATHER_API_KEY"] = "secret-token"
            }
        });

    var options = new CompilationOptions(sourceRuntimeSettingsResolver: resolver);
    var query = """
        couple weather.current with settings prod as CurrentWeather;
        select City from CurrentWeather('Warsaw')
        """;

    var rows = QueryRunner.Run(query, new WeatherSchemaProvider(), options);

    Assert.AreEqual("Warsaw", rows[0][0]);
}
```

Also test the settings description snapshot produced during analysis:

```csharp
[TestMethod]
public void DescSettings_WhenSourceDeclaresRequiredSetting_ReturnsRequirement()
{
    var items = InstanceCreator.CreateForAnalyze(
        "select City from #weather.current('Warsaw')",
        Guid.NewGuid().ToString("N"),
        new WeatherSchemaProvider(),
        new TestLoggerResolver());
    var descriptions = items.SourceRuntimeSettingDescriptionsBySourceContextId
        .Values
        .Single();

    Assert.AreEqual("WEATHER_API_KEY", descriptions.Single().Name);
}
```

### 5.7 Planning Tests

Test planning directly:

```csharp
[TestMethod]
public void TryPlanSource_WhenCityPredicateIsUsed_AcceptsPredicate()
{
    var schema = new WeatherSchema(new WeatherClient());
    var request = new SourcePlanRequest
    {
        Identity = SourceIdentity.Empty,
        Predicate = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(nameof(WeatherEntity.City))),
            new SourcePredicateLiteral("Warsaw"))
    };

    var result = schema.TryPlanSource("current", request);

    Assert.IsNotNull(result.AcceptedPredicate);
    Assert.IsNull(result.ResidualPredicate);
}
```

Test planning through SQL using `CompileForInspection` and `desc query` when the host supports it:

```sql
desc query (
    select City, TemperatureC
    from #weather.current('Warsaw')
    where City = 'Warsaw'
    order by ObservedAt desc
    take 10
);
```

### 5.8 TABLE and COUPLE Tests

Use `TABLE` and `COUPLE` for dictionary row sources or explicit dynamic contracts:

```sql
table WeatherRow {
    City: string trim,
    TemperatureC: decimal culture 'en-US',
    ObservedAt: datetimeoffset format 'O'
};

couple weather.current with table WeatherRow and settings prod as CurrentWeather;

select City, TemperatureC
from CurrentWeather('Warsaw')
where TemperatureC > 10;
```

Assert that modifiers are visible in the datasource by recording `metadataContext.AllColumns`, `request.RequiredColumns`, and `executionContext.AllColumns`.

### 5.9 Package Smoke Tests

Add a package smoke test that expands the final zip and checks both metadata layers. This catches the common "works in unit tests, invisible in host" failure.

```csharp
using System.IO.Compression;

[TestMethod]
public void PackageZip_WhenExpanded_ContainsDiscoveryMetadataAndXml()
{
    var packagePath = Path.Combine("artifacts", "Musoq.DataSources.Weather-windows-x64.zip");
    var extractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    ZipFile.ExtractToDirectory(packagePath, extractDir);

    Assert.IsTrue(File.Exists(Path.Combine(extractDir, "EntryPoint.txt")));
    Assert.IsTrue(File.Exists(Path.Combine(extractDir, "Platform.txt")));
    Assert.IsTrue(File.Exists(Path.Combine(extractDir, "Architecture.txt")));
    Assert.IsTrue(File.Exists(Path.Combine(extractDir, "LibraryName.txt")));
    Assert.IsTrue(File.Exists(Path.Combine(extractDir, "Version.txt")));

    var pluginZip = Path.Combine(extractDir, "Plugin.zip");
    Assert.IsTrue(File.Exists(pluginZip));

    var pluginDir = Path.Combine(extractDir, "plugin");
    ZipFile.ExtractToDirectory(pluginZip, pluginDir);

    Assert.IsTrue(File.Exists(Path.Combine(pluginDir, "Musoq.DataSources.Weather.dll")));
    Assert.IsTrue(File.Exists(Path.Combine(pluginDir, "Musoq.DataSources.Weather.xml")));
}
```

## Phase 6 - Build and Package

### 6.1 Package Structure

The proven datasource distribution format is a nested zip archive. The outer zip contains static metadata files. The inner `Plugin.zip` contains the plugin DLL, XML documentation, runtime files, and third-party dependencies.

```text
Musoq.DataSources.Weather-windows-x64.zip
  EntryPoint.txt          # Musoq.DataSources.Weather.dll
  Platform.txt            # windows | linux | macos | alpine
  Architecture.txt        # x64 | arm64
  LibraryName.txt         # Musoq.DataSources.Weather
  Version.txt             # 1.0.0
  Plugin.zip
    Musoq.DataSources.Weather.dll
    Musoq.DataSources.Weather.xml
    Musoq.DataSources.Weather.deps.json
    Musoq.DataSources.Weather.runtimeconfig.json
    ThirdParty.Dependency.dll
```

Confirm the exact package convention used by your Musoq host. If the host uses a registry or a newer plugin command, keep the inner `Plugin.zip` contents and adapt only the outer metadata names.

`Version.txt` must contain the exact project SemVer. Stable versions such as `1.2.3` and prerelease versions such as `1.2.3-alpha.1`, `1.2.3-beta.1`, and `1.2.3-rc.1` are valid. Keep the same exact version in the project file, NuGet package, GitHub release tag, package metadata, and registry `versionHistory`.

Repository registries use schema `1.1`, which is additive over schema `1.0`. Existing clients continue reading `latestVersion`, `releaseTag`, `releaseDate`, `artifacts`, and `versionHistory`. Channel-aware clients can also read `latestStableVersion`, `latestPrereleaseVersion`, and `channels`. Release tags are path-safe:

```text
8.4.8-Musoq.DataSources.Weather
8.4.9-alpha.1-Musoq.DataSources.Weather
```

Use one release tag to publish both NuGet and plugin zip assets:

```powershell
git tag 8.4.9-alpha.1-Musoq.DataSources.Weather
git push origin 8.4.9-alpha.1-Musoq.DataSources.Weather
```

The tag version must match the project `<Version>` exactly. The workflow publishes `.nupkg`, `.snupkg`, all runtime plugin zips, and the channel-aware registry entry from the same release.

To publish a third-party datasource repository, copy the release tooling as a set:

- `scripts/common`
- `scripts/release`
- `scripts/Pack-Plugin.ps1`
- `scripts/Update-PluginRegistry.ps1`
- `scripts/Rollback-PluginReleases.ps1`
- `.github/workflows/release-datasource.yml`

The workflow passes its own `owner/repo` value to the scripts. Consumers can add the generated registry URL:

```text
https://github.com/{owner}/{repo}/releases/download/plugin-registry/plugin-registry.json
```

### 6.2 Excluded Assemblies

Do not ship assemblies provided by the host unless your host explicitly requires self-contained plugins.

Common exclusions:

- `Musoq.Schema.dll`
- `Musoq.Plugins.dll`
- `Musoq.Parser.dll`
- `Musoq.Evaluator.dll`
- `Musoq.Converter.dll`
- `Microsoft.CodeAnalysis*.dll`
- `System.*.dll`
- `Microsoft.Extensions.*.dll`

### 6.3 PowerShell Build Script

`build-package.ps1`:

Save this script in the solution or plugin-repository root and run it from that directory. It assumes the plugin project folder is named the same as `$PluginName`.

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginName,

    [ValidateSet("windows", "linux", "macos", "alpine")]
    [string]$Platform = "windows",

    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ridMap = @{
    "windows" = "win"
    "linux" = "linux"
    "macos" = "osx"
    "alpine" = "linux-musl"
}

$rid = "$($ridMap[$Platform])-$Architecture"
$project = "$PluginName/$PluginName.csproj"
$publishDir = "artifacts/publish/$rid"
$packageId = "$Platform-$Architecture"
$packageDir = "artifacts/package/$packageId"
$pluginZip = Join-Path $packageDir "Plugin.zip"
$finalZip = "artifacts/$PluginName-$packageId.zip"

$excluded = @(
    "Musoq.Schema.dll",
    "Musoq.Plugins.dll",
    "Musoq.Parser.dll",
    "Musoq.Evaluator.dll",
    "Musoq.Converter.dll"
)

Remove-Item $publishDir, $packageDir, $finalZip -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDir, $packageDir | Out-Null

dotnet publish $project `
    --configuration $Configuration `
    --framework net10.0 `
    --runtime $rid `
    --self-contained false `
    --output $publishDir `
    --nologo `
    --verbosity quiet

$required = @(
    "$PluginName.dll",
    "$PluginName.xml"
)

foreach ($name in $required) {
    $path = Join-Path $publishDir $name
    if (-not (Test-Path $path)) {
        throw "Required package file is missing: $path"
    }
}

foreach ($name in $excluded) {
    Remove-Item (Join-Path $publishDir $name) -Force -ErrorAction SilentlyContinue
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $pluginZip -Force

$assemblyPath = Join-Path $publishDir "$PluginName.dll"
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath).ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "1.0.0"
}

Set-Content -Path (Join-Path $packageDir "EntryPoint.txt") -Value "$PluginName.dll" -NoNewline
Set-Content -Path (Join-Path $packageDir "Platform.txt") -Value $Platform -NoNewline
Set-Content -Path (Join-Path $packageDir "Architecture.txt") -Value $Architecture -NoNewline
Set-Content -Path (Join-Path $packageDir "LibraryName.txt") -Value $PluginName -NoNewline
Set-Content -Path (Join-Path $packageDir "Version.txt") -Value $version -NoNewline
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $finalZip -Force

Write-Host "Created $finalZip"
```

### 6.4 Bash Build Script

`build-package.sh`:

Save this script in the solution or plugin-repository root and run it from that directory. It assumes the plugin project folder is named the same as `PLUGIN_NAME`.

```bash
#!/usr/bin/env bash
set -euo pipefail

PLUGIN_NAME="${1:?Plugin name is required}"
PLATFORM="${2:-linux}"
ARCH="${3:-x64}"
CONFIGURATION="${4:-Release}"

case "$PLATFORM" in
  windows) RID_PREFIX="win" ;;
  linux) RID_PREFIX="linux" ;;
  macos) RID_PREFIX="osx" ;;
  alpine) RID_PREFIX="linux-musl" ;;
  *) echo "Unsupported platform: $PLATFORM" >&2; exit 1 ;;
esac

RID="${RID_PREFIX}-${ARCH}"
PACKAGE_ID="${PLATFORM}-${ARCH}"
PROJECT="${PLUGIN_NAME}/${PLUGIN_NAME}.csproj"
PUBLISH_DIR="artifacts/publish/${RID}"
PACKAGE_DIR="artifacts/package/${PACKAGE_ID}"
PLUGIN_ZIP="${PACKAGE_DIR}/Plugin.zip"
FINAL_ZIP="artifacts/${PLUGIN_NAME}-${PACKAGE_ID}.zip"

rm -rf "$PUBLISH_DIR" "$PACKAGE_DIR" "$FINAL_ZIP"
mkdir -p "$PUBLISH_DIR" "$PACKAGE_DIR"

dotnet publish "$PROJECT" \
  --configuration "$CONFIGURATION" \
  --framework net10.0 \
  --runtime "$RID" \
  --self-contained false \
  --output "$PUBLISH_DIR" \
  --nologo \
  --verbosity quiet

test -f "$PUBLISH_DIR/${PLUGIN_NAME}.dll"
test -f "$PUBLISH_DIR/${PLUGIN_NAME}.xml"

rm -f \
  "$PUBLISH_DIR/Musoq.Schema.dll" \
  "$PUBLISH_DIR/Musoq.Plugins.dll" \
  "$PUBLISH_DIR/Musoq.Parser.dll" \
  "$PUBLISH_DIR/Musoq.Evaluator.dll" \
  "$PUBLISH_DIR/Musoq.Converter.dll"

(cd "$PUBLISH_DIR" && zip -qr "../../package/${PACKAGE_ID}/Plugin.zip" .)

printf '%s' "${PLUGIN_NAME}.dll" > "$PACKAGE_DIR/EntryPoint.txt"
printf '%s' "$PLATFORM" > "$PACKAGE_DIR/Platform.txt"
printf '%s' "$ARCH" > "$PACKAGE_DIR/Architecture.txt"
printf '%s' "$PLUGIN_NAME" > "$PACKAGE_DIR/LibraryName.txt"
printf '%s' "1.0.0" > "$PACKAGE_DIR/Version.txt"

(cd "$PACKAGE_DIR" && zip -qr "../../${PLUGIN_NAME}-${PACKAGE_ID}.zip" .)

echo "Created $FINAL_ZIP"
```

## Phase 7 - Import and Install Scripts

### 7.1 PowerShell Install Script

`install-plugin.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command musoq -ErrorAction SilentlyContinue)) {
    throw "The musoq CLI was not found on PATH."
}

musoq datasource import $PackagePath
musoq datasource list
```

### 7.2 Bash Install Script

`install-plugin.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

PACKAGE_PATH="${1:?Package path is required}"

if ! command -v musoq >/dev/null 2>&1; then
  echo "The musoq CLI was not found on PATH." >&2
  exit 1
fi

musoq datasource import "$PACKAGE_PATH"
musoq datasource list
```

If your host has a newer plugin-focused CLI, the equivalent command may be:

```powershell
musoq plugin install .\artifacts\Musoq.DataSources.Weather-windows-x64.zip
musoq plugin list
```

### 7.3 Installing from Registry

If your host supports registries:

```powershell
musoq plugin registry list
musoq plugin install Musoq.DataSources.Weather
```

For private registries:

```powershell
musoq plugin registry add internal https://example.local/musoq/plugins/index.json
musoq plugin install Musoq.DataSources.Weather --registry internal
```

## Appendix A - Troubleshooting and Common Pitfalls

### A.1 Compilation Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `net8.0` target errors | old plugin target | migrate projects to `net10.0` |
| `GetRowSource` override not found | runtime-v1 signature | implement `GetRowSource<T>(string, SourceExecutionContext, params object?[])` |
| `RuntimeContext` type missing | runtime-v1 API | use `SourceMetadataContext` or `SourceExecutionContext` |
| package downgrade/conflict | mixed Musoq versions | align all `Musoq.*` packages to one train |
| XML file missing | docs disabled or file not copied into package | set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and fail packaging when `.xml` is absent |

### A.2 Test Failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| row type mismatch | table metadata and row source type differ | use `SchemaTableMetadata(typeof(TEntity))` and `EnsureSourceType<TRequested, TEntity>()` |
| missing setting diagnostic | resolver did not return required value | test with `CompilationOptions(sourceRuntimeSettingsResolver: ...)` |
| contract warning/error | datasource rejected `TABLE` or read modifier | update `SourceDescriptor.ContractDiagnostics` or query table |
| query compiles but source sees no columns | projection not requested or dynamic table not coupled | use `TABLE` + `COUPLE` or inspect `AllColumns` |
| `desc #schema` lists only `empty` or no source methods | table constructors were not registered | call `AddTable<TTable>("method")` or override runtime-v2 `GetRawConstructors(SourceMetadataContext)` |
| `desc #schema.method('arg')` has wrong parameters | table constructors do not mirror source parameters | add metadata-only table constructors such as `WeatherTable(string city)` |

### A.3 Runtime and Packaging Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| plugin not discovered | missing `[assembly: PluginSchemas("schema")]` or schema name mismatch | add `PluginSchemas`, keep it lowercase, and match the `SchemaBase` name |
| host cannot load plugin | wrong RID or missing dependency | publish for host RID and inspect `Plugin.zip` |
| duplicate assembly load errors | shipped host-provided Musoq DLLs | exclude host assemblies from package |
| host can load plugin but cannot list tables without loading DLL | XML documentation missing from `Plugin.zip` | include `Musoq.DataSources.Name.xml` next to the DLL |
| settings not available during metadata | requirement phase excludes metadata | set `SourceRuntimeSettingPhase.Metadata` or `All` if needed |
| progress UI silent | source never calls progress methods | call begin, rows known, rows read, end |

### A.4 Data Source Specific Issues

| Source type | Runtime-v2 guidance |
|-------------|---------------------|
| HTTP API | declare endpoint/token settings, accept predicates only when API query parameters match semantics |
| database | use source runtime settings for connection strings, accept projection and predicates conservatively |
| files | use dictionary rows for user-declared `TABLE` shapes, honor read modifiers when possible |
| streaming | yield bounded chunks; do not materialize whole input unless required |
| high-volume | use `RowChunk<T>` or chunked arrays/lists; report progress |

## Appendix B - Complete File Reference

Minimum typed plugin:

```text
Musoq.DataSources.Weather/
  Musoq.DataSources.Weather.csproj
  Assembly.cs
  Properties/AssemblyInfo.cs
  WeatherEntity.cs
  WeatherTable.cs
  WeatherRowSource.cs
  WeatherSchema.cs
  WeatherSchemaProvider.cs
  WeatherLibrary.cs
  WeatherClient.cs
  WeatherPredicatePlanner.cs
  WeatherPredicateEvaluator.cs
```

Minimum dynamic plugin additions:

```text
  DynamicRecordTable.cs
  DynamicRecordSource.cs
  ReadModifierValueConverter.cs
```

Minimum test project:

```text
Musoq.DataSources.Weather.Tests/
  Musoq.DataSources.Weather.Tests.csproj
  TestLoggerResolver.cs
  DictionarySettingsResolver.cs
  TestContexts.cs
  WeatherSchemaTests.cs
  WeatherQueryTests.cs
  WeatherRuntimeSettingsTests.cs
  WeatherPlanningTests.cs
  WeatherTableCoupleTests.cs
```

## Appendix C - NuGet Package Version Resolution

### Using dotnet CLI

```powershell
dotnet package search Musoq.Schema --prerelease
dotnet package search Musoq.Converter --prerelease
```

### Using NuGet HTTP API

```powershell
$package = "musoq.schema"
$index = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$package/index.json"
$index.versions | Select-Object -Last 20
```

### Using PowerShell in a Repository

```powershell
Select-String -Path **/*.csproj,Directory.Packages.props,scripts/Versions.props `
    -Pattern "Musoq.Schema|Musoq.Plugins|Musoq.Converter|Musoq.Evaluator"
```

### Package Compatibility Matrix

| Plugin target | Musoq package train | .NET target |
|---------------|---------------------|-------------|
| runtime-v2 current | `17.0.0-alpha.1` train or compatible | `net10.0` |
| runtime-v1 legacy | not covered by this guide | do not use |

Always align `Musoq.Schema`, `Musoq.Plugins`, `Musoq.Converter`, and `Musoq.Evaluator` when they are used together.

## Appendix D - Predicate Pushdown in Runtime V2

### D.1 When to Implement Predicate Pushdown

Implement pushdown only when the external source can apply the same semantics as Musoq. Good candidates:

- API filters with exact equality or range semantics
- SQL database predicates with parameterized queries
- server-side ordering and paging
- expensive columns that can be skipped through accepted projection

Do not accept a predicate if case sensitivity, null behavior, culture, type conversion, or time zone behavior differs and you cannot compensate.

### D.2 Architecture Overview

```text
SQL WHERE / ORDER BY / SKIP / TAKE
  -> SourcePlanRequest
  -> schema.TryPlanSource(...)
  -> SourcePlanResult with accepted and residual work
  -> SourceExecutionContext.Plan
  -> row source applies accepted work or sends it to remote API
  -> engine applies residual work
```

The source must set both the public accepted/residual fields and the `ExecutionPlan` fields consistently.

### D.3 Request and Result Records

```csharp
public sealed record SourcePlanRequest
{
    public required SourceIdentity Identity { get; init; }
    public IReadOnlyDictionary<string, string> SourceRuntimeSettings { get; init; }
    public IReadOnlyList<SourceColumnRef> RequiredColumns { get; init; }
    public SourcePredicateExpression? Predicate { get; init; }
    public IReadOnlyList<OrderByExpression> OrderBy { get; init; }
    public long? Skip { get; init; }
    public long? Take { get; init; }
}

public sealed record SourcePlanResult
{
    public required SourceExecutionPlan ExecutionPlan { get; init; }
    public IReadOnlyList<SourceColumnRef> AcceptedColumns { get; init; }
    public SourcePredicateExpression? AcceptedPredicate { get; init; }
    public SourcePredicateExpression? ResidualPredicate { get; init; }
    public IReadOnlyList<OrderByExpression> AcceptedOrderBy { get; init; }
    public IReadOnlyList<OrderByExpression> ResidualOrderBy { get; init; }
    public long? AcceptedSkip { get; init; }
    public long? ResidualSkip { get; init; }
    public long? AcceptedTake { get; init; }
    public long? ResidualTake { get; init; }
    public CardinalityEstimate? Cardinality { get; init; }
    public IReadOnlyList<OptimizationDiagnostic> Diagnostics { get; init; }
    public IReadOnlyList<SourceContractDiagnostic> ContractDiagnostics { get; init; }
}
```

### D.4 Predicate Expressions

Supported public expression records:

```csharp
public abstract record SourcePredicateExpression;
public sealed record SourcePredicateColumn(SourceColumnRef Column) : SourcePredicateExpression;
public sealed record SourcePredicateLiteral(object? Value) : SourcePredicateExpression;
public sealed record SourcePredicateComparison(SourcePredicateComparisonOperator Operator, SourcePredicateExpression Left, SourcePredicateExpression Right) : SourcePredicateExpression;
public sealed record SourcePredicateLogical(SourcePredicateLogicalOperator Operator, SourcePredicateExpression Left, SourcePredicateExpression Right) : SourcePredicateExpression;
public sealed record SourcePredicateIn(SourcePredicateExpression Expression, IReadOnlyList<SourcePredicateExpression> Values, bool IsNegated = false) : SourcePredicateExpression;
public sealed record SourcePredicateNullCheck(SourcePredicateExpression Expression, bool IsNegated = false) : SourcePredicateExpression;
```

### D.5 API Query Builder

Example builder for a web API:

```csharp
internal sealed class WeatherApiQuery
{
    public string? City { get; init; }
    public long? Skip { get; init; }
    public long? Take { get; init; }
    public string? OrderBy { get; init; }
    public bool Descending { get; init; }
}

internal static class WeatherApiQueryBuilder
{
    public static WeatherApiQuery FromPlan(SourceExecutionPlan plan)
    {
        return new WeatherApiQuery
        {
            City = ExtractCity(plan.AcceptedPredicate),
            Skip = plan.AcceptedSkip,
            Take = plan.AcceptedTake,
            OrderBy = plan.AcceptedOrderBy.FirstOrDefault()?.Column.Name,
            Descending = plan.AcceptedOrderBy.FirstOrDefault()?.Direction == OrderDirection.Descending
        };
    }

    private static string? ExtractCity(SourcePredicateExpression? predicate)
    {
        return predicate switch
        {
            SourcePredicateComparison
            {
                Operator: SourcePredicateComparisonOperator.Equal,
                Left: SourcePredicateColumn { Column.Name: nameof(WeatherEntity.City) },
                Right: SourcePredicateLiteral { Value: string city }
            } => city,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                ExtractCity(logical.Left) ?? ExtractCity(logical.Right),
            _ => null
        };
    }
}
```

### D.6 Best Practices

- Accept only work you truly execute.
- Return unsupported work as residual so the engine can still produce correct results.
- Always consume accepted work from `executionContext.Plan`; do not re-parse SQL.
- Keep source plan properties simple, serializable, and version-tolerant.
- Add direct tests for accepted and residual work.
- Use `CardinalityEstimate.Exact`, `Estimate`, `Bounded`, or `Unknown` when the source has row-count knowledge.

### D.7 Migration From Runtime V1 Predicate Helpers

Old datasource code often parsed a where AST with a helper. Delete that path. Runtime-v2 planning already gives the datasource normalized source expressions.

Migration steps:

1. Remove parser AST dependencies from the plugin.
2. Replace the old helper with a planner that pattern matches `SourcePredicateExpression`.
3. Return `SourcePlanResult` with accepted and residual work.
4. Apply accepted work in the API request or row source using `SourceExecutionContext.Plan`.
5. Test both accepted and rejected predicates through compiled SQL.

Final migration audit:

```powershell
rg -n 'RuntimeContext|QuerySourceInfo|QueryHints|IObjectResolver|EntityResolver|BlockingCollection|WhereNodeHelper|net8\.0|Musoq\.Parser" Version="5\.7\.0|Musoq\.Schema" Version="10\.1\.0' .
```

Hits should appear only in documentation or explicit migration warnings, not in source files.
