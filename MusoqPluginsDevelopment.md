# Musoq Plugin Development Tutorial - Runtime V2

This tutorial teaches how to build and migrate Musoq data source plugins for the runtime-v2 engine. It is self-contained for a plugin repository that does not have the Musoq source tree checked out. The examples mirror the current runtime-v2 shape used by Musoq itself: typed entity rows, dictionary rows for dynamic data, explicit source planning, source runtime settings, `TABLE` / `COUPLE`, read modifiers, and contract diagnostics.

Runtime-v2 only: do not build new plugins against runtime-v1 datasource APIs.

## Table of Contents

1. [Understanding Musoq Plugins](#understanding-musoq-plugins)
2. [Prerequisites and Setup](#prerequisites-and-setup)
3. [Core Concepts and Architecture](#core-concepts-and-architecture)
4. [Building Your First Plugin](#building-your-first-plugin)
5. [Understanding Each Component](#understanding-each-component)
6. [Essential XML Metadata (Critical)](#essential-xml-metadata-critical)
7. [Testing and Validation](#testing-and-validation)
8. [Advanced Runtime V2 Patterns](#advanced-runtime-v2-patterns)
9. [Migration From Runtime V1](#migration-from-runtime-v1)
10. [Best Practices and Common Patterns](#best-practices-and-common-patterns)
11. [Common Use Cases](#common-use-cases)
12. [Summary](#summary)

## Understanding Musoq Plugins

### What is a Musoq Plugin?

A Musoq datasource plugin lets SQL query external data:

```sql
select City, TemperatureC
from #weather.current('Warsaw')
where TemperatureC > 10
order by ObservedAt desc
take 5;
```

The plugin tells Musoq:

- what schema name it provides, such as `weather`
- what source methods exist, such as `current(city)`
- what row type and columns each source returns
- how to produce row chunks during execution
- what source-side work can be accepted, such as filters or paging
- what settings the host must resolve, such as API tokens

### How Plugins Work

At runtime the engine follows this path:

```text
SQL text
  -> #weather.current('Warsaw')
  -> ISchemaProvider.GetSchema("weather")
  -> ISchema.GetTableByName("current", metadataContext, parameters)
  -> ISchema.DescribeSource("current", describeContext, parameters)
  -> ISchema.TryPlanSource("current", request, parameters)
  -> ISchema.GetRowSource<T>("current", executionContext, parameters)
  -> RowSource<T>.Chunks
  -> SQL projection, joins, grouping, ordering, and result rows
```

### Plugin Lifecycle

1. The host loads the plugin assembly.
2. The host creates an `ISchemaProvider`.
3. During query compilation, Musoq asks for table metadata, source descriptions, settings requirements, and planning decisions.
4. During query execution, Musoq asks for a typed `RowSource<T>`.
5. The row source emits chunks as `IReadOnlyList<T>`.

## Prerequisites and Setup

### Required Tools

- .NET SDK `10.0.300` or newer compatible `10.0` feature band
- A compatible Musoq package train; current known train is `17.0.0-alpha.1`
- Visual Studio, Rider, or VS Code with C# support
- A test runner for MSTest, xUnit, or NUnit

Add `global.json` to a standalone repository:

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

### Setting Up Your Development Environment

```powershell
dotnet new sln -n WeatherPlugin
dotnet new classlib -n Musoq.DataSources.Weather -f net10.0
dotnet new mstest -n Musoq.DataSources.Weather.Tests -f net10.0
dotnet sln add Musoq.DataSources.Weather/Musoq.DataSources.Weather.csproj
dotnet sln add Musoq.DataSources.Weather.Tests/Musoq.DataSources.Weather.Tests.csproj
dotnet add Musoq.DataSources.Weather.Tests/Musoq.DataSources.Weather.Tests.csproj reference Musoq.DataSources.Weather/Musoq.DataSources.Weather.csproj
```

Plugin project package references:

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

Test project package references:

```xml
<ItemGroup>
  <PackageReference Include="Musoq.Converter" Version="17.0.0-alpha.1" />
  <PackageReference Include="Musoq.Evaluator" Version="17.0.0-alpha.1" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="MSTest.TestAdapter" Version="3.8.2" />
  <PackageReference Include="MSTest.TestFramework" Version="3.8.2" />
</ItemGroup>
```

## Core Concepts and Architecture

### The Five Essential Components

Runtime-v2 plugins still have a simple mental model, but one old component changes: the resolver/helper maps are no longer the bridge. Typed rows and table metadata are the bridge.

1. **SchemaProvider** - gives the host an `ISchema`.
2. **Schema** - implements source metadata, settings, planning, and row-source creation.
3. **Entity or Dictionary Row** - represents each data row.
4. **Table** - declares columns and row type through `ISchemaTable`.
5. **RowSource** - emits row chunks through `RowSource<T>`.

Optional components:

- **Library** - SQL-callable functions through `LibraryBase`.
- **Client** - talks to an API, database, file system, or SDK.
- **Planner** - translates `SourcePlanRequest` into accepted work.
- **Value converter** - honors `TABLE` read modifiers for dynamic sources.

### Runtime V2 Contracts

The active datasource contract is:

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

Rows are produced through:

```csharp
public abstract class RowSource<T>
{
    public abstract IEnumerable<IReadOnlyList<T>> Chunks { get; }
}

public abstract class RowSourceBase<T> : RowSource<T>
{
    protected abstract void CollectChunks(IChunkWriter<T> writer);
}

public interface IChunkWriter<T>
{
    CancellationToken CancellationToken { get; }
    void Write(IReadOnlyList<T> rows);
}
```

### Typed Rows vs Dynamic Rows

Use typed rows when the plugin owns the schema:

```csharp
public sealed class WeatherEntity
{
    public string City { get; init; } = string.Empty;
    public decimal TemperatureC { get; init; }
}
```

Use dictionary rows when the query owns the schema through `TABLE`:

```csharp
IReadOnlyDictionary<string, object?> row =
    new Dictionary<string, object?>
    {
        ["City"] = "Warsaw",
        ["TemperatureC"] = 21.5m
    };
```

### Data Flow

```text
Schema table metadata:
  column names, indexes, types, row type

Planning:
  required columns, predicate, order, skip, take

Execution context:
  accepted source plan, runtime settings, progress reporter

RowSource:
  chunks of typed rows or dictionary rows
```

## Building Your First Plugin

This walkthrough builds `#weather.current(city)`.

### Step 1: Create the Project Foundation

Directory:

```text
Musoq.DataSources.Weather/
  Musoq.DataSources.Weather.csproj
  Assembly.cs
  WeatherEntity.cs
  WeatherTable.cs
  WeatherRowSource.cs
  WeatherSchema.cs
  WeatherSchemaProvider.cs
  WeatherLibrary.cs
  WeatherClient.cs
```

### Step 2: Configure the Project File

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

The important parts are `EnableDynamicLoading`, `GenerateDocumentationFile`, the XML copy target, and `ExcludeAssets=runtime` on Musoq assemblies that the host provides. Test projects usually do not exclude runtime assets because the test process must load the Musoq assemblies itself.

### Step 3: Register Your Plugin

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

`PluginSchemas("weather")` is critical. Musoq can discover that the plugin owns `#weather` without loading the DLL, then use the XML documentation next to the DLL to inspect available methods and columns.

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

If the target host uses a different plugin factory, adapt this one file. Keep the schema implementation runtime-v2.

### Step 4: Design Your Entity

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

Entity rules:

- Keep properties public and readable.
- Prefer immutable `init` setters or constructor-only values.
- Match column names to property names unless you have a strong reason not to.
- Use concrete primitive-friendly types for queryable values.

### Step 5: Create the Table Definition

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

`SchemaTableMetadata(typeof(WeatherEntity))` is the runtime-v2 row-type contract.

### Step 6: Implement the Client

The client is ordinary application code. Keep host/runtime types out of it.

```csharp
namespace Musoq.DataSources.Weather;

public sealed class WeatherClient
{
    private readonly IReadOnlyDictionary<string, string> _settings;

    public WeatherClient()
        : this(new Dictionary<string, string>())
    {
    }

    private WeatherClient(IReadOnlyDictionary<string, string> settings)
    {
        _settings = settings;
    }

    public WeatherClient WithSettings(IReadOnlyDictionary<string, string> settings)
    {
        return new WeatherClient(settings);
    }

    public IReadOnlyList<WeatherEntity> GetCurrent(string city, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _settings.TryGetValue("WEATHER_API_KEY", out var _);

        return
        [
            new WeatherEntity
            {
                City = city,
                ObservedAt = DateTimeOffset.UtcNow,
                TemperatureC = 21.5m,
                HumidityPercent = 55m,
                Condition = "Clear"
            }
        ];
    }
}
```

### Step 7: Implement the RowSource

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
        var plannedRows = WeatherPlanExecutor.Apply(rows, executionContext.Plan).ToArray();

        executionContext.ReportDataSourceRowsKnown(dataSourceName, plannedRows.Length);
        writer.Write(plannedRows);
        executionContext.ReportDataSourceRowsRead(dataSourceName, plannedRows.Length, plannedRows.Length);
        executionContext.ReportDataSourceEnd(dataSourceName, plannedRows.Length);
    }
}
```

If you already have chunks:

```csharp
public sealed class ExistingChunkSource<T>(IEnumerable<IReadOnlyList<T>> chunks) : RowSource<T>
{
    public override IEnumerable<IReadOnlyList<T>> Chunks =>
        RowChunking.NormalizeSourceChunks(chunks);
}
```

For large materialized arrays or lists, use `RowChunk<T>`:

```csharp
for (var offset = 0; offset < rows.Count; offset += 4096)
{
    yield return new RowChunk<WeatherEntity>(
        rows,
        offset,
        Math.Min(4096, rows.Count - offset));
}
```

### Step 8: Create the Library

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

Usage:

```sql
select City, CelsiusToFahrenheit(TemperatureC)
from #weather.current('Warsaw');
```

### Step 9: Create the SchemaProvider

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

### Step 10: Create the Schema

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

        // Registers constructor metadata for desc #weather and static catalogs.
        AddTable<WeatherTable>(Current);
    }

    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object?[] parameters)
    {
        EnsureCurrent(name);
        return new WeatherTable();
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object?[] parameters)
    {
        EnsureCurrent(name);
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
        EnsureCurrent(name);
        return
        [
            new SourceRuntimeSettingRequirement(
                "WEATHER_API_KEY",
                Required: true,
                Secret: true,
                SourceRuntimeSettingPhase.Execution,
                "API key for the weather provider.")
        ];
    }

    public override SourcePlanResult TryPlanSource(
        string name,
        SourcePlanRequest request,
        params object?[] parameters)
    {
        EnsureCurrent(name);
        return WeatherPlanBuilder.Plan(request);
    }

    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object?[] parameters)
    {
        EnsureCurrent(name);

        if (parameters.Length != 1 || parameters[0] is not string city)
            throw new ArgumentException("#weather.current(city) requires one string argument.");

        return EnsureSourceType<T, WeatherEntity>(
            name,
            new WeatherRowSource(
                _client.WithSettings(executionContext.SourceRuntimeSettings),
                executionContext,
                city));
    }

    private static void EnsureCurrent(string name)
    {
        if (!string.Equals(name, Current, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Source '{name}' is not supported.");
    }

    private static MethodsAggregator CreateLibrary()
    {
        var manager = new MethodsManager();
        manager.RegisterLibraries(new LibraryBase());
        manager.RegisterLibraries(new WeatherLibrary());
        return new MethodsAggregator(manager);
    }
}
```

### Step 11: Add Minimal Planning

Start with reject-all planning:

```csharp
public static SourcePlanResult Plan(SourcePlanRequest request)
{
    return SourcePlanResult.RejectAll(request);
}
```

Then add safe accepted work. This example accepts projection only, and leaves predicate, order, skip, and take to the engine:

```csharp
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Weather;

internal static class WeatherPlanBuilder
{
    public static SourcePlanResult Plan(SourcePlanRequest request)
    {
        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = request.RequiredColumns
            },
            AcceptedColumns = request.RequiredColumns,
            ResidualPredicate = request.Predicate,
            ResidualOrderBy = request.OrderBy,
            ResidualSkip = request.Skip,
            ResidualTake = request.Take,
            Cardinality = CardinalityEstimate.Unknown("Weather source does not know filtered cardinality at compile time.")
        };
    }
}
```

Execution applies only the work accepted into `SourceExecutionContext.Plan`:

```csharp
internal static class WeatherPlanExecutor
{
    public static IEnumerable<WeatherEntity> Apply(
        IEnumerable<WeatherEntity> rows,
        SourceExecutionPlan plan)
    {
        return rows;
    }
}
```

Do not accept `take` or `skip` unless the source also accepts every earlier operation that can change row membership or row order. For example, accepting `take` while leaving `where` or `order by` residual can truncate the wrong rows before the engine applies the remaining work.

## Understanding Each Component

### Component 1: Entity

Typed entity rows are the best default for stable schemas.

Good entity:

```csharp
public sealed class CommitEntity
{
    public string Sha { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public DateTimeOffset Date { get; init; }
    public int FileCount { get; init; }
}
```

Avoid:

- hiding queryable values behind methods
- using ambiguous `object` properties for known primitive values
- keeping old index resolver maps as the source of truth

### Component 2: Table

`ISchemaTable` maps SQL-visible column names to types and row type.

```csharp
public interface ISchemaTable
{
    ISchemaColumn[] Columns { get; }
    SchemaTableMetadata Metadata { get; }
    ISchemaColumn? GetColumnByName(string name);
    ISchemaColumn[] GetColumnsByName(string name);
}
```

`SchemaColumn` constructor:

```csharp
new SchemaColumn("ColumnName", 0, typeof(string))
new SchemaColumn("ColumnName", 0, typeof(string), readModifiers)
```

Table constructors also describe source parameters for `desc #schema.method` and static catalogs. If users call `#weather.current('Warsaw')`, the table should expose a metadata-only constructor with the same public parameters:

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

Register the table in the schema constructor:

```csharp
public WeatherSchema(WeatherClient client)
    : base("weather", CreateLibrary())
{
    _client = client;
    AddTable<WeatherTable>("current");
}
```

`SchemaBase` uses those registrations to implement:

```csharp
SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext);
SchemaMethodInfo[] GetRawConstructors(string methodName, SourceMetadataContext metadataContext);
```

Override those methods only for generated/dynamic source catalogs or schemas that cannot use `AddTable<T>()`. Do not use the retired `GetRawConstructors(RuntimeContext)` signature in runtime-v2 plugins.

### Component 3: RowSource

`RowSource<T>.Chunks` is pull-based. The engine enumerates chunks during execution.

Patterns:

```csharp
public sealed class SingleChunkSource<T>(IReadOnlyList<T> rows) : RowSource<T>
{
    public override IEnumerable<IReadOnlyList<T>> Chunks
    {
        get { yield return rows; }
    }
}
```

```csharp
public sealed class WriterSource<T>(IReadOnlyList<T> rows) : RowSourceBase<T>
{
    protected override void CollectChunks(IChunkWriter<T> writer)
    {
        writer.Write(rows);
    }
}
```

Do not use producer queues or blocking collections in runtime-v2 plugin code. Emit chunks directly.

### Component 4: Schema

`SchemaBase` gives useful defaults:

- table/source constructor registration with `AddTable<T>()` and `AddSource<T>()`
- `DescribeSource` default based on table metadata
- `DescribeSourceRuntimeSettings` from `SourceRuntimeSettingAttribute`
- `TryPlanSource` default reject-all
- row type validation helpers
- library method resolution through `MethodsAggregator`

When a plugin needs explicit control, override the runtime-v2 methods directly.

### Component 5: Library

Library methods are normal C# methods decorated with `[BindableMethod]`.

```csharp
[BindableMethod]
public bool IsFreezing(decimal celsius)
{
    return celsius <= 0m;
}
```

Keep datasource I/O out of library methods. Source I/O belongs in clients and row sources.

## Essential XML Metadata (Critical)

### Why XML Metadata Matters

XML metadata is not just developer-facing documentation. It is a static discovery contract. Musoq tooling can read the XML file next to the plugin DLL to determine available schemas, source methods, parameters, tables, and columns without loading the DLL itself.

That matters for:

- plugin discovery UIs
- `desc`-style table and column catalogs
- generated help
- package review
- offline indexes and registries
- safer inspection of untrusted plugin packages
- autonomous agent repair and maintenance

Runtime-v2 execution still validates behavior through `GetTableByName`, `DescribeSource`, `DescribeSourceRuntimeSettings`, `TryPlanSource`, and `GetRowSource<T>`. The XML file is the static catalog; the runtime-v2 schema methods are the execution contract. They must stay synchronized.

Enable XML docs:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

### XML Metadata Structure

Put the plugin-level description on the schema class and the source catalog on the schema constructor. This is the part Musoq can parse without loading the assembly:

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

For runtime-v2 settings, the authoritative contract is `DescribeSourceRuntimeSettings`. If your Musoq host still reads the legacy XML `environmentVariables` node for static help, document setting names there too, but do not read secrets from environment variables unless your host explicitly requires that.

```xml
<environmentVariables>
  <environmentVariable name="WEATHER_API_KEY" isRequired="true">API token resolved by the host.</environmentVariable>
</environmentVariables>
```

### Understanding Column Types

Use stable type strings in `<column type="...">` and keep them synchronized with `ISchemaTable.Columns`.

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

Value types are nullable in `TABLE` context because dynamic sources can omit or null a value. Still document the intended column type in XML when the source shape is static.

### Dynamic vs Static Columns

Static typed source:

```sql
select City, TemperatureC from #weather.current('Warsaw');
```

Dynamic source with explicit shape:

```sql
table CsvWeather {
    City: string trim,
    TemperatureC: decimal culture 'en-US',
    ObservedAt: datetimeoffset format 'O'
};

couple separatedvalues.comma with table CsvWeather as WeatherCsv;

select City, TemperatureC
from WeatherCsv('./weather.csv', true, 0);
```

For sources whose columns are unknown until the query, parameter, or external data is known, mark the static catalog as dynamic:

```xml
<columns isDynamic="true"></columns>
```

`TABLE` differs from binary/text interpretation schemas. `TABLE` declares a row shape for an existing datasource. Binary/text schemas describe how to parse bytes or text into structured values. AI schemas describe structured inference output and can be combined with `TABLE` or `COUPLE` in larger queries.

### Packaging XML Documentation

The generated XML file must be shipped next to the plugin DLL inside the inner `Plugin.zip`. A common standalone datasource package is a two-level zip:

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

Verify it before publishing:

```powershell
dotnet build --configuration Release --nologo --verbosity quiet
Test-Path .\Musoq.DataSources.Weather\bin\Release\net10.0\Musoq.DataSources.Weather.xml
```

If the XML is missing, Musoq tooling may be unable to list the plugin's tables and columns without loading the DLL.

`Version.txt` must match the exact project SemVer. Stable versions such as `1.2.3` and prerelease versions such as `1.2.3-alpha.1`, `1.2.3-beta.1`, and `1.2.3-rc.1` are valid. Do not strip prerelease suffixes from `Version.txt`, release tags, NuGet packages, or registry history.

Repository plugin registries use the package shape described in `MusoqDotnetPluginsZipSpecification.md`. Registry schema `1.2` is backwards-compatible with schemas `1.0` and `1.1`: `latestVersion`, `releaseTag`, `releaseDate`, `artifacts`, and `versionHistory` remain present for existing clients, while newer clients can read channel data plus per-version runtime compatibility and artifact integrity.

The unified release flow uses one compatible tag for both NuGet and plugin zip assets:

```powershell
git tag 9.0.0-alpha.1-Musoq.DataSources.Weather
git push origin 9.0.0-alpha.1-Musoq.DataSources.Weather
```

The tag must match the project `<Version>` exactly. The workflow uploads `.nupkg`, `.snupkg`, and all runtime plugin zips to the same GitHub release, then updates the registry. Existing clients continue using the registry `releaseTag` and `artifacts` fields.

Portable release scripts should be copied as a set into a datasource repository:

- `scripts/common`
- `scripts/release`
- `scripts/Pack-Plugin.ps1`
- `scripts/Update-PluginRegistry.ps1`
- `.github/workflows/release-datasource.yml`
- `.github/workflows/release-datasources-batch.yml`
- `.github/workflows/rollback-release.yml`
- `.github/workflows/validate-plugin-packages.yml`

The workflow must pass the current GitHub repository as `owner/repo`. The generated registry is published at:

```text
https://github.com/{owner}/{repo}/releases/download/plugin-registry/plugin-registry.json
```

For a copy-ready external repository checklist, see `MusoqThirdPartyDatasourceRepositorySetup.md`.

Rollback is tag-scoped through `scripts/release/Rollback-Release.ps1`. Helper NuGet-only packages that do not implement datasource schemas are not handled by the unified datasource release flow yet.

For coordinated releases, prefer the manual `release-datasources-batch.yml` workflow over pushing many tags. It validates the selected datasource set, restores/builds/tests once, publishes each selected datasource, and updates the registry once.

The default import command for hosts using datasource packages is:

```powershell
musoq datasource import .\artifacts\Musoq.DataSources.Weather-windows-x64.zip
musoq datasource list
```

Some hosts expose the newer plugin-oriented equivalent:

```powershell
musoq plugin install .\artifacts\Musoq.DataSources.Weather-windows-x64.zip
musoq plugin list
```

## Testing and Validation

### Setting Up Your Test Project

Add converter/evaluator packages to compile and execute real SQL:

```xml
<PackageReference Include="Musoq.Converter" Version="17.0.0-alpha.1" />
<PackageReference Include="Musoq.Evaluator" Version="17.0.0-alpha.1" />
```

### Test Logger

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
            .CreateLogger("MusoqTests");
    }
}
```

### Test Context and Query Helpers

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
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

internal static class QueryRunner
{
    public static Table Run(
        string query,
        ISchemaProvider provider,
        CompilationOptions? options = null)
    {
        var compiled = InstanceCreator.CompileForExecution(
            query,
            Guid.NewGuid().ToString("N"),
            provider,
            new TestLoggerResolver(),
            options ?? new CompilationOptions());

        return compiled.Run();
    }
}
```

### Running a Query

```csharp
using Musoq.Converter;

var compiled = InstanceCreator.CompileForExecution(
    "select City from #weather.current('Warsaw')",
    Guid.NewGuid().ToString("N"),
    new WeatherSchemaProvider(),
    new TestLoggerResolver());

var rows = compiled.Run();
```

### Diagnostic Compilation

Use this path for negative tests:

```csharp
var result = InstanceCreator.CompileWithDiagnostics(
    "select MissingColumn from #weather.current('Warsaw')",
    Guid.NewGuid().ToString("N"),
    new WeatherSchemaProvider(),
    new TestLoggerResolver());

Assert.IsFalse(result.Succeeded);
Assert.IsTrue(result.Errors.Count > 0);
```

### Inspection Compilation

Use inspection to verify planning, generated C#, and query description behavior:

```csharp
var inspection = InstanceCreator.CompileForInspection(
    "desc query (select City from #weather.current('Warsaw'))",
    Guid.NewGuid().ToString("N"),
    new WeatherSchemaProvider(),
    new TestLoggerResolver());

StringAssert.Contains(inspection.PlanningText, "weather");
```

### Static Discovery Tests

Test the assembly attribute that lets Musoq find the schema before loading behavior:

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

Test raw constructors and `desc` output:

```csharp
[TestMethod]
public void GetRawConstructors_WhenAskedForCurrent_ReturnsCityParameter()
{
    var schema = new WeatherSchema(new WeatherClient());
    var context = TestContexts.Metadata();

    var constructors = schema.GetRawConstructors("current", context);

    Assert.IsTrue(constructors.Any(constructor =>
        constructor.ConstructorInfo.Arguments.Any(argument =>
            argument.Name == "city" && argument.Type == typeof(string))));
}

[TestMethod]
public void DescMethodWithArgs_ShouldReturnWeatherColumns()
{
    var rows = QueryRunner.Run(
        "desc #weather.current('Warsaw')",
        new WeatherSchemaProvider());

    Assert.IsTrue(rows.Any(row => string.Equals((string)row[0], nameof(WeatherEntity.City), StringComparison.Ordinal)));
    Assert.IsTrue(rows.Any(row => string.Equals((string)row[0], nameof(WeatherEntity.TemperatureC), StringComparison.Ordinal)));
}
```

Test the generated XML file:

```csharp
[TestMethod]
public void XmlDocumentation_WhenBuilt_ContainsVirtualConstructorsAndColumns()
{
    var xmlPath = Path.ChangeExtension(typeof(WeatherSchema).Assembly.Location, ".xml");

    Assert.IsTrue(File.Exists(xmlPath), $"Missing XML documentation file: {xmlPath}");

    var xml = File.ReadAllText(xmlPath);
    StringAssert.Contains(xml, "virtual-constructors");
    StringAssert.Contains(xml, "#weather.current(string city)");
    StringAssert.Contains(xml, nameof(WeatherEntity.TemperatureC));
}
```

### Runtime Settings Test

```csharp
using Musoq.Evaluator;
using Musoq.Schema.Optimization;

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

[TestMethod]
public void Current_WhenSettingsProfileIsUsed_ReceivesResolvedToken()
{
    var resolver = new DictionarySettingsResolver(
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["prod"] = new Dictionary<string, string>
            {
                ["WEATHER_API_KEY"] = "token"
            }
        });

    var options = new CompilationOptions(sourceRuntimeSettingsResolver: resolver);
    var query = """
        couple weather.current with settings prod as CurrentWeather;
        select City from CurrentWeather('Warsaw')
        """;

    var compiled = InstanceCreator.CompileForExecution(
        query,
        Guid.NewGuid().ToString("N"),
        new WeatherSchemaProvider(),
        new TestLoggerResolver(),
        options);

    var rows = compiled.Run();

    Assert.AreEqual("Warsaw", rows[0][0]);
}
```

### TABLE / COUPLE and Read Modifier Test

```sql
table LegacyWeather {
    City: string encoding 'utf-8' trim,
    TemperatureC: decimal culture 'en-US',
    ObservedAt: datetimeoffset format 'O',
    Payload: string source codec 'base64'
};

couple weather.dynamic with table LegacyWeather as WeatherRows;

select City, TemperatureC from WeatherRows();
```

Datasource checks:

- `metadataContext.AllColumns` contains columns with read modifiers in `GetTableByName`.
- `SourcePlanRequest.RequiredColumns` contains `SourceColumnRef.ReadModifiers`.
- `executionContext.AllColumns` contains modifiers in `GetRowSource<T>`.
- unsupported modifiers produce `SourceContractDiagnostic.Warning` or `Error`.

Statement order matters in a query batch:

1. Put `TABLE` definitions first.
2. Put `COUPLE` statements after the `TABLE` definitions they reference.
3. Put CTEs and the final query after `TABLE` / `COUPLE`.

### Package Smoke Test

After building the plugin package, expand it and verify the static metadata and XML documentation are present:

```csharp
using System.IO.Compression;

[TestMethod]
public void PackageZip_WhenExpanded_ContainsMetadataAndXml()
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

## Advanced Runtime V2 Patterns

### Source Planning

`SourcePlanRequest` contains possible source-side work:

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
```

`SourcePlanResult` tells the engine what was accepted and what remains residual:

```csharp
return new SourcePlanResult
{
    ExecutionPlan = new SourceExecutionPlan
    {
        Identity = request.Identity,
        AcceptedColumns = request.RequiredColumns,
        AcceptedPredicate = request.Predicate,
        AcceptedOrderBy = request.OrderBy,
        AcceptedSkip = request.Skip,
        AcceptedTake = request.Take,
        Properties = new Dictionary<string, object?>
        {
            ["Strategy"] = "RemoteApi"
        }
    },
    AcceptedColumns = request.RequiredColumns,
    AcceptedPredicate = request.Predicate,
    AcceptedOrderBy = request.OrderBy,
    AcceptedSkip = request.Skip,
    AcceptedTake = request.Take,
    Cardinality = CardinalityEstimate.Unknown("Remote API does not expose a count endpoint.")
};
```

Only accept work the source actually applies, and preserve query ordering semantics. A source can safely accept `take` or `skip` only when every predicate and order dependency before that slice is also accepted by the same source plan. The full `SourcePlanResult` also exposes `Diagnostics` and `ContractDiagnostics`; use those collections for planning warnings and source contract errors instead of throwing when the query can be diagnosed.

### Predicate Expression Pattern Matching

```csharp
private static bool IsCityEquals(SourcePredicateExpression expression)
{
    return expression is SourcePredicateComparison
    {
        Operator: SourcePredicateComparisonOperator.Equal,
        Left: SourcePredicateColumn { Column.Name: nameof(WeatherEntity.City) },
        Right: SourcePredicateLiteral { Value: string }
    };
}
```

Supported expression records include:

- `SourcePredicateColumn`
- `SourcePredicateLiteral`
- `SourcePredicateComparison`
- `SourcePredicateLogical`
- `SourcePredicateIn`
- `SourcePredicateNullCheck`

### Source Runtime Settings

Manual declaration:

```csharp
new SourceRuntimeSettingRequirement(
    "API_TOKEN",
    Required: true,
    Secret: true,
    SourceRuntimeSettingPhase.All,
    "Token used by the source.")
```

Attribute declaration:

```csharp
[SourceRuntimeSetting("API_TOKEN", Secret = true, Description = "Token used by the source.")]
public ApiSource(SourceExecutionContext context)
{
}
```

SQL profile selection:

```sql
couple weather.current with settings prod as CurrentWeather;
desc settings CurrentWeather;
```

### Dynamic Dictionary Rows

Dynamic rows are useful for CSV, JSON, loosely typed APIs, and table-valued input where the query declares shape.

```csharp
public sealed class DynamicTable(ISchemaColumn[] columns) : ISchemaTable
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

public sealed class DynamicSource(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    : RowSourceBase<IReadOnlyDictionary<string, object?>>
{
    protected override void CollectChunks(IChunkWriter<IReadOnlyDictionary<string, object?>> writer)
    {
        writer.Write(rows);
    }
}
```

### Read Modifiers and Conversion

Known modifier constants are available through `ColumnReadModifiers`:

- `ColumnReadModifiers.Encoding`
- `ColumnReadModifiers.Culture`
- `ColumnReadModifiers.Format`
- `ColumnReadModifiers.Trim`
- `ColumnReadModifiers.SourcePrefix`

Example conversion:

```csharp
using System.Globalization;
using Musoq.Schema;

private static object? ConvertValue(object? rawValue, ISchemaColumn column)
{
    if (rawValue == null)
        return null;

    var modifiers = column.ReadModifiers;
    var value = rawValue;

    if (value is string text)
    {
        if (modifiers.ContainsKey(ColumnReadModifiers.Trim))
            text = text.Trim();

        var culture = modifiers.TryGetValue(ColumnReadModifiers.Culture, out var cultureName)
            ? CultureInfo.GetCultureInfo(cultureName)
            : CultureInfo.InvariantCulture;

        var targetType = Nullable.GetUnderlyingType(column.ColumnType) ?? column.ColumnType;
        return targetType == typeof(string)
            ? text
            : Convert.ChangeType(text, targetType, culture);
    }

    return value;
}
```

### Contract Diagnostics

Use `SourceContractDiagnostic` for table/source mismatches:

```csharp
using Musoq.Schema;
using Musoq.Schema.Optimization;

SourceContractDiagnostic.Error(
    "Only utf-8 encoding is supported by #weather.dynamic().",
    "UnsupportedEncoding") with
{
    ColumnName = "City",
    ModifierKey = ColumnReadModifiers.Encoding
};
```

Warnings become `MQ5013_SourceContractWarning`. Errors become `MQ3071_SourceContractError`.

### Progress Reporting

```csharp
executionContext.ReportDataSourceBegin("weather.current");
executionContext.ReportDataSourceRowsKnown("weather.current", totalRows);
executionContext.ReportDataSourceRowsRead("weather.current", rowsRead, totalRows);
executionContext.ReportDataSourceEnd("weather.current", rowsRead);
```

Progress belongs in row source execution, not metadata or planning.

## Migration From Runtime V1

### Retired API Map

These names must not appear in active runtime-v2 source code:

| Runtime-v1 API | Runtime-v2 replacement |
|----------------|------------------------|
| `RuntimeContext` | `SourceMetadataContext` or `SourceExecutionContext` |
| `QuerySourceInfo` | `SourceIdentity`, `SourceDescriptor`, `SourceExecutionPlan` |
| `QueryHints` | `SourcePlanRequest` and `SourcePlanResult` |
| `IObjectResolver` | typed entity properties or dictionary rows |
| `EntityResolver` | table metadata plus row type |
| `BlockingCollection` chunks | `IEnumerable<IReadOnlyList<T>>` chunks |
| non-generic `GetRowSource(...)` | `GetRowSource<T>(...)` |
| `WhereNodeHelper` | `SourcePredicateExpression` planner |

### Migration Checklist

1. Upgrade projects to `net10.0`.
2. Align all `Musoq.*` package versions to one runtime-v2 train.
3. Replace old table helpers with `ISchemaTable` and `SchemaTableMetadata(typeof(TEntity))`.
4. Replace resolver maps with typed properties or dictionary row access.
5. Replace old row producers with `RowSource<T>`, `RowSourceBase<T>`, and `IChunkWriter<T>`.
6. Replace non-generic source creation with `GetRowSource<T>`.
7. Move environment variables and secrets to source runtime settings.
8. Replace predicate AST pushdown with `TryPlanSource`.
9. Consume accepted work from `SourceExecutionContext.Plan`.
10. Add compiled query tests for typed rows, dynamic rows, settings, planning, and diagnostics.

### Old Helper Migration

Old plugins often had a helper containing:

- name-to-index map
- index-to-object map
- resolver object

Runtime-v2 does not need those maps for typed rows. Convert them into table metadata:

```csharp
public ISchemaColumn[] Columns { get; } =
[
    new SchemaColumn(nameof(MyEntity.Id), 0, typeof(int)),
    new SchemaColumn(nameof(MyEntity.Name), 1, typeof(string))
];

public SchemaTableMetadata Metadata { get; } = new(typeof(MyEntity));
```

### Old Environment Variable Migration

Before:

```csharp
var token = Environment.GetEnvironmentVariable("API_TOKEN");
```

After:

```csharp
public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(...)
{
    return
    [
        new SourceRuntimeSettingRequirement(
            "API_TOKEN",
            Required: true,
            Secret: true,
            SourceRuntimeSettingPhase.Execution,
            "API token.")
    ];
}

var token = executionContext.SourceRuntimeSettings["API_TOKEN"];
```

### Old Predicate Pushdown Migration

Before: parse query AST nodes inside the datasource.

After: pattern match source predicate records:

```csharp
private static string? TryGetAcceptedId(SourcePredicateExpression? predicate)
{
    return predicate switch
    {
        SourcePredicateComparison
        {
            Operator: SourcePredicateComparisonOperator.Equal,
            Left: SourcePredicateColumn { Column.Name: "Id" },
            Right: SourcePredicateLiteral { Value: string id }
        } => id,
        _ => null
    };
}
```

### Migration Audit

Run:

```powershell
rg -n 'RuntimeContext|QuerySourceInfo|QueryHints|IObjectResolver|EntityResolver|BlockingCollection|WhereNodeHelper|net8\.0|Musoq\.Parser" Version="5\.7\.0|Musoq\.Schema" Version="10\.1\.0' .
```

Allowed hits:

- this retired API table
- migration warnings
- changelog notes

Disallowed hits:

- active source code
- active tests using old contracts
- project files targeting old framework or stale fixed packages

## Best Practices and Common Patterns

### Golden Rules

1. Use typed rows for stable schemas.
2. Use dictionary rows deliberately for query-declared or dynamic schemas.
3. Keep table metadata and row source type identical.
4. Include `[assembly: PluginSchemas("schema")]` and keep it aligned with `SchemaBase`.
5. Generate and package XML documentation next to the plugin DLL.
6. Register table constructors with `AddTable<T>()` or override runtime-v2 `GetRawConstructors`.
7. Return reject-all planning until pushdown is correct.
8. Treat settings values as host-resolved secrets.
9. Emit bounded chunks; avoid full materialization for large sources.
10. Return residual work for anything the source does not apply.
11. Use contract diagnostics for unsupported table contracts or modifiers.
12. Write compiled SQL, `desc`, XML, and package smoke tests.

### HTTP API Pattern

```text
Schema settings:
  API_TOKEN, BASE_URL, TENANT

Planning:
  accept equality/range filters the API supports
  accept take only if API has limit
  accept skip only if API has offset or cursor semantics that match

Execution:
  build request from SourceExecutionContext.Plan
  stream pages into chunks
  report progress when total count is known
```

### Database Pattern

```text
Schema settings:
  connection string or named connection profile

Planning:
  translate accepted predicates into parameterized SQL
  accept projection to reduce selected columns
  accept order/skip/take only with deterministic order

Execution:
  use DbDataReader
  convert rows into typed entities or dictionaries
  chunk at a fixed size
```

### File Processing Pattern

```text
Schema:
  typed source for known file metadata
  dictionary source for TABLE-declared file records

Read modifiers:
  encoding, culture, format, trim, source-specific codec

Execution:
  stream file lines or records
  avoid loading large files into memory
```

### Caching Pattern

Cache only stable metadata and reusable clients. Do not cache query-specific settings or execution plans globally unless the cache key includes the source context and settings profile.

Good:

```csharp
private static readonly Lazy<MethodsAggregator> CachedLibrary = new(CreateLibrary);
```

Risky:

```csharp
private static IReadOnlyDictionary<string, string> LastSettings;
```

### Error Handling

Fail fast for invalid source names and parameters:

```csharp
if (parameters.Length != 1 || parameters[0] is not string city)
    throw new ArgumentException("#weather.current(city) requires one string city argument.");
```

Use `SourceContractDiagnostic` when the query contract is invalid but discovered during metadata/planning.

## Common Use Cases

### Web API Data Source

```sql
couple github.issues with settings prod as Issues;

select Number, Title, State
from Issues('owner', 'repo')
where State = 'open'
order by Number desc
take 50;
```

Runtime-v2 features used:

- settings profile for token
- predicate pushdown for `State`
- order/take pushdown if API supports it
- typed rows for issue fields

### CSV or Legacy File Data Source

```sql
table InvoiceRow {
    Id: string trim,
    IssuedAt: datetime format 'yyyy-MM-dd',
    Amount: decimal culture 'pl-PL',
    Payload: string source codec 'base64'
};

couple separatedvalues.comma with table InvoiceRow as Invoices;

select Id, Amount
from Invoices('./invoices.csv', true, 0)
where Amount > 1000;
```

Runtime-v2 features used:

- dictionary rows
- `TABLE`
- read modifiers
- contract diagnostics

### Database Data Source

```sql
couple postgres.table with settings reporting as Orders;

select Id, CustomerId, Total
from Orders('orders')
where Total > 100
order by CreatedAt desc
take 100;
```

Runtime-v2 features used:

- settings profile for connection
- predicate/order/take planning
- typed rows or dictionary rows depending on table discovery

### AI Interpretation With TABLE / COUPLE

AI schemas can be used in a query that also consumes table-shaped sources:

```sql
ai InvoiceSummary {
    Vendor: string required,
    Total: decimal required,
    Currency: enum('USD', 'EUR', 'PLN')
}

table InvoiceFile {
    FileName: string,
    ContentBase64: string
};

couple files.records with table InvoiceFile as Files;

select f.FileName, summary.Vendor, summary.Total
from Files('./invoices') f
cross apply Infer(f.ContentBase64, InvoiceSummary) summary;
```

Here `TABLE` describes the file datasource rows. The AI schema describes the structured inference output.

## Summary

A runtime-v2 datasource plugin is built around explicit metadata and typed chunk production:

- `ISchemaProvider` resolves schemas.
- `ISchema` handles metadata, settings, planning, and execution.
- `ISchemaTable` declares columns and row type.
- `RowSource<T>` emits `IReadOnlyList<T>` chunks.
- `SourcePlanRequest` and `SourcePlanResult` replace old hint and AST pushdown paths.
- Source runtime settings replace hardcoded environment configuration.
- `TABLE` / `COUPLE` and read modifiers make dynamic sources explicit and testable.

For migrations, remove the old helper/resolver/pushdown APIs first, then rebuild the datasource as typed rows or deliberate dictionary rows with runtime-v2 tests.
