# Musoq .NET Data Source Plugin — Autonomous Development Guide

This document is the **single source of truth** for an AI agent to autonomously build, test, package, and install a Musoq .NET data source plugin **without access to the Musoq.DataSources repository**. It unifies plugin development, testing, and the packaging/distribution (zip specification) into one self-contained reference.

> **Audience**: AI coding agents performing autonomous implementation.
> **Scope**: .NET plugin development only. No Python.

---

## Table of Contents

1. [Phase 0 — Pre-Flight Checks](#phase-0--pre-flight-checks)
2. [Phase 1 — Scaffolded Execution Plan](#phase-1--scaffolded-execution-plan)
3. [Phase 2 — Plugin Architecture Overview](#phase-2--plugin-architecture-overview)
4. [Phase 3 — Step-by-Step Implementation](#phase-3--step-by-step-implementation)
5. [Phase 4 — XML Documentation (Critical)](#phase-4--xml-documentation-critical)
6. [Phase 5 — Unit Tests](#phase-5--unit-tests)
7. [Phase 6 — Build & Package (Zip Specification)](#phase-6--build--package-zip-specification)
8. [Phase 7 — Import / Install Scripts](#phase-7--import--install-scripts)
9. [Appendix A — Troubleshooting & Common Pitfalls](#appendix-a--troubleshooting--common-pitfalls)
10. [Appendix B — Complete File Reference](#appendix-b--complete-file-reference)
11. [Appendix C — NuGet Package Version Resolution](#appendix-c--nuget-package-version-resolution)
12. [Appendix D — Predicate Pushdown for Web API Sources](#appendix-d--predicate-pushdown-for-web-api-sources)

---

## Phase 0 — Pre-Flight Checks

Before writing any code, the agent **must** determine its working context.

### 0.1 Detect Existing Solution

```
CHECK: Does a *.sln file exist in the workspace root?
  YES → You are inside an existing Musoq.DataSources repository.
        • Reuse the existing Musoq.DataSources.Tests.Common project.
        • Match the Musoq.* NuGet package versions already used by sibling projects (inspect any existing .csproj).
        • Add your new projects to the existing .sln via `dotnet sln add`.
  NO  → You are creating a standalone plugin from scratch.
        • You must create a new solution file.
        • You must resolve the latest Musoq NuGet package versions yourself (see Appendix C).
        • You must create your own test helper infrastructure (see Phase 5).
```

### 0.2 Determine Musoq Package Versions

**Never hardcode Musoq package versions.** Always resolve them:

1. If inside an existing repo: read version numbers from any sibling `.csproj` file.
2. If standalone: query NuGet for the latest stable versions of:
   - `Musoq.Parser`
   - `Musoq.Plugins`
   - `Musoq.Schema`
   - `Musoq.Evaluator`
   - `Musoq.Converter`

See [Appendix C](#appendix-c--nuget-package-version-resolution) for how to query NuGet programmatically.

### 0.3 Verify Prerequisites

- .NET 8.0 SDK or later installed (`dotnet --version`)
- Target framework: `net8.0`

---

## Phase 1 — Scaffolded Execution Plan

The agent should begin by building its own detailed execution plan from this high-level scaffold. Each item marked with `[FILL]` requires the agent to expand with specifics for its target data source.

```
EXECUTION PLAN
==============

1. DESIGN DECISIONS
   1.1 Schema name (lowercase, used in SQL as #schemaname.method())    [FILL]
   1.2 Table/method names (e.g., "file", "query", "list")              [FILL]
   1.3 Entity design — what columns to expose                          [FILL]
   1.4 Constructor parameters — what the user passes in SQL            [FILL]
   1.5 Third-party NuGet dependencies                                  [FILL]
   1.6 Environment variables (if any, with isRequired flags)           [FILL]
   1.7 Predicate pushdown (for web APIs — see Appendix D)              [FILL: yes/no]

2. PROJECT STRUCTURE
   2.1 Plugin project: Musoq.DataSources.{Name}/                      [FILL]
   2.2 Test project:   Musoq.DataSources.{Name}.Tests/                [FILL]
   2.3 List all files to create                                        [FILL]

3. IMPLEMENTATION ORDER
   3.1 Create .csproj files (plugin + tests)
   3.2 Create AssemblyInfo.cs + Assembly.cs
   3.3 Create Entity class(es)
   3.4 Create TableHelper class(es)
   3.5 Create Table class(es)
   3.6 Create RowSource class(es)
   3.7 Create Library class (even if empty)
   3.8 Create SchemaProvider class
   3.9 Create Schema class (with full XML documentation)
   3.10 Build and fix compilation errors
   3.11 [IF WEB API] Create WhereNodeHelper + QueryBuilder (Appendix D)

4. TESTING
   4.1 Create test data / fixtures                                     [FILL]
   4.2 Create functional query tests                                   [FILL]
   4.3 Create describe (desc) tests                                    [FILL]
   4.4 Create edge case tests                                          [FILL]
   4.5 Run all tests, fix failures

5. PACKAGING
   5.1 Create build-package script (PowerShell + Bash)
   5.2 Create install script
   5.3 Verify package structure

6. FINAL VERIFICATION
   6.1 Clean build from scratch
   6.2 All tests green
   6.3 Package creation succeeds
```

---

## Phase 2 — Plugin Architecture Overview

Every Musoq plugin consists of exactly **7 files** (minimum) across **5 logical components**:

```
Musoq.DataSources.{Name}/
├── Musoq.DataSources.{Name}.csproj    # Project configuration
├── AssemblyInfo.cs                     # Schema registration
├── Assembly.cs                         # InternalsVisibleTo for tests
├── Entities/
│   └── {Name}Entity.cs                # Data model (Component 1)
├── Tables/
│   ├── {Name}TableHelper.cs           # Column mappings (Component 2)
│   └── {Name}Table.cs                 # Table metadata (Component 3)
├── Sources/
│   └── {Name}RowSource.cs             # Data fetcher (Component 4)
├── {Name}Library.cs                   # Custom SQL functions (Component 5, can be empty)
├── {Name}SchemaProvider.cs            # Schema factory
└── {Name}Schema.cs                    # Main orchestrator with XML docs
```

### Data Flow

```
SQL: SELECT Col FROM #schema.method('arg')
  ↓
1. Musoq looks up schema name via [assembly: PluginSchemas("schema")]
2. SchemaProvider.GetSchema() → returns Schema instance
3. Schema.GetTableByName("method") → returns Table (column metadata)
4. Schema.GetRowSource("method", ..., 'arg') → creates RowSource
5. RowSource.CollectChunks() → fetches data, creates EntityResolver<Entity> objects
6. Each EntityResolver uses TableHelper's NameToIndexMap + IndexToMethodAccessMap
7. Musoq applies SQL operations (WHERE, GROUP BY, ORDER BY, etc.)
8. Results returned to user
```

---

## Phase 3 — Step-by-Step Implementation

### 3.1 Project File (`.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Product>Musoq</Product>
    <Description>[FILL: Description of what this plugin does]</Description>
    <PackageProjectUrl>[FILL: GitHub URL]</PackageProjectUrl>
    <PackageLicenseFile>LICENSE</PackageLicenseFile>
    <PackageTags>[FILL: comma-separated tags]</PackageTags>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../LICENSE" Pack="true" Visible="false" PackagePath="" />
  </ItemGroup>

  <!-- CRITICAL: Without this target, XML documentation for NuGet dependencies
       will NOT be copied to the output directory, and Musoq won't see the
       full metadata at runtime. -->
  <Target Name="_ResolveCopyLocalNuGetPackageXmls" AfterTargets="ResolveReferences">
    <ItemGroup>
      <ReferenceCopyLocalPaths
        Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')"
        Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)' != '' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
    </ItemGroup>
  </Target>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
    <!-- Musoq.Parser and Musoq.Schema MUST have ExcludeAssets=runtime because
         these DLLs are provided by the host. Including them causes assembly
         loading conflicts at runtime. -->
    <PackageReference Include="Musoq.Converter" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Evaluator" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Parser" Version="[RESOLVE]">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Musoq.Plugins" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Schema" Version="[RESOLVE]">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <!-- [FILL: Add your third-party NuGet dependencies here] -->
  </ItemGroup>

</Project>
```

**Critical details:**

| Setting | Why |
|---------|-----|
| `EnableDynamicLoading` | Allows Musoq host to load plugin at runtime |
| `GenerateDocumentationFile` | Generates the `.xml` file containing your XML doc comments |
| `_ResolveCopyLocalNuGetPackageXmls` target | Copies XML docs from NuGet packages to output — without this, Musoq can't read dependency metadata |
| `ExcludeAssets=runtime` on Parser/Schema | These DLLs are provided by the Musoq host; including them causes version conflicts |

### 3.2 Assembly Registration Files

**`AssemblyInfo.cs`** — Registers the schema name that users will reference in SQL:

```csharp
using Musoq.Schema.Attributes;

[assembly: PluginSchemas("[FILL: schema-name-lowercase]")]
```

The schema name is what appears after `#` in SQL queries: `#schemaname.method()`.

**`Assembly.cs`** — Exposes internal types to the test project:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Musoq.DataSources.[FILL].Tests")]
```

This is necessary because RowSource, Table, TableHelper are typically `internal` classes, and tests need access.

### 3.3 Entity Class

The entity is a plain C# class. Each public property decorated with `[EntityProperty]` becomes a SQL column.

```csharp
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.[FILL].Entities;

/// <summary>
/// [FILL: Description of what this entity represents]
/// </summary>
public class [FILL]Entity
{
    /// <summary>
    /// [FILL: Column description]
    /// </summary>
    [EntityProperty]
    public string Name { get; set; } = string.Empty;
    
    // [FILL: Add more properties as needed]
    // Supported types: string, int, long, double, decimal, bool, DateTime, DateTime?,
    //                  byte[], string[], and other basic .NET types.
    // For nullable reference types, use the ? suffix: string?, DateTime?, etc.
}
```

**Design guidelines:**

- Use simple .NET types that SQL understands.
- Mark every property that should be a column with `[EntityProperty]`.
- Add XML `<summary>` doc comments to every property.
- Use `string.Empty` as default for strings (not null).
- Use nullable types (`DateTime?`) when data might not exist.

### 3.4 TableHelper Class

This static class provides three lookup structures for fast column access:

```csharp
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.[FILL].Entities;

namespace Musoq.DataSources.[FILL].Tables;

internal static class [FILL]TableHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<[FILL]Entity, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static [FILL]TableHelper()
    {
        // Maps column names to integer indices.
        // The index must match between all three structures.
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof([FILL]Entity.Name), 0 },
            // [FILL: Add entries for all entity properties, incrementing the index]
        };

        // Maps integer indices to lambda accessors that extract the value from an entity.
        IndexToMethodAccessMap = new Dictionary<int, Func<[FILL]Entity, object?>>
        {
            { 0, entity => entity.Name },
            // [FILL: Add entries matching NameToIndexMap]
        };

        // Defines column metadata (name, index, .NET type) for Musoq's query engine.
        Columns =
        [
            new SchemaColumn(nameof([FILL]Entity.Name), 0, typeof(string)),
            // [FILL: Add entries matching above. Type must match the entity property type.]
        ];
    }
}
```

**The three structures MUST be consistent:**
- Same number of entries in all three.
- Index N in `NameToIndexMap` must correspond to index N in `IndexToMethodAccessMap` and `Columns`.
- The `typeof()` in `SchemaColumn` must match the actual property return type.

### 3.5 Table Class

Implements `ISchemaTable` — this is what Musoq queries for column metadata:

```csharp
using Musoq.Schema;

namespace Musoq.DataSources.[FILL].Tables;

internal class [FILL]Table : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = [FILL]TableHelper.Columns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(Entities.[FILL]Entity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name)!;
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}
```

### 3.6 RowSource Class

This is where data fetching happens. It extends `RowSourceBase<TEntity>`:

```csharp
using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.[FILL].Entities;
using Musoq.DataSources.[FILL].Tables;

namespace Musoq.DataSources.[FILL].Sources;

internal class [FILL]RowSource : RowSourceBase<[FILL]Entity>
{
    private const string SourceName = "[FILL: lowercase source identifier]";
    private readonly string _parameterFromSql;  // [FILL: parameters from constructor]
    private readonly RuntimeContext _runtimeContext;

    // Constructor parameters (after RuntimeContext) become the SQL method parameters.
    // e.g. for #schema.method('arg1', 42):
    //   public MyRowSource(string arg1, int arg2, RuntimeContext ctx)
    // The parameter order matters — it matches positional args in SQL.
    // RuntimeContext can be in any position; Musoq injects it automatically.
    public [FILL]RowSource(string parameterFromSql, RuntimeContext runtimeContext)
    {
        _parameterFromSql = parameterFromSql;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(
        BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            const int chunkSize = 1000;
            var endWorkToken = _runtimeContext.EndWorkToken;

            // [FILL: Your data-fetching logic here]
            // For file-based sources, check File.Exists first and return early if not found.
            // For API-based sources, make HTTP calls here.

            var list = new List<EntityResolver<[FILL]Entity>>(chunkSize);

            foreach (var item in /* [FILL: your data enumeration] */)
            {
                if (endWorkToken.IsCancellationRequested)
                    return;

                var entity = new [FILL]Entity
                {
                    // [FILL: Map source data to entity properties]
                };

                list.Add(new EntityResolver<[FILL]Entity>(
                    entity,
                    [FILL]TableHelper.NameToIndexMap,
                    [FILL]TableHelper.IndexToMethodAccessMap));

                totalRowsProcessed++;

                // When chunk is full, flush it to Musoq and start a new list.
                if (list.Count >= chunkSize)
                {
                    chunkedSource.Add(list, endWorkToken);
                    list = new List<EntityResolver<[FILL]Entity>>(chunkSize);
                }
            }

            // Don't forget the last partial chunk!
            if (list.Count > 0)
            {
                chunkedSource.Add(list, endWorkToken);
            }
        }
        finally
        {
            // ALWAYS report end, even on failure. Use finally block.
            _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
    }
}
```

**Key patterns:**

1. **Always call `ReportDataSourceBegin`/`ReportDataSourceEnd`** — wrap in try/finally.
2. **Check `endWorkToken.IsCancellationRequested`** in loops to support query cancellation.
3. **Chunk your data** — don't accumulate everything in one list for large datasets.
4. **Flush the last partial chunk** — a common bug is forgetting to add the remaining items.
5. **For file-based sources** — check `File.Exists()` and return early (empty result) if file is missing. Do not throw.

### 3.7 Library Class

Even if you have no custom SQL functions, you must create an empty library:

```csharp
using Musoq.Plugins;

namespace Musoq.DataSources.[FILL];

/// <summary>
/// [FILL] helper methods
/// </summary>
public class [FILL]Library : LibraryBase
{
    // Add [BindableMethod] methods here for custom SQL functions.
    // Leave empty if no custom functions are needed.
}
```

### 3.8 SchemaProvider Class

A simple factory that returns your Schema instance:

```csharp
using Musoq.Schema;

namespace Musoq.DataSources.[FILL];

/// <summary>
/// Provides the requested schema
/// </summary>
public class [FILL]SchemaProvider : ISchemaProvider
{
    /// <summary>
    /// Get schema based on provided name
    /// </summary>
    /// <param name="schema">Schema name</param>
    /// <returns>Requested schema</returns>
    public ISchema GetSchema(string schema)
    {
        return new [FILL]Schema();
    }
}
```

### 3.9 Schema Class

The schema is the main orchestrator. Its **XML documentation on the constructor** is critical — Musoq uses it for discovery, help, and parameter validation.

```csharp
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Exceptions;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using Musoq.DataSources.[FILL].Sources;
using Musoq.DataSources.[FILL].Tables;

namespace Musoq.DataSources.[FILL];

/// <description>
/// [FILL: Multi-line description of what this plugin does]
/// </description>
/// <short-description>
/// [FILL: One-line summary]
/// </short-description>
/// <project-url>[FILL: URL]</project-url>
public class [FILL]Schema : SchemaBase
{
    private const string SchemaName = "[FILL: lowercase schema name matching AssemblyInfo]";

    /// <virtual-constructors>
    /// [FILL: See Phase 4 for the full XML documentation structure]
    /// </virtual-constructors>
    public [FILL]Schema()
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    /// Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(
        string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "[FILL: method name]" => new [FILL]Table(),
            _ => throw new TableNotFoundException(nameof(name))
        };
    }

    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="interCommunicator">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(
        string name, RuntimeContext interCommunicator, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "[FILL: method name]" => new [FILL]RowSource(
                (string)parameters[0],  // [FILL: cast parameters to correct types]
                interCommunicator),
            _ => throw new SourceNotFoundException(nameof(name))
        };
    }

    /// <summary>
    /// Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();
        constructors.AddRange(
            TypeHelper.GetSchemaMethodInfosForType<[FILL]RowSource>("[FILL: method name]"));
        return constructors.ToArray();
    }

    /// <summary>
    /// Gets raw constructor information for a specific data source method.
    /// </summary>
    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            "[FILL: method name]" => TypeHelper
                .GetSchemaMethodInfosForType<[FILL]RowSource>("[FILL: method name]"),
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: [FILL: comma-separated list of method names]")
        };
    }

    /// <summary>
    /// Gets raw constructor information for all data source methods in the schema.
    /// </summary>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return TypeHelper
            .GetSchemaMethodInfosForType<[FILL]RowSource>("[FILL: method name]");
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new [FILL]Library();
        methodsManager.RegisterLibraries(library);
        return new MethodsAggregator(methodsManager);
    }
}
```

---

## Phase 4 — XML Documentation (Critical)

The XML documentation on the Schema constructor is **not optional**. Musoq parses it at runtime for:
- Schema discovery (`desc #schemaname`)
- Method signatures (`desc #schemaname.method`)
- Column metadata (`desc #schemaname.method('arg')`)
- Environment variable requirements
- IntelliSense / help systems

### 4.1 Full XML Structure Reference

Here is the complete XML structure placed as a doc comment on the Schema **constructor**:

```xml
/// <virtual-constructors>
///   <virtual-constructor>
///     <virtual-param>Description of parameter 1</virtual-param>
///     <virtual-param>Description of parameter 2</virtual-param>
///     <examples>
///       <example>
///         <from>
///           <environmentVariables>
///             <environmentVariable name="VAR_NAME" isRequired="true">Description</environmentVariable>
///             <environmentVariable name="OPTIONAL_VAR" isRequired="false">Description (default: value)</environmentVariable>
///           </environmentVariables>
///           #schemaname.method(string param1, int param2)
///         </from>
///         <description>What this method does</description>
///         <columns>
///           <column name="ColumnName" type="string">Column description</column>
///           <column name="OtherColumn" type="int">Other description</column>
///         </columns>
///       </example>
///     </examples>
///   </virtual-constructor>
/// </virtual-constructors>
```

### 4.2 Column Type Strings

Use these exact type strings in the `type` attribute:

| .NET Type | XML `type` value |
|-----------|-----------------|
| `string` | `string` |
| `int` | `int` |
| `long` | `long` |
| `double` | `double` |
| `decimal` | `decimal` |
| `bool` | `bool` |
| `DateTime` | `DateTime` |
| `DateTime?` | `DateTime?` |
| `byte[]` | `byte[]` |
| `string[]` | `string[]` |

For generic types, use XML entities: `IList&lt;string&gt;`, `IDictionary&lt;string, object&gt;`.

### 4.3 Dynamic Columns

If columns are determined at runtime (e.g., database queries, CSV headers):

```xml
<columns isDynamic="true"></columns>
```

### 4.4 Multiple Overloads

Add multiple `<virtual-constructor>` blocks for different parameter combinations:

```xml
/// <virtual-constructors>
///   <virtual-constructor>
///     <examples>
///       <example>
///         <from>#schema.method()</from>
///         <description>No-argument version</description>
///         <columns>...</columns>
///       </example>
///     </examples>
///   </virtual-constructor>
///   <virtual-constructor>
///     <virtual-param>File path</virtual-param>
///     <examples>
///       <example>
///         <from>#schema.method(string path)</from>
///         <description>Single-argument version</description>
///         <columns>...</columns>
///       </example>
///     </examples>
///   </virtual-constructor>
/// </virtual-constructors>
```

### 4.5 Where XML Docs Go — Summary

| Location | Content |
|----------|---------|
| Class-level on Schema | `<description>`, `<short-description>`, `<project-url>` |
| Constructor of Schema | `<virtual-constructors>` with all method signatures |
| Entity properties | `<summary>` on each property |
| All public methods | Standard `<summary>`, `<param>`, `<returns>` |

### 4.6 Verifying XML Generation

After building, confirm the `.xml` file exists alongside the `.dll`:

```bash
dotnet build
# Check for: bin/Debug/net8.0/Musoq.DataSources.{Name}.xml
```

If the `.xml` file is missing, ensure `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is in your `.csproj`.

---

## Phase 5 — Unit Tests

### 5.1 Test Project Structure

```
Musoq.DataSources.{Name}.Tests/
├── Musoq.DataSources.{Name}.Tests.csproj
├── {Name}Tests.cs                    # Functional query tests
└── {Name}SchemaDescribeTests.cs      # Schema discovery tests
```

### 5.2 Test Project File (`.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.8.2" />
    <PackageReference Include="MSTest.TestFramework" Version="3.8.2" />
    <PackageReference Include="Musoq.Converter" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Evaluator" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Parser" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Plugins" Version="[RESOLVE]" />
    <PackageReference Include="Musoq.Schema" Version="[RESOLVE]" />
    <!-- [FILL: third-party packages needed for test data creation] -->
  </ItemGroup>

  <ItemGroup>
    <!-- If inside existing repo, reference the shared test common project -->
    <ProjectReference Include="..\Musoq.DataSources.Tests.Common\Musoq.DataSources.Tests.Common.csproj" />
    <ProjectReference Include="..\Musoq.DataSources.{Name}\Musoq.DataSources.{Name}.csproj" />
  </ItemGroup>

</Project>
```

### 5.3 Test Infrastructure — When Inside Existing Repo

When inside the existing Musoq.DataSources repository, you can use the shared `Musoq.DataSources.Tests.Common` project which provides:

**`InstanceCreatorHelpers.CompileForExecution()`** — Compiles and prepares a SQL query for execution:

```csharp
public static CompiledQuery CompileForExecution(
    string script,                // The SQL query string
    string assemblyName,          // Unique name (use Guid.NewGuid().ToString())
    ISchemaProvider schemaProvider, // Your SchemaProvider instance
    IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> environmentVariables)
```

**`EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables()`** — Creates mocked env vars:

```csharp
// No specific env vars:
var envVars = EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables();

// With specific env vars:
var envVars = EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(
    new Dictionary<string, string> { ["API_KEY"] = "test-key" });
```

**`Culture.ApplyWithDefaultCulture()`** — Sets test culture to `pl-PL` for consistent results.

### 5.4 Test Infrastructure — When Standalone (No Existing Repo)

If you're building outside the repository, you must create these helper classes yourself. Here is the complete implementation:

**`InstanceCreatorHelpers.cs`:**

```csharp
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Converter;
using Musoq.Converter.Build;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Tests.Common;

public static class InstanceCreatorHelpers
{
    private static ILoggerResolver DefaultLoggerResolver => new VoidLoggerResolver();

    private static CompilationOptions CompilationOptions { get; } =
        new(ParallelizationMode.Full, usePrimitiveTypeValidation: false);

    public static CompiledQuery CompileForExecution(
        string script,
        string assemblyName,
        ISchemaProvider schemaProvider,
        IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> environmentVariables,
        ILoggerResolver loggerResolver = null)
    {
        loggerResolver ??= DefaultLoggerResolver;

        var compiledQuery = InstanceCreator.CompileForExecution(
            script,
            assemblyName,
            schemaProvider,
            loggerResolver,
            () => new CreateTree(
                new TransformTree(
                    new TurnQueryIntoRunnableCode(null), loggerResolver)),
            items =>
            {
                items.PositionalEnvironmentVariables = environmentVariables;
                items.CreateBuildMetadataAndInferTypesVisitor = (provider, columns, _) =>
                    new BuildMetadataAndInferTypesForTestsVisitor(
                        provider, columns, environmentVariables, CompilationOptions,
                        loggerResolver.ResolveLogger<BuildMetadataAndInferTypesForTestsVisitor>());
            });

        var runnableField = compiledQuery.GetType().GetRuntimeFields()
            .FirstOrDefault(f => f.Name.Contains("runnable"));
        var runnable = (IRunnable)runnableField?.GetValue(compiledQuery);

        if (runnable == null)
            throw new InvalidOperationException("Runnable is null.");

        runnable.Logger = loggerResolver
            .ResolveLogger<BuildMetadataAndInferTypesForTestsVisitor>();

        return compiledQuery;
    }

    private class VoidLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger()
        {
            return new Mock<ILogger>().Object;
        }

        public ILogger<T> ResolveLogger<T>()
        {
            return new Mock<ILogger<T>>().Object;
        }
    }
}
```

**`BuildMetadataAndInferTypesForTestsVisitor.cs`:**

```csharp
using Microsoft.Extensions.Logging;
using Musoq.Evaluator;
using Musoq.Evaluator.Visitors;
using Musoq.Parser.Nodes.From;
using Musoq.Schema;

namespace Musoq.DataSources.Tests.Common;

public class BuildMetadataAndInferTypesForTestsVisitor(
    ISchemaProvider provider,
    IReadOnlyDictionary<string, string[]> columns,
    IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> defaultEnvironmentVariables,
    CompilationOptions compilationOptions,
    ILogger<BuildMetadataAndInferTypesForTestsVisitor> logger)
    : BuildMetadataAndInferTypesVisitor(provider, columns, logger, compilationOptions)
{
    protected override IReadOnlyDictionary<string, string> RetrieveEnvironmentVariables(
        uint position, SchemaFromNode node)
    {
        var emptyEnvironmentVariables = new Dictionary<string, string>();

        if (defaultEnvironmentVariables.TryGetValue(position, out var variables))
        {
            foreach (var variable in variables)
            {
                emptyEnvironmentVariables.TryAdd(variable.Key, variable.Value);
            }
        }

        InternalPositionalEnvironmentVariables.TryAdd(position, emptyEnvironmentVariables);

        return emptyEnvironmentVariables;
    }
}
```

**`EnvironmentVariablesHelpers.cs`:**

```csharp
using Moq;

namespace Musoq.DataSources.Tests.Common;

public static class EnvironmentVariablesHelpers
{
    public static IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>>
        CreateMockedEnvironmentVariables()
    {
        var mock = new Mock<IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>>>();
        mock.Setup(f => f[It.IsAny<uint>()])
            .Returns(new Dictionary<string, string>());
        return mock.Object;
    }

    public static IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>>
        CreateMockedEnvironmentVariables(IReadOnlyDictionary<string, string> variables)
    {
        var mock = new Mock<IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>>>();
        var data = new Dictionary<uint, IReadOnlyDictionary<string, string>>();
        for (uint i = 0; i <= 100; i++) data[i] = variables;

        mock.Setup(x => x.GetEnumerator()).Returns(() => data.GetEnumerator());
        mock.Setup(x => x.Keys).Returns(data.Keys);
        mock.Setup(x => x[It.IsAny<uint>()]).Returns((uint index) => data[index]);
        mock.Setup(x => x.TryGetValue(It.IsAny<uint>(),
                out It.Ref<IReadOnlyDictionary<string, string>>.IsAny))
            .Returns((uint key, out IReadOnlyDictionary<string, string> val) =>
                data.TryGetValue(key, out val));

        return mock.Object;
    }
}
```

**`Culture.cs`:**

```csharp
using System.Globalization;

namespace Musoq.DataSources.Tests.Common;

public static class Culture
{
    public static CultureInfo DefaultCulture { get; } = CultureInfo.GetCultureInfo("pl-PL");

    public static void ApplyWithDefaultCulture() => Apply(DefaultCulture);

    public static void Apply(CultureInfo culture)
    {
        CultureInfo.CurrentCulture
            = CultureInfo.CurrentUICulture
            = CultureInfo.DefaultThreadCurrentCulture
            = CultureInfo.DefaultThreadCurrentUICulture
            = culture;
    }
}
```

Additional NuGet packages needed for the test common project when standalone:

```xml
<PackageReference Include="Moq" Version="4.20.72" />
```

### 5.5 Functional Query Tests

These tests run actual SQL queries through the Musoq engine against your plugin.

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.[FILL].Tests;

[TestClass]
public class [FILL]Tests
{
    // Static constructor sets culture for consistent test results
    static [FILL]Tests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    // Helper to compile and prepare a query
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new [FILL]SchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    // Escape backslashes in file paths for SQL string literals
    private static string EscapePath(string path)
    {
        return path.Replace("\\", "\\\\");
    }

    // --- Test data setup ---
    // Use [ClassInitialize] to create test fixtures (temp files, etc.)
    // Use [ClassCleanup] to clean them up
    // [FILL: Create test data appropriate for your data source]

    [TestMethod]
    public void SelectAll_ShouldReturnExpectedRows()
    {
        var query = "SELECT Column1, Column2 FROM #schema.method('[FILL: test arg]')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        // Verify column metadata
        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Column1", table.Columns.ElementAt(0).ColumnName);

        // Verify row count
        Assert.AreEqual([FILL: expected count], table.Count);

        // Verify specific values
        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "[FILL: expected value]"));
    }

    [TestMethod]
    public void WhereClause_ShouldFilterCorrectly()
    {
        var query = @"SELECT Column1 FROM #schema.method('[FILL]')
                      WHERE Column2 = 'value'";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual([FILL: expected filtered count], table.Count);
    }

    [TestMethod]
    public void Count_ShouldWork()
    {
        var query = "SELECT Count(Column1) FROM #schema.method('[FILL]')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual([FILL: expected], (int)table.First().Values[0]);
    }

    [TestMethod]
    public void EmptySource_ShouldReturnNoRows()
    {
        // Test with non-existent file / empty source
        var query = "SELECT Column1 FROM #schema.method('[FILL: empty/missing source]')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(0, table.Count);
    }
}
```

### 5.6 Schema Describe Tests

These test the `desc #schema` query variants that exercise `GetRawConstructors`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.[FILL].Tests;

[TestClass]
public class [FILL]SchemaDescribeTests
{
    static [FILL]SchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new [FILL]SchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #[FILL: schema name]";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        // desc #schema returns columns: Name, Param 0, Param 1, ...
        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        // Should list all methods
        Assert.AreEqual([FILL: expected method count], table.Count);

        var row = table.First(row => (string)row[0] == "[FILL: method name]");
        Assert.AreEqual("[FILL: paramName]: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescMethod_ShouldReturnMethodSignature()
    {
        var query = "desc #[FILL].method";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("[FILL: method name]", (string)table.First()[0]);
    }

    [TestMethod]
    public void DescMethodWithArgs_ShouldReturnTableSchema()
    {
        // [FILL: Create a temp test fixture if needed]

        var query = "desc #[FILL].method('[FILL: test arg]')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        // desc with args returns 3 columns and one row per entity property
        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        // Verify expected columns are present
        Assert.IsTrue(columnNames.Contains("Name"));
        // [FILL: Check all expected column names]
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #[FILL].unknownmethod";

        try
        {
            var vm = CreateAndRunVirtualMachine(query);
            vm.Run();
            Assert.Fail("Should have thrown");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Assert.IsTrue(message.Contains("unknownmethod", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(
                message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Available data sources", StringComparison.OrdinalIgnoreCase));
        }
    }
}
```

### 5.7 Adding to Existing Solution

If inside the existing Musoq.DataSources repo:

```bash
cd <repo-root>
dotnet sln add Musoq.DataSources.{Name}/Musoq.DataSources.{Name}.csproj
dotnet sln add Musoq.DataSources.{Name}.Tests/Musoq.DataSources.{Name}.Tests.csproj
```

---

## Phase 6 — Build & Package (Zip Specification)

The distribution format is a **nested zip archive**. The outer zip contains metadata files and an inner `Plugin.zip` with the actual binaries.

### 6.1 Package Structure

```
Musoq.DataSources.{Name}-{platform}-{arch}.zip        ← Outer zip
├── EntryPoint.txt          # Content: "Musoq.DataSources.{Name}.dll"
├── Platform.txt            # Content: "windows" | "linux" | "macos" | "alpine"
├── Architecture.txt        # Content: "x64" | "arm64"
├── Version.txt             # (Optional) Content: "1.0.0"
├── LibraryName.txt         # (Optional) Content: "Musoq.DataSources.{Name}"
└── Plugin.zip              ← Inner zip
    ├── Musoq.DataSources.{Name}.dll
    ├── Musoq.DataSources.{Name}.deps.json
    ├── Musoq.DataSources.{Name}.runtimeconfig.json
    ├── Musoq.DataSources.{Name}.xml
    ├── ThirdParty.Dependency.dll
    ├── third-party-notices/
    │   ├── report.json
    │   └── ThirdParty.Dependency/
    │       └── license.txt
    └── ...
```

### 6.2 Excluded Assemblies

The following **MUST NOT** be in `Plugin.zip` — they are provided by the Musoq host:

- `Musoq.Schema.dll`
- `Musoq.Parser.dll`
- `Musoq.Plugins.dll`

### 6.3 PowerShell Build Script

Save as `build-package.ps1` in the plugin project directory:

```powershell
<#
.SYNOPSIS
    Builds and packages a Musoq data source plugin according to the Musoq zip specification.

.PARAMETER PluginName
    The full plugin name (e.g., "Musoq.DataSources.MyPlugin")

.PARAMETER Platform
    Target platform: windows, linux, macos, alpine

.PARAMETER Architecture
    Target architecture: x64, arm64

.PARAMETER Configuration
    Build configuration (default: Release)

.EXAMPLE
    .\build-package.ps1 -PluginName "Musoq.DataSources.MyPlugin" -Platform "windows" -Architecture "x64"
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$PluginName,

    [Parameter(Mandatory=$true)]
    [ValidateSet("windows", "linux", "macos", "alpine")]
    [string]$Platform,

    [Parameter(Mandatory=$true)]
    [ValidateSet("x64", "arm64")]
    [string]$Architecture,

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Map platform to RID prefix
$ridMap = @{
    "windows" = "win"
    "linux"   = "linux"
    "macos"   = "osx"
    "alpine"  = "linux-musl"
}
$rid = "$($ridMap[$Platform])-$Architecture"

$projectDir   = $PSScriptRoot
$projectFile  = Join-Path $projectDir "$PluginName.csproj"
$publishDir   = Join-Path $projectDir "publish"
$packageDir   = Join-Path $projectDir "package"
$pluginZip    = Join-Path $packageDir "Plugin.zip"
$finalZip     = Join-Path $projectDir "$PluginName-$Platform-$Architecture.zip"

# Excluded assemblies (provided by Musoq host)
$excludedAssemblies = @(
    "Musoq.Schema.dll",
    "Musoq.Parser.dll",
    "Musoq.Plugins.dll"
)

Write-Host "=== Building $PluginName for $Platform-$Architecture ===" -ForegroundColor Cyan

# Step 1: Clean
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
if (Test-Path $finalZip)   { Remove-Item $finalZip -Force }
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# Step 2: Publish
Write-Host "Publishing..." -ForegroundColor Yellow
dotnet publish $projectFile -c $Configuration -r $rid --no-self-contained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Step 3: Remove excluded assemblies
Write-Host "Removing excluded assemblies..." -ForegroundColor Yellow
foreach ($excluded in $excludedAssemblies) {
    $path = Join-Path $publishDir $excluded
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "  Removed: $excluded"
    }
}

# Step 4: Create Plugin.zip (inner archive)
Write-Host "Creating Plugin.zip..." -ForegroundColor Yellow
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $pluginZip -Force

# Step 5: Create metadata files
Write-Host "Creating metadata files..." -ForegroundColor Yellow
Set-Content -Path (Join-Path $packageDir "EntryPoint.txt")    -Value "$PluginName.dll" -NoNewline
Set-Content -Path (Join-Path $packageDir "Platform.txt")      -Value $Platform -NoNewline
Set-Content -Path (Join-Path $packageDir "Architecture.txt")  -Value $Architecture -NoNewline
Set-Content -Path (Join-Path $packageDir "LibraryName.txt")   -Value $PluginName -NoNewline

# Optional: extract version from DLL
$dllPath = Join-Path $publishDir "$PluginName.dll"
if (Test-Path $dllPath) {
    $version = (Get-Item $dllPath).VersionInfo.ProductVersion
    if ($version) {
        # Strip any +commitsha suffix
        $version = $version -replace '\+.*$', ''
        Set-Content -Path (Join-Path $packageDir "Version.txt") -Value $version -NoNewline
        Write-Host "  Version: $version"
    }
}

# Step 6: Create final package (outer archive)
Write-Host "Creating final package..." -ForegroundColor Yellow
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $finalZip -Force

# Cleanup
Remove-Item $publishDir -Recurse -Force
Remove-Item $packageDir -Recurse -Force

Write-Host "=== Package created: $finalZip ===" -ForegroundColor Green
Write-Host "Contents:"
# Verify
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($finalZip)
$zip.Entries | ForEach-Object { Write-Host "  $_" }
$zip.Dispose()
```

### 6.4 Bash Build Script

Save as `build-package.sh`:

```bash
#!/bin/bash
set -euo pipefail

# Usage: ./build-package.sh <PluginName> <platform> <architecture> [configuration]
# Example: ./build-package.sh Musoq.DataSources.MyPlugin linux x64

PLUGIN_NAME="${1:?Usage: $0 <PluginName> <platform> <architecture> [configuration]}"
PLATFORM="${2:?Specify platform: windows|linux|macos|alpine}"
ARCHITECTURE="${3:?Specify architecture: x64|arm64}"
CONFIGURATION="${4:-Release}"

# Map platform to RID prefix
case "$PLATFORM" in
    windows) RID_PREFIX="win" ;;
    linux)   RID_PREFIX="linux" ;;
    macos)   RID_PREFIX="osx" ;;
    alpine)  RID_PREFIX="linux-musl" ;;
    *)       echo "Invalid platform: $PLATFORM"; exit 1 ;;
esac
RID="${RID_PREFIX}-${ARCHITECTURE}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_FILE="${SCRIPT_DIR}/${PLUGIN_NAME}.csproj"
PUBLISH_DIR="${SCRIPT_DIR}/publish"
PACKAGE_DIR="${SCRIPT_DIR}/package"
FINAL_ZIP="${SCRIPT_DIR}/${PLUGIN_NAME}-${PLATFORM}-${ARCHITECTURE}.zip"

# Excluded assemblies
EXCLUDED_ASSEMBLIES=("Musoq.Schema.dll" "Musoq.Parser.dll" "Musoq.Plugins.dll")

echo "=== Building ${PLUGIN_NAME} for ${PLATFORM}-${ARCHITECTURE} ==="

# Clean
rm -rf "$PUBLISH_DIR" "$PACKAGE_DIR" "$FINAL_ZIP"
mkdir -p "$PACKAGE_DIR"

# Publish
echo "Publishing..."
dotnet publish "$PROJECT_FILE" -c "$CONFIGURATION" -r "$RID" --no-self-contained -o "$PUBLISH_DIR"

# Remove excluded assemblies
echo "Removing excluded assemblies..."
for asm in "${EXCLUDED_ASSEMBLIES[@]}"; do
    rm -f "${PUBLISH_DIR}/${asm}"
done

# Create Plugin.zip
echo "Creating Plugin.zip..."
(cd "$PUBLISH_DIR" && zip -r "${PACKAGE_DIR}/Plugin.zip" .)

# Create metadata files
printf '%s' "${PLUGIN_NAME}.dll"  > "${PACKAGE_DIR}/EntryPoint.txt"
printf '%s' "$PLATFORM"          > "${PACKAGE_DIR}/Platform.txt"
printf '%s' "$ARCHITECTURE"      > "${PACKAGE_DIR}/Architecture.txt"
printf '%s' "$PLUGIN_NAME"       > "${PACKAGE_DIR}/LibraryName.txt"

# Extract version from published DLL metadata (best-effort)
VERSION=$(dotnet --roll-forward LatestMajor \
    /usr/share/dotnet/sdk/*/Tools/net*/any/Microsoft.DotNet.Tools.dll \
    2>/dev/null || true)
# Fallback: use project version
printf '1.0.0' > "${PACKAGE_DIR}/Version.txt"

# Create final package
echo "Creating final package..."
(cd "$PACKAGE_DIR" && zip -r "$FINAL_ZIP" .)

# Cleanup
rm -rf "$PUBLISH_DIR" "$PACKAGE_DIR"

echo "=== Package created: ${FINAL_ZIP} ==="
echo "Contents:"
unzip -l "$FINAL_ZIP"
```

---

## Phase 7 — Import / Install Scripts

### 7.1 PowerShell Install Script

Save as `install-plugin.ps1`:

```powershell
<#
.SYNOPSIS
    Installs a Musoq data source plugin from a local package.

.PARAMETER PackagePath
    Path to the .zip package file or extracted directory.

.EXAMPLE
    .\install-plugin.ps1 -PackagePath ".\Musoq.DataSources.MyPlugin-windows-x64.zip"
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

# Verify musoq CLI is available
if (-not (Get-Command "musoq" -ErrorAction SilentlyContinue)) {
    Write-Error "musoq CLI not found. Please install it first: https://github.com/Puchaczov/Musoq"
    exit 1
}

if (-not (Test-Path $PackagePath)) {
    Write-Error "Package not found: $PackagePath"
    exit 1
}

Write-Host "Installing plugin from: $PackagePath" -ForegroundColor Cyan
musoq datasource import $PackagePath

if ($LASTEXITCODE -eq 0) {
    Write-Host "Plugin installed successfully!" -ForegroundColor Green
} else {
    Write-Error "Plugin installation failed."
    exit 1
}
```

### 7.2 Bash Install Script

Save as `install-plugin.sh`:

```bash
#!/bin/bash
set -euo pipefail

PACKAGE_PATH="${1:?Usage: $0 <path-to-package.zip>}"

if ! command -v musoq &>/dev/null; then
    echo "Error: musoq CLI not found. Install it first."
    exit 1
fi

if [ ! -f "$PACKAGE_PATH" ]; then
    echo "Error: Package not found: $PACKAGE_PATH"
    exit 1
fi

echo "Installing plugin from: $PACKAGE_PATH"
musoq datasource import "$PACKAGE_PATH"
echo "Plugin installed successfully!"
```

### 7.3 Installing from Registry

```bash
# Install from the built-in plugin registry
musoq datasource install <plugin-short-name>

# Add a custom registry
musoq registry add custom https://your-registry.example.com/registry.json
```

---

## Appendix A — Troubleshooting & Common Pitfalls

These are real issues encountered during plugin development with proven solutions.

### A.1 Compilation Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| `CS8603: Possible null reference return` on `GetColumnByName` | `SingleOrDefault` returns nullable but interface expects non-null | Add `!` (null-forgiving operator): `return Columns.SingleOrDefault(...)!;` |
| Missing types from `Musoq.Schema` namespace | Missing package reference | Ensure `Musoq.Schema` is in your `.csproj` with `ExcludeAssets=runtime` |
| `Musoq.Schema.Exceptions.TableNotFoundException` not found | Older Musoq.Schema version | Use `throw new TableNotFoundException(nameof(name))` — check you have the right package version |
| `Musoq.Schema.Exceptions.SourceNotFoundException` not found | Same as above | Same as above |

### A.2 Test Failures

| Problem | Cause | Solution |
|---------|-------|----------|
| Tests return fewer rows than expected | Data creation issue — resource handles (streams, files) not properly disposed before reading | Use explicit `using (var x = ...) { }` blocks instead of `using var`. Dispose each resource before creating the next one. This is especially important for libraries that manage file/stream handles internally. |
| `desc #schema` test returns wrong column count | `GetRawConstructors` not implemented | Override both `GetRawConstructors(string, RuntimeContext)` and `GetRawConstructors(RuntimeContext)` |
| `desc #schema.unknownmethod` doesn't throw | Missing throw in switch default | Ensure the `_` arm in your switch throws `NotSupportedException` with a helpful message mentioning "Available data sources" |
| Assert fails on `table.Count` with off-by-one | Forgot to flush last partial chunk in RowSource | Add `if (list.Count > 0) chunkedSource.Add(list, endWorkToken)` after the loop |
| Null reference in `row.Values[n]` | Column type mismatch between entity and helper | Ensure `typeof()` in `SchemaColumn` matches the actual property type exactly |
| File path tests fail on Windows | Backslashes not escaped in SQL strings | Use `path.Replace("\\", "\\\\")` when embedding paths in SQL query strings |

### A.3 Runtime / Packaging Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Plugin not discovered by Musoq | Missing `[assembly: PluginSchemas(...)]` | Add `AssemblyInfo.cs` with the attribute |
| XML metadata missing at runtime | `GenerateDocumentationFile` not set, or `_ResolveCopyLocalNuGetPackageXmls` target missing | Add both to `.csproj` |
| Assembly loading conflict | Musoq.Schema.dll or Musoq.Parser.dll included in Plugin.zip | Remove from publish output; add `ExcludeAssets=runtime` to `.csproj` |
| Plugin loads but columns are empty | XML docs not generated | Check that `.xml` file exists next to `.dll` in build output |
| `desc #schema.method('arg')` crashes | `GetRawConstructors` returns wrong constructors | Use `TypeHelper.GetSchemaMethodInfosForType<YourRowSource>("method")` |

### A.4 Data Source Specific Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| RowSource hangs indefinitely | Not checking `endWorkToken.IsCancellationRequested` | Check cancellation token in every loop iteration |
| Memory issues with large files | Loading entire file content into memory | Use chunking (add to `chunkedSource` every N items) and consider not loading binary `Content` unless needed |
| `DateTime.MinValue` showing in results | Source returns default DateTime values | Map `DateTime.MinValue` to `null` for nullable DateTime columns |

---

## Appendix B — Complete File Reference

Here is the complete list of files for a plugin named `Musoq.DataSources.Example` with schema name `example` and one table method `file`:

| # | File | Purpose |
|---|------|---------|
| 1 | `Musoq.DataSources.Example/Musoq.DataSources.Example.csproj` | Project config with NuGet refs, dynamic loading, XML docs |
| 2 | `Musoq.DataSources.Example/AssemblyInfo.cs` | `[assembly: PluginSchemas("example")]` |
| 3 | `Musoq.DataSources.Example/Assembly.cs` | `[assembly: InternalsVisibleTo("Musoq.DataSources.Example.Tests")]` |
| 4 | `Musoq.DataSources.Example/Entities/ExampleEntity.cs` | Entity with `[EntityProperty]` on each column |
| 5 | `Musoq.DataSources.Example/Tables/ExampleTableHelper.cs` | Three static maps: NameToIndex, IndexToMethod, Columns |
| 6 | `Musoq.DataSources.Example/Tables/ExampleTable.cs` | `ISchemaTable` implementation |
| 7 | `Musoq.DataSources.Example/Sources/ExampleRowSource.cs` | `RowSourceBase<ExampleEntity>` with `CollectChunks` |
| 8 | `Musoq.DataSources.Example/ExampleLibrary.cs` | Empty `LibraryBase` (or with `[BindableMethod]` functions) |
| 9 | `Musoq.DataSources.Example/ExampleSchemaProvider.cs` | `ISchemaProvider` factory |
| 10 | `Musoq.DataSources.Example/ExampleSchema.cs` | `SchemaBase` with full XML docs |
| 11 | `Musoq.DataSources.Example.Tests/Musoq.DataSources.Example.Tests.csproj` | Test project config |
| 12 | `Musoq.DataSources.Example.Tests/ExampleTests.cs` | Functional SQL query tests |
| 13 | `Musoq.DataSources.Example.Tests/ExampleSchemaDescribeTests.cs` | `desc` query tests |
| 14 | `build-package.ps1` | PowerShell packaging script |
| 15 | `build-package.sh` | Bash packaging script |
| 16 | `install-plugin.ps1` | PowerShell install script |
| 17 | `install-plugin.sh` | Bash install script |

---

## Appendix C — NuGet Package Version Resolution

When you don't have access to an existing repo to read versions from, resolve the latest stable versions:

### Using `dotnet` CLI

```bash
# Search for latest version of a package
dotnet package search Musoq.Schema --take 1
dotnet package search Musoq.Parser --take 1
dotnet package search Musoq.Plugins --take 1
dotnet package search Musoq.Evaluator --take 1
dotnet package search Musoq.Converter --take 1
```

### Using NuGet HTTP API

```bash
# Get package versions (JSON response)
curl -s "https://api.nuget.org/v3-flatcontainer/musoq.schema/index.json"
# Returns: { "versions": ["1.0.0", "2.0.0", ..., "12.0.0"] }
# Take the last entry for the latest version.
```

### Using PowerShell

```powershell
$packageName = "Musoq.Schema"
$response = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$($packageName.ToLower())/index.json"
$latestVersion = $response.versions[-1]
Write-Host "$packageName : $latestVersion"
```

### Package Compatibility Matrix

The Musoq packages have version dependencies. When resolving, prefer using versions that are known to work together. The safest approach:

1. Resolve `Musoq.Schema` version first (it's the core).
2. Use `Musoq.Evaluator` and `Musoq.Converter` — these will pull compatible transitive dependencies.
3. All five packages in a single plugin must be from compatible releases.

---

## Appendix D — Predicate Pushdown for Web API Sources

When building plugins that wrap **web APIs** (REST, GraphQL, etc.), a critical optimization is **predicate pushdown** — extracting WHERE clause conditions from the SQL query and translating them into API-specific query parameters or query languages.

Without predicate pushdown, your plugin would:
1. Fetch **all** records from the API
2. Let Musoq runtime filter them in memory

With predicate pushdown, your plugin:
1. Extracts filter conditions from the parsed WHERE clause
2. Translates them to API query parameters (e.g., `?state=open&assignee=john`)
3. Fetches **only** matching records from the API
4. Let Musoq apply any remaining filters it couldn't push down

This dramatically reduces network traffic, API rate limit consumption, and memory usage.

### D.1 When to Implement Predicate Pushdown

Implement predicate pushdown when your data source:
- Wraps a **web API** (REST, GraphQL, SOAP, etc.)
- The API supports **query filtering** (query parameters, JQL, GraphQL filters, etc.)
- Dataset is potentially **large** (hundreds or thousands of records)
- API has **rate limits** that you want to respect

Skip predicate pushdown when:
- Your data source reads **local files** (filesystem filtering is fast)
- The API returns **all data anyway** (no server-side filtering supported)
- Dataset is always **small** (under 100 records)

### D.2 Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         User SQL Query                               │
│  SELECT * FROM #api.issues('PROJ') WHERE Status = 'Open' AND ...    │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Musoq Parser                                    │
│  Parses SQL → Creates AST with WhereNode containing condition tree  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    RuntimeContext                                    │
│  _runtimeContext.QuerySourceInfo.WhereNode → Parsed WHERE clause    │
│  _runtimeContext.QueryHints.TakeValue → LIMIT value (if any)        │
│  _runtimeContext.QueryHints.SkipValue → OFFSET value (if any)       │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                WhereNodeHelper / FilterBuilder                       │
│  1. ExtractParameters(WhereNode) → FilterParameters object          │
│  2. Walk AST tree, extract supported conditions                      │
│  3. Ignore unsupported conditions (Musoq filters them later)        │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    API Query Builder                                 │
│  BuildQuery(baseQuery, filterParameters) → API-specific query       │
│  e.g., JQL: "project = PROJ AND status = 'Open'"                    │
│  e.g., REST: "?state=open&assignee=john"                            │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Web API Call                                     │
│  Fetch only matching records (much smaller dataset)                  │
└─────────────────────────────────────────────────────────────────────┘
```

### D.3 Filter Parameters Class

Create a class to hold extracted filter parameters specific to your API:

```csharp
using Musoq.Parser.Nodes;

namespace Musoq.DataSources.[FILL].Helpers;

/// <summary>
/// Parameters extracted from WHERE clause for [FILL] API queries.
/// </summary>
internal class [FILL]FilterParameters
{
    // String filters (equality comparisons)
    /// <summary>Gets or sets the status filter (e.g., "open", "closed").</summary>
    public string? Status { get; set; }
    
    /// <summary>Gets or sets the assignee filter.</summary>
    public string? Assignee { get; set; }
    
    /// <summary>Gets or sets the author/creator filter.</summary>
    public string? Author { get; set; }
    
    // Collection filters (for IN or multiple equality)
    /// <summary>Gets or sets labels to filter by.</summary>
    public List<string> Labels { get; set; } = [];
    
    // Date range filters (comparison operators)
    /// <summary>Gets or sets the created date range start (>= comparison).</summary>
    public DateTimeOffset? CreatedAfter { get; set; }
    
    /// <summary>Gets or sets the created date range end (<= comparison).</summary>
    public DateTimeOffset? CreatedBefore { get; set; }
    
    // Boolean filters
    /// <summary>Gets or sets whether to filter archived items.</summary>
    public bool? IsArchived { get; set; }
    
    // Text search (LIKE operator)
    /// <summary>Gets or sets text search query from LIKE conditions.</summary>
    public string? TextSearch { get; set; }
    
    // [FILL: Add properties for each API filter your endpoint supports]
}
```

### D.4 Where Node Helper Class

Create a helper class that walks the WHERE clause AST and extracts filter parameters:

```csharp
using Musoq.Parser.Nodes;

namespace Musoq.DataSources.[FILL].Helpers;

/// <summary>
/// Helper class to extract filter parameters from WHERE clause nodes for [FILL] API.
/// </summary>
internal static class WhereNodeHelper
{
    /// <summary>
    /// Extracts filter parameters from a WHERE node.
    /// </summary>
    public static [FILL]FilterParameters ExtractParameters(WhereNode? whereNode)
    {
        var parameters = new [FILL]FilterParameters();
        
        if (whereNode?.Expression == null)
            return parameters;
        
        ExtractFromNode(whereNode.Expression, parameters);
        
        return parameters;
    }
    
    private static void ExtractFromNode(Node node, [FILL]FilterParameters parameters)
    {
        switch (node)
        {
            // AND conditions: process both sides (both can be pushed down)
            case AndNode andNode:
                ExtractFromNode(andNode.Left, parameters);
                ExtractFromNode(andNode.Right, parameters);
                break;
            
            // OR conditions: skip for pushdown (complex to translate, let runtime filter)
            case OrNode:
                // OR conditions are complex — skip for now.
                // The Musoq runtime will filter these after fetching.
                break;
            
            // Equality: column = 'value'
            case EqualityNode equalityNode:
                ExtractEqualityCondition(equalityNode, parameters);
                break;
            
            // Comparisons: column >= value, column < value, etc.
            case GreaterOrEqualNode greaterEqualNode:
                ExtractComparisonCondition(greaterEqualNode, ">=", parameters);
                break;
            
            case LessOrEqualNode lessEqualNode:
                ExtractComparisonCondition(lessEqualNode, "<=", parameters);
                break;
            
            case GreaterNode greaterNode:
                ExtractComparisonCondition(greaterNode, ">", parameters);
                break;
            
            case LessNode lessNode:
                ExtractComparisonCondition(lessNode, "<", parameters);
                break;
            
            // LIKE: column LIKE '%pattern%' (for text search)
            case LikeNode likeNode:
                ExtractLikeCondition(likeNode, parameters);
                break;
        }
    }
    
    private static void ExtractEqualityCondition(
        EqualityNode node, 
        [FILL]FilterParameters parameters)
    {
        var (fieldName, value) = ExtractFieldAndValue(node.Left, node.Right);
        
        if (fieldName == null || value == null)
            return;
        
        // Map entity property names to filter parameters
        // Use case-insensitive matching for robustness
        switch (fieldName.ToLowerInvariant())
        {
            case "status":
                parameters.Status = value.ToString();
                break;
            case "assignee":
            case "assigneedisplayname":
                parameters.Assignee = value.ToString();
                break;
            case "author":
            case "authorlogin":
                parameters.Author = value.ToString();
                break;
            case "isarchived":
                if (bool.TryParse(value.ToString(), out var archived))
                    parameters.IsArchived = archived;
                break;
            // [FILL: Add cases for each filterable column in your entity]
        }
    }
    
    private static void ExtractComparisonCondition(
        Node node, 
        string op, 
        [FILL]FilterParameters parameters)
    {
        Node left, right;
        
        switch (node)
        {
            case GreaterOrEqualNode ge:
                left = ge.Left;
                right = ge.Right;
                break;
            case LessOrEqualNode le:
                left = le.Left;
                right = le.Right;
                break;
            case GreaterNode g:
                left = g.Left;
                right = g.Right;
                break;
            case LessNode l:
                left = l.Left;
                right = l.Right;
                break;
            default:
                return;
        }
        
        var (fieldName, value) = ExtractFieldAndValue(left, right);
        
        if (fieldName == null || value == null)
            return;
        
        // Handle date range comparisons
        switch (fieldName.ToLowerInvariant())
        {
            case "createdat":
                if (op is ">=" or ">")
                {
                    if (DateTimeOffset.TryParse(value.ToString(), out var after))
                        parameters.CreatedAfter = after;
                }
                else if (op is "<=" or "<")
                {
                    if (DateTimeOffset.TryParse(value.ToString(), out var before))
                        parameters.CreatedBefore = before;
                }
                break;
            // [FILL: Add cases for other date/numeric columns]
        }
    }
    
    private static void ExtractLikeCondition(
        LikeNode node, 
        [FILL]FilterParameters parameters)
    {
        if (node.Left is not FieldNode fieldNode)
            return;
        
        if (node.Right is not StringNode stringNode)
            return;
        
        var fieldName = fieldNode.FieldName.ToLowerInvariant();
        var pattern = stringNode.Value;
        
        // Convert SQL LIKE pattern to API search (strip % wildcards)
        var searchText = pattern.Trim('%');
        
        switch (fieldName)
        {
            case "summary":
            case "title":
            case "description":
                parameters.TextSearch = searchText;
                break;
        }
    }
    
    private static (string? fieldName, object? value) ExtractFieldAndValue(
        Node left, 
        Node right)
    {
        string? fieldName = null;
        object? value = null;
        
        // Handle: Column = 'value'
        if (left is FieldNode fieldNode)
        {
            fieldName = fieldNode.FieldName;
            value = ExtractValue(right);
        }
        // Handle: 'value' = Column (less common but valid SQL)
        else if (right is FieldNode fieldNode2)
        {
            fieldName = fieldNode2.FieldName;
            value = ExtractValue(left);
        }
        
        return (fieldName, value);
    }
    
    private static object? ExtractValue(Node node)
    {
        return node switch
        {
            StringNode stringNode => stringNode.Value,
            IntegerNode intNode => intNode.ObjValue,
            DecimalNode decimalNode => decimalNode.Value,
            BooleanNode boolNode => boolNode.Value,
            _ => null  // Unsupported node type — skip
        };
    }
}
```

### D.5 API Query Builder

Translate extracted parameters into your API's query format:

```csharp
namespace Musoq.DataSources.[FILL].Helpers;

/// <summary>
/// Builds API-specific queries from extracted filter parameters.
/// </summary>
internal static class QueryBuilder
{
    /// <summary>
    /// Builds a query string/object from filter parameters.
    /// </summary>
    /// <param name="baseQuery">Base query (e.g., project filter from method args)</param>
    /// <param name="parameters">Filter parameters from WHERE clause</param>
    /// <returns>API-specific query format</returns>
    public static string BuildQuery(string? baseQuery, [FILL]FilterParameters parameters)
    {
        var conditions = new List<string>();
        
        if (!string.IsNullOrEmpty(baseQuery))
            conditions.Add(baseQuery);
        
        // For JQL-style APIs (Jira, etc.)
        if (!string.IsNullOrEmpty(parameters.Status))
            conditions.Add($"status = \"{EscapeValue(parameters.Status)}\"");
        
        if (!string.IsNullOrEmpty(parameters.Assignee))
            conditions.Add($"assignee = \"{EscapeValue(parameters.Assignee)}\"");
        
        if (parameters.CreatedAfter.HasValue)
            conditions.Add($"created >= \"{parameters.CreatedAfter.Value:yyyy-MM-dd}\"");
        
        if (parameters.CreatedBefore.HasValue)
            conditions.Add($"created <= \"{parameters.CreatedBefore.Value:yyyy-MM-dd}\"");
        
        // [FILL: Add translation for each filter parameter]
        
        return conditions.Count > 0 
            ? string.Join(" AND ", conditions) 
            : string.Empty;  // No filters = fetch all (API default)
    }
    
    /// <summary>
    /// Builds REST query parameters from filter parameters.
    /// </summary>
    public static Dictionary<string, string> BuildQueryParams([FILL]FilterParameters parameters)
    {
        var queryParams = new Dictionary<string, string>();
        
        // For REST APIs with query parameters
        if (!string.IsNullOrEmpty(parameters.Status))
            queryParams["state"] = parameters.Status.ToLowerInvariant();
        
        if (!string.IsNullOrEmpty(parameters.Assignee))
            queryParams["assignee"] = parameters.Assignee;
        
        if (parameters.CreatedAfter.HasValue)
            queryParams["since"] = parameters.CreatedAfter.Value.ToString("O");
        
        // [FILL: Add query parameters for each filter]
        
        return queryParams;
    }
    
    private static string EscapeValue(string value)
    {
        // Escape special characters for your API's query language
        return value.Replace("\"", "\\\"");
    }
}
```

### D.6 Using Predicate Pushdown in RowSource

Update your RowSource to use the filter extraction:

```csharp
protected override void CollectChunks(
    BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    _runtimeContext.ReportDataSourceBegin(SourceName);
    long totalRowsProcessed = 0;

    try
    {
        // ═══════════════════════════════════════════════════════════════
        // STEP 1: Extract filter parameters from WHERE clause
        // ═══════════════════════════════════════════════════════════════
        var filterParams = WhereNodeHelper.ExtractParameters(
            _runtimeContext.QuerySourceInfo.WhereNode);
        
        // ═══════════════════════════════════════════════════════════════
        // STEP 2: Get pagination hints from query (LIMIT/OFFSET)
        // ═══════════════════════════════════════════════════════════════
        var takeValue = _runtimeContext.QueryHints.TakeValue;  // LIMIT
        var skipValue = _runtimeContext.QueryHints.SkipValue;  // OFFSET
        
        var maxRows = takeValue.HasValue ? (int)takeValue.Value : int.MaxValue;
        var startAt = skipValue.HasValue ? (int)skipValue.Value : 0;
        
        // ═══════════════════════════════════════════════════════════════
        // STEP 3: Build API query with extracted filters
        // ═══════════════════════════════════════════════════════════════
        var baseQuery = $"project = {_projectKey}";  // From method args
        var apiQuery = QueryBuilder.BuildQuery(baseQuery, filterParams);
        
        // ═══════════════════════════════════════════════════════════════
        // STEP 4: Fetch data with pushed-down predicates
        // ═══════════════════════════════════════════════════════════════
        var fetchedRows = 0;
        var pageSize = 100;
        
        while (fetchedRows < maxRows && 
               !_runtimeContext.EndWorkToken.IsCancellationRequested)
        {
            // API call with filters already applied
            var items = _api.SearchAsync(apiQuery, pageSize, startAt).Result;
            
            if (items.Count == 0)
                break;
            
            var resolvers = items
                .Take(maxRows - fetchedRows)
                .Select(item => new EntityResolver<[FILL]Entity>(
                    item,
                    [FILL]TableHelper.NameToIndexMap,
                    [FILL]TableHelper.IndexToMethodAccessMap))
                .ToList();
            
            chunkedSource.Add(resolvers, _runtimeContext.EndWorkToken);
            
            fetchedRows += resolvers.Count;
            totalRowsProcessed += resolvers.Count;
            startAt += items.Count;
            
            _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
            
            if (items.Count < pageSize)
                break;  // Last page
        }
    }
    finally
    {
        _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
    }
}
```

### D.7 Supported AST Node Types

The Musoq parser creates these node types that you can handle:

| Node Type | SQL Example | Description |
|-----------|-------------|-------------|
| `AndNode` | `a AND b` | Logical AND — process both sides |
| `OrNode` | `a OR b` | Logical OR — typically skip (complex) |
| `EqualityNode` | `Col = 'value'` | Equality comparison |
| `NotEqualNode` | `Col <> 'value'` | Inequality comparison |
| `GreaterNode` | `Col > 10` | Greater than |
| `GreaterOrEqualNode` | `Col >= 10` | Greater than or equal |
| `LessNode` | `Col < 10` | Less than |
| `LessOrEqualNode` | `Col <= 10` | Less than or equal |
| `LikeNode` | `Col LIKE '%x%'` | Pattern matching |
| `InNode` | `Col IN ('a','b')` | Set membership |
| `IsNullNode` | `Col IS NULL` | Null check |

Value node types for extracting literals:

| Node Type | Example | Property |
|-----------|---------|----------|
| `StringNode` | `'hello'` | `.Value` (string) |
| `IntegerNode` | `42` | `.ObjValue` (object) |
| `DecimalNode` | `3.14` | `.Value` (decimal) |
| `BooleanNode` | `true` | `.Value` (bool) |
| `FieldNode` | `ColumnName` | `.FieldName` (string) |

### D.8 Best Practices

1. **Don't require all filters** — If WHERE clause has no pushable conditions, fetch all data.

2. **Skip OR conditions** — OR is complex to translate. Let Musoq runtime filter.
   ```csharp
   case OrNode:
       // Skip — Musoq runtime will filter after fetch
       break;
   ```

3. **Handle both field positions** — SQL allows `'value' = Column`:
   ```csharp
   if (left is FieldNode) { /* normal */ }
   else if (right is FieldNode) { /* reversed */ }
   ```

4. **Map multiple property names** — Users might filter on different property names:
   ```csharp
   case "assignee":
   case "assigneedisplayname":
   case "assigneeemail":
       parameters.Assignee = value.ToString();
       break;
   ```

5. **Use LIMIT/OFFSET hints** — Apply them to API pagination:
   ```csharp
   var take = _runtimeContext.QueryHints.TakeValue;
   var skip = _runtimeContext.QueryHints.SkipValue;
   ```

6. **Escape values properly** — Prevent injection in API queries:
   ```csharp
   private static string EscapeJql(string value) =>
       value.Replace("\"", "\\\"").Replace("'", "\\'");
   ```

7. **Document pushable columns** — In XML docs, note which columns support pushdown:
   ```xml
   /// <param name="Status">
   /// The issue status. Supports predicate pushdown for efficient API filtering.
   /// </param>
   ```

### D.9 Example: Complete Filter Extraction

Given this SQL query:
```sql
SELECT * 
FROM #jira.issues('MYPROJ') 
WHERE Status = 'Open' 
  AND Assignee = 'john.doe'
  AND CreatedAt >= '2024-01-01'
  AND Priority = 'High'
```

The WhereNodeHelper extracts:
```csharp
filterParams.Status = "Open"
filterParams.Assignee = "john.doe"
filterParams.CreatedAfter = 2024-01-01T00:00:00
filterParams.Priority = "High"
```

QueryBuilder generates JQL:
```
project = MYPROJ AND status = "Open" AND assignee = "john.doe" 
AND created >= "2024-01-01" AND priority = "High"
```

API call fetches only matching issues instead of all issues in the project.

---

*This document was validated by building a working data source plugin from scratch that queries compound binary files (OLE structured storage). All patterns, code templates, test structures, and packaging scripts have been verified against the actual Musoq framework.*
