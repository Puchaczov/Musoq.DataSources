# Musoq Plugin Development Guide

This comprehensive guide provides everything you need to create custom plugins for Musoq, enabling you to query any data source using SQL-like syntax.

## Table of Contents

1. [Quick Start (5 Minutes)](#quick-start-5-minutes)
2. [Prerequisites](#prerequisites)
3. [Plugin Architecture Overview](#plugin-architecture-overview)
4. [Step-by-Step Implementation Guide](#step-by-step-implementation-guide)
5. [Component Details](#component-details)
6. [Advanced Features](#advanced-features)
7. [Testing Your Plugin](#testing-your-plugin)
8. [Best Practices](#best-practices)
9. [Real-World Examples](#real-world-examples)
10. [Common Use Cases](#common-use-cases)
11. [Working Example Plugin](#working-example-plugin)
12. [Support and Community](#support-and-community)

---

## Quick Start (5 Minutes)

Get up and running with a working Musoq plugin in 5 minutes using this minimal template.

### 1. Create Project Structure

```bash
mkdir Musoq.DataSources.MyPlugin
cd Musoq.DataSources.MyPlugin

# Create the basic directory structure
mkdir Entities Tables Sources

# Create the project file
cat > Musoq.DataSources.MyPlugin.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <Version>1.0.0</Version>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Musoq.Parser" Version="4.4.0">
            <ExcludeAssets>runtime</ExcludeAssets>
        </PackageReference>
        <PackageReference Include="Musoq.Plugins" Version="6.11.0" />
        <PackageReference Include="Musoq.Schema" Version="8.2.0">
            <ExcludeAssets>runtime</ExcludeAssets>
        </PackageReference>
    </ItemGroup>
</Project>
EOF
```

### 2. Copy Template Files

Save each of the following files in your project:

#### `AssemblyInfo.cs`
```csharp
using Musoq.Schema.Attributes;

[assembly: PluginSchemas("myplugin")]
```

#### `Entities/SimpleEntity.cs`
```csharp
namespace Musoq.DataSources.MyPlugin.Entities;

public class SimpleEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.Now;
    public int Value { get; set; }
}
```

#### `Tables/SimpleTableHelper.cs`
```csharp
using Musoq.Schema;
using Musoq.DataSources.MyPlugin.Entities;

namespace Musoq.DataSources.MyPlugin.Tables;

internal static class SimpleTableHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap = new Dictionary<string, int>
    {
        {nameof(SimpleEntity.Id), 0},
        {nameof(SimpleEntity.Name), 1},
        {nameof(SimpleEntity.Created), 2},
        {nameof(SimpleEntity.Value), 3}
    };
    
    public static readonly IReadOnlyDictionary<int, Func<SimpleEntity, object?>> IndexToMethodAccessMap = new Dictionary<int, Func<SimpleEntity, object?>>
    {
        {0, entity => entity.Id},
        {1, entity => entity.Name},
        {2, entity => entity.Created},
        {3, entity => entity.Value}
    };
    
    public static readonly ISchemaColumn[] Columns = new[]
    {
        new SchemaColumn(nameof(SimpleEntity.Id), 0, typeof(string)),
        new SchemaColumn(nameof(SimpleEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(SimpleEntity.Created), 2, typeof(DateTime)),
        new SchemaColumn(nameof(SimpleEntity.Value), 3, typeof(int))
    };
}
```

#### `Tables/SimpleTable.cs`
```csharp
using Musoq.Schema;
using Musoq.DataSources.MyPlugin.Entities;

namespace Musoq.DataSources.MyPlugin.Tables;

internal class SimpleTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SimpleTableHelper.Columns;
    public SchemaTableMetadata Metadata { get; } = new(typeof(SimpleEntity));

    public ISchemaColumn? GetColumnByName(string name) =>
        Columns.SingleOrDefault(column => column.ColumnName == name);
    
    public ISchemaColumn[] GetColumnsByName(string name) =>
        Columns.Where(column => column.ColumnName == name).ToArray();
}
```

#### `Sources/SimpleRowSource.cs`
```csharp
using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.MyPlugin.Entities;
using Musoq.DataSources.MyPlugin.Tables;

namespace Musoq.DataSources.MyPlugin.Sources;

internal class SimpleRowSource : RowSourceBase<SimpleEntity>
{
    private readonly int _count;

    public SimpleRowSource(RuntimeContext runtimeContext, int count = 10)
    {
        _count = count;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var entities = Enumerable.Range(1, _count)
            .Select(i => new SimpleEntity
            {
                Id = i.ToString(),
                Name = $"Item {i}",
                Created = DateTime.Now.AddDays(-i),
                Value = i * 10
            })
            .ToList();

        var resolvers = entities.Select(entity => 
            new EntityResolver<SimpleEntity>(entity, SimpleTableHelper.NameToIndexMap, SimpleTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }
}
```

#### `SimpleLibrary.cs`
```csharp
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Musoq.DataSources.MyPlugin.Entities;

namespace Musoq.DataSources.MyPlugin;

public class SimpleLibrary : LibraryBase
{
    [BindableMethod]
    public string FormatItem([InjectSpecificSource(typeof(SimpleEntity))] SimpleEntity entity)
    {
        return $"{entity.Name} (#{entity.Id})";
    }

    [BindableMethod]
    public int DaysOld([InjectSpecificSource(typeof(SimpleEntity))] SimpleEntity entity)
    {
        return (DateTime.Now - entity.Created).Days;
    }
}
```

#### `MyPluginSchema.cs`
```csharp
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using Musoq.DataSources.MyPlugin.Sources;
using Musoq.DataSources.MyPlugin.Tables;

namespace Musoq.DataSources.MyPlugin;

/// <description>
/// Simple demo plugin for Musoq
/// </description>
/// <short-description>
/// Simple demo plugin for Musoq
/// </short-description>
/// <project-url>https://github.com/YourGitHub/MyPlugin</project-url>
public class MyPluginSchema : SchemaBase
{
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>#myplugin.simple()</from>
    /// <description>Gets simple demo data</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Item name</column>
    /// <column name="Created" type="DateTime">Creation date</column>
    /// <column name="Value" type="int">Numeric value</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Number of items to generate</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#myplugin.simple(int count)</from>
    /// <description>Gets simple demo data with specified count</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Item name</column>
    /// <column name="Created" type="DateTime">Creation date</column>
    /// <column name="Value" type="int">Numeric value</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public MyPluginSchema() : base("myplugin", CreateLibrary()) { }

    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "simple" => new SimpleTable(),
            _ => throw new NotSupportedException($"Table '{name}' not supported")
        };
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "simple" => new SimpleRowSource(runtimeContext, parameters.Length > 0 ? Convert.ToInt32(parameters[0]) : 10),
            _ => throw new NotSupportedException($"Table '{name}' not supported")
        };
    }

    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<SimpleRowSource>("simple"));
        return constructors.ToArray();
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        methodsManager.RegisterLibraries(new SimpleLibrary());
        return new MethodsAggregator(methodsManager);
    }
}
```

### 3. Build and Test

```bash
# Build the plugin
dotnet build

# Test it works
dotnet pack
```

### 4. Usage Example

Once your plugin is built, you can use it in Musoq like this:

```sql
-- Get all items
SELECT * FROM #myplugin.simple()

-- Get 5 items
SELECT * FROM #myplugin.simple(5)

-- Use custom functions
SELECT Id, Name, FormatItem() as Formatted, DaysOld() as Age 
FROM #myplugin.simple(5)

-- Filter and order
SELECT * FROM #myplugin.simple(20) 
WHERE Value > 50 
ORDER BY Created DESC
```

---

## Prerequisites

- .NET 8.0 SDK or later
- Understanding of C# programming
- Basic knowledge of SQL concepts
- Familiarity with the data source you want to integrate

---

## Plugin Architecture Overview

A Musoq plugin consists of several key components working together:

```
MyPlugin/
├── AssemblyInfo.cs              # Plugin registration
├── MyPluginSchema.cs            # Main schema class
├── Tables/
│   ├── MyTable.cs              # Table metadata definition
│   └── MyTableHelper.cs        # Column mappings and helpers
├── Sources/
│   └── MyRowSource.cs          # Data source implementation
├── Entities/
│   └── MyEntity.cs             # Data model
├── MyPluginLibrary.cs          # Custom functions (optional)
└── MyPlugin.csproj             # Project configuration
```

### Core Components

1. **Schema**: The main entry point that defines available tables and functions
2. **Table**: Defines the structure and metadata of your data
3. **RowSource**: Implements the data retrieval logic
4. **Entity**: Represents the data model/structure
5. **Library**: Provides custom functions and methods (optional)
6. **Helper**: Contains mappings between entity properties and table columns

---

## Step-by-Step Implementation Guide

### Step 1: Create the Project

Create a new .NET class library:

```bash
dotnet new classlib -n Musoq.DataSources.MyPlugin
cd Musoq.DataSources.MyPlugin
```

### Step 2: Configure the Project File

Update your `.csproj` file:

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
        <PackageProjectUrl>https://github.com/YourGitHub/MyPlugin</PackageProjectUrl>
        <PackageLicenseFile>LICENSE</PackageLicenseFile>
        <PackageTags>sql, myplugin, dotnet-core</PackageTags>
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <IncludeSymbols>true</IncludeSymbols>
        <SymbolPackageFormat>snupkg</SymbolPackageFormat>
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <PackageId>Musoq.DataSources.MyPlugin</PackageId>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Musoq.Parser" Version="4.4.0">
            <ExcludeAssets>runtime</ExcludeAssets>
        </PackageReference>
        <PackageReference Include="Musoq.Plugins" Version="6.11.0" />
        <PackageReference Include="Musoq.Schema" Version="8.2.0">
            <ExcludeAssets>runtime</ExcludeAssets>
        </PackageReference>
        <!-- Add your specific dependencies here -->
    </ItemGroup>
</Project>
```

### Step 3: Register the Plugin

Create `AssemblyInfo.cs`:

```csharp
using Musoq.Schema.Attributes;

[assembly: PluginSchemas("myplugin")]
```

### Step 4: Create the Entity

Create `Entities/MyEntity.cs`:

```csharp
namespace Musoq.DataSources.MyPlugin.Entities;

/// <summary>
/// Represents a data record from your data source
/// </summary>
public class MyEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int Count { get; set; }
    public bool IsActive { get; set; }
    
    // Add properties that represent your data structure
}
```

### Step 5: Create the Helper Class

Create `Tables/MyTableHelper.cs`:

```csharp
using Musoq.Schema;
using Musoq.DataSources.MyPlugin.Entities;

namespace Musoq.DataSources.MyPlugin.Tables;

internal static class MyTableHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<MyEntity, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static MyTableHelper()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(MyEntity.Id), 0},
            {nameof(MyEntity.Name), 1},
            {nameof(MyEntity.CreatedDate), 2},
            {nameof(MyEntity.Count), 3},
            {nameof(MyEntity.IsActive), 4}
        };
        
        IndexToMethodAccessMap = new Dictionary<int, Func<MyEntity, object?>>
        {
            {0, entity => entity.Id},
            {1, entity => entity.Name},
            {2, entity => entity.CreatedDate},
            {3, entity => entity.Count},
            {4, entity => entity.IsActive}
        };
        
        Columns = new[]
        {
            new SchemaColumn(nameof(MyEntity.Id), 0, typeof(string)),
            new SchemaColumn(nameof(MyEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(MyEntity.CreatedDate), 2, typeof(DateTime)),
            new SchemaColumn(nameof(MyEntity.Count), 3, typeof(int)),
            new SchemaColumn(nameof(MyEntity.IsActive), 4, typeof(bool))
        };
    }
}
```

### Step 6: Create the Table

Create `Tables/MyTable.cs`:

```csharp
using Musoq.Schema;
using Musoq.DataSources.MyPlugin.Entities;

namespace Musoq.DataSources.MyPlugin.Tables;

internal class MyTable : ISchemaTable
{
    public ISchemaColumn[] Columns => MyTableHelper.Columns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(MyEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}
```

### Step 7: Create the Row Source

Create `Sources/MyRowSource.cs`:

```csharp
using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.MyPlugin.Entities;
using Musoq.DataSources.MyPlugin.Tables;

namespace Musoq.DataSources.MyPlugin.Sources;

internal class MyRowSource : RowSourceBase<MyEntity>
{
    private readonly string? _connectionString;
    private readonly CancellationToken _cancellationToken;

    public MyRowSource(RuntimeContext runtimeContext, string? connectionString = null)
    {
        _connectionString = connectionString ?? runtimeContext.EnvironmentVariables.GetValueOrDefault("MY_CONNECTION_STRING");
        _cancellationToken = runtimeContext.EndWorkToken;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var entities = GetDataFromSource();

        var resolvers = entities.Select(entity => 
            new EntityResolver<MyEntity>(entity, MyTableHelper.NameToIndexMap, MyTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }

    private List<MyEntity> GetDataFromSource()
    {
        // Implement your data retrieval logic here
        // This could be:
        // - API calls
        // - Database queries
        // - File reading
        // - Web scraping
        // - etc.
        
        return new List<MyEntity>
        {
            new MyEntity 
            { 
                Id = "1", 
                Name = "Sample Record", 
                CreatedDate = DateTime.Now, 
                Count = 42, 
                IsActive = true 
            }
        };
    }
}
```

### Step 8: Create the Library (Optional)

Create `MyPluginLibrary.cs`:

```csharp
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Musoq.DataSources.MyPlugin.Entities;

namespace Musoq.DataSources.MyPlugin;

/// <summary>
/// Provides custom functions for MyPlugin
/// </summary>
public class MyPluginLibrary : LibraryBase
{
    /// <summary>
    /// Formats the entity name with a prefix
    /// </summary>
    /// <param name="entity">The entity instance</param>
    /// <param name="prefix">Prefix to add</param>
    /// <returns>Formatted name</returns>
    [BindableMethod]
    public string FormatName([InjectSpecificSource(typeof(MyEntity))] MyEntity entity, string prefix)
    {
        return $"{prefix}: {entity.Name}";
    }

    /// <summary>
    /// Calculates days since creation
    /// </summary>
    /// <param name="entity">The entity instance</param>
    /// <returns>Days since creation</returns>
    [BindableMethod]
    public int DaysSinceCreation([InjectSpecificSource(typeof(MyEntity))] MyEntity entity)
    {
        return (DateTime.Now - entity.CreatedDate).Days;
    }
}
```

### Step 9: Create the Schema

Create `MyPluginSchema.cs`:

```csharp
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using Musoq.DataSources.MyPlugin.Sources;
using Musoq.DataSources.MyPlugin.Tables;

namespace Musoq.DataSources.MyPlugin;

/// <description>
/// Provides access to MyPlugin data source
/// </description>
/// <short-description>
/// Provides access to MyPlugin data source
/// </short-description>
/// <project-url>https://github.com/YourGitHub/MyPlugin</project-url>
public class MyPluginSchema : SchemaBase
{
    private const string SchemaName = "myplugin";
    private const string TableName = "data";

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="MY_CONNECTION_STRING" isRequired="false">Connection string for MyPlugin</environmentVariable>
    /// </environmentVariables>
    /// #myplugin.data()
    /// </from>
    /// <description>Gets data from MyPlugin source</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Record name</column>
    /// <column name="CreatedDate" type="DateTime">Creation date</column>
    /// <column name="Count" type="int">Count value</column>
    /// <column name="IsActive" type="bool">Active status</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Connection string</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#myplugin.data(string connectionString)</from>
    /// <description>Gets data from MyPlugin source with custom connection</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Record name</column>
    /// <column name="CreatedDate" type="DateTime">Creation date</column>
    /// <column name="Count" type="int">Count value</column>
    /// <column name="IsActive" type="bool">Active status</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public MyPluginSchema() 
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    /// Gets the table metadata for the specified table name
    /// </summary>
    /// <param name="name">Table name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters</param>
    /// <returns>Table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            TableName => new MyTable(),
            _ => throw new NotSupportedException($"Table '{name}' is not supported.")
        };
    }

    /// <summary>
    /// Gets the row source for the specified table name
    /// </summary>
    /// <param name="name">Table name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters</param>
    /// <returns>Row source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            TableName => new MyRowSource(runtimeContext, parameters.Length > 0 ? parameters[0]?.ToString() : null),
            _ => throw new NotSupportedException($"Table '{name}' is not supported.")
        };
    }

    /// <summary>
    /// Gets the available constructors for this schema
    /// </summary>
    /// <returns>Schema method information</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<MyRowSource>(TableName));
        return constructors.ToArray();
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new MyPluginLibrary();
        methodsManager.RegisterLibraries(library);
        return new MethodsAggregator(methodsManager);
    }
}
```

---

## Component Details

### Schema Class Deep Dive

The Schema class is the main entry point for your plugin. Key responsibilities:

- **Registration**: Inherits from `SchemaBase` and registers with the specified name
- **Table Resolution**: `GetTableByName()` returns appropriate table metadata
- **Data Source Creation**: `GetRowSource()` creates data source instances
- **Constructor Definition**: `GetConstructors()` defines available table constructors
- **Library Integration**: `CreateLibrary()` registers custom functions

### RowSource Patterns

There are two main patterns for implementing row sources:

#### 1. Synchronous Collection (RowSourceBase<T>)
```csharp
internal class MyRowSource : RowSourceBase<MyEntity>
{
    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        // Collect all data and add to chunkedSource
        var data = GetAllData();
        var resolvers = data.Select(item => new EntityResolver<MyEntity>(...)).ToList();
        chunkedSource.Add(resolvers);
    }
}
```

#### 2. Streaming/Async (RowSource)
```csharp
internal class MyRowSource : RowSource
{
    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            foreach (var item in GetDataStream())
            {
                yield return new EntityResolver<MyEntity>(item, ...);
            }
        }
    }
}
```

### Entity Design Principles

- Use properties with public getters
- Include null safety annotations (`string?`, `int?`)
- Consider using records for immutable data
- Add meaningful XML documentation

---

## Advanced Features

### Custom Functions with Entity Injection

```csharp
[BindableMethod]
public string ProcessEntity([InjectSpecificSource(typeof(MyEntity))] MyEntity entity, string parameter)
{
    return $"Processed {entity.Name} with {parameter}";
}
```

### Complex Data Types

Support complex properties in entities:

```csharp
public class MyComplexEntity
{
    public string Id { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public NestedObject Details { get; set; } = new();
}
```

### Multiple Tables in One Schema

```csharp
public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
{
    return name.ToLowerInvariant() switch
    {
        "users" => new UsersRowSource(runtimeContext),
        "orders" => new OrdersRowSource(runtimeContext),
        "products" => new ProductsRowSource(runtimeContext),
        _ => throw new NotSupportedException($"Table '{name}' is not supported.")
    };
}
```

### Environment Variable Usage

```csharp
public MyRowSource(RuntimeContext runtimeContext)
{
    var apiKey = runtimeContext.EnvironmentVariables["MY_API_KEY"];
    var endpoint = runtimeContext.EnvironmentVariables.GetValueOrDefault("MY_ENDPOINT", "https://default.api.com");
}
```

### Async Data Retrieval

```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    var data = GetDataAsync().GetAwaiter().GetResult();
    // Process data...
}

private async Task<List<MyEntity>> GetDataAsync()
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetStringAsync("https://api.example.com/data");
    return JsonSerializer.Deserialize<List<MyEntity>>(response) ?? new List<MyEntity>();
}
```

---

## Testing Your Plugin

### Unit Testing Setup

Create a test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
        <PackageReference Include="xunit" Version="2.4.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
        <PackageReference Include="Moq" Version="4.20.69" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Musoq.DataSources.MyPlugin\Musoq.DataSources.MyPlugin.csproj" />
    </ItemGroup>
</Project>
```

### Sample Test

```csharp
[Fact]
public void RowSource_Should_Return_Data()
{
    // Arrange
    var runtimeContext = new RuntimeContext(CancellationToken.None, 
        new Dictionary<string, string>());
    var rowSource = new MyRowSource(runtimeContext);

    // Act
    var rows = rowSource.Rows.ToList();

    // Assert
    Assert.NotEmpty(rows);
    Assert.True(rows.Count > 0);
}
```

### Integration Testing

```csharp
[Fact]
public void Schema_Should_Create_Valid_Table()
{
    // Arrange
    var schema = new MyPluginSchema();
    var runtimeContext = new RuntimeContext(CancellationToken.None, new Dictionary<string, string>());

    // Act
    var table = schema.GetTableByName("data", runtimeContext);
    var rowSource = schema.GetRowSource("data", runtimeContext);

    // Assert
    Assert.NotNull(table);
    Assert.NotNull(rowSource);
    Assert.True(table.Columns.Length > 0);
}
```

---

## Best Practices

### 1. Error Handling

```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    try
    {
        var data = GetDataFromSource();
        var resolvers = data.Select(entity => 
            new EntityResolver<MyEntity>(entity, MyTableHelper.NameToIndexMap, MyTableHelper.IndexToMethodAccessMap))
            .ToList();
        chunkedSource.Add(resolvers);
    }
    catch (Exception ex)
    {
        // Log the error appropriately
        throw new InvalidOperationException($"Failed to retrieve data from MyPlugin: {ex.Message}", ex);
    }
}
```

### 2. Resource Management

```csharp
public class MyRowSource : RowSourceBase<MyEntity>, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public MyRowSource(RuntimeContext runtimeContext)
    {
        _httpClient = new HttpClient();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
```

### 3. Performance Considerations

- Use chunking for large datasets
- Implement pagination when possible
- Consider caching for frequently accessed data
- Use async/await for I/O operations

### 4. Documentation

- Add XML documentation to all public members
- Include usage examples in schema comments
- Document environment variables and configuration

### 5. Versioning

- Follow semantic versioning
- Maintain backward compatibility
- Document breaking changes

---

## Real-World Examples

### Simple File Reader Plugin

```csharp
// Entity representing a CSV row
public class CsvRowEntity
{
    public int LineNumber { get; set; }
    public Dictionary<string, string> Columns { get; set; } = new();
}

// Row source that reads CSV files
internal class CsvRowSource : RowSourceBase<CsvRowEntity>
{
    private readonly string _filePath;

    public CsvRowSource(RuntimeContext runtimeContext, string filePath)
    {
        _filePath = filePath;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var lines = File.ReadAllLines(_filePath);
        var headers = lines[0].Split(',');
        
        var entities = lines.Skip(1).Select((line, index) =>
        {
            var values = line.Split(',');
            var entity = new CsvRowEntity { LineNumber = index + 2 };
            
            for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
            {
                entity.Columns[headers[i]] = values[i];
            }
            
            return entity;
        }).ToList();

        var resolvers = entities.Select(entity => 
            new EntityResolver<CsvRowEntity>(entity, CsvTableHelper.NameToIndexMap, CsvTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }
}
```

### API Integration Plugin

```csharp
// Entity for REST API data
public class ApiEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

// Row source for API integration
internal class ApiRowSource : RowSourceBase<ApiEntity>
{
    private readonly string _apiKey;
    private readonly string _endpoint;

    public ApiRowSource(RuntimeContext runtimeContext, string? endpoint = null)
    {
        _apiKey = runtimeContext.EnvironmentVariables["API_KEY"];
        _endpoint = endpoint ?? runtimeContext.EnvironmentVariables["API_ENDPOINT"];
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var response = httpClient.GetStringAsync(_endpoint).GetAwaiter().GetResult();
        var apiData = JsonSerializer.Deserialize<List<ApiEntity>>(response) ?? new List<ApiEntity>();

        var resolvers = apiData.Select(entity => 
            new EntityResolver<ApiEntity>(entity, ApiTableHelper.NameToIndexMap, ApiTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }
}
```

### Common Modifications from Quick Start

#### Reading from a File
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    var lines = File.ReadAllLines("data.txt");
    var entities = lines.Select((line, index) => new SimpleEntity
    {
        Id = index.ToString(),
        Name = line,
        Created = DateTime.Now,
        Value = line.Length
    }).ToList();

    // Convert to resolvers and add to collection...
}
```

#### Making HTTP Requests
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    using var client = new HttpClient();
    var json = client.GetStringAsync("https://api.example.com/data").Result;
    var entities = JsonSerializer.Deserialize<List<SimpleEntity>>(json) ?? new List<SimpleEntity>();

    // Convert to resolvers and add to collection...
}
```

#### Database Connection
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    using var connection = new SqlConnection(_connectionString);
    connection.Open();
    
    var command = new SqlCommand("SELECT * FROM MyTable", connection);
    using var reader = command.ExecuteReader();
    
    var entities = new List<SimpleEntity>();
    while (reader.Read())
    {
        entities.Add(new SimpleEntity
        {
            Id = reader["Id"].ToString()!,
            Name = reader["Name"].ToString()!,
            Created = Convert.ToDateTime(reader["Created"]),
            Value = Convert.ToInt32(reader["Value"])
        });
    }

    // Convert to resolvers and add to collection...
}
```

---

## Common Use Cases

### 🌐 Web API Integration
Query REST APIs, GraphQL endpoints, or web services using SQL syntax.

### 🗄️ Custom Database Connectors
Connect to proprietary databases or data stores not supported by standard providers.

### 📁 File System Operations
Query files, directories, logs, or any file-based data sources.

### ☁️ Cloud Service Integration
Query cloud services like AWS, Azure, GCP resources, or SaaS platforms.

### 🔧 System Monitoring
Query system metrics, performance counters, or monitoring data.

### 📊 Data Processing Pipelines
Transform and query data from ETL processes or data pipelines.

---

## Working Example Plugin

A complete, functional plugin implementation is available in the repository at `Musoq.DataSources.Example/` demonstrating:

- Proper project structure with all required files
- Entity, Table, RowSource, Schema, and Library implementations
- Multiple constructor patterns
- Custom functions with entity injection
- Comprehensive documentation
- Successfully builds and integrates with the solution

Study this example to see all concepts in action and as a reference for your own plugins.

---

## Plugin Development Workflow

1. **Plan Your Schema**: Define what tables and functions you need
2. **Design Your Entity**: Model your data structure
3. **Implement Core Components**: Schema, Table, RowSource, Entity
4. **Add Custom Functions**: Extend with domain-specific operations
5. **Test Thoroughly**: Unit tests, integration tests, real-world usage
6. **Document**: Add comprehensive documentation and examples
7. **Package**: Create NuGet package for distribution

---

## Plugin Architecture Patterns

The guides demonstrate several architectural patterns found in existing plugins:

1. **Simple Data Source** (like System plugin)
   - Basic entity with simple properties
   - Static data generation
   - Minimal configuration

2. **API Integration** (like OpenAI/Ollama plugins)
   - HTTP client integration
   - Configuration through environment variables
   - Custom functions for data processing

3. **Complex Data Source** (like Docker/Kubernetes plugins)
   - Multiple related tables
   - External service integration
   - Rich metadata and helper functions

4. **File-based Sources** (demonstrated in examples)
   - File system operations
   - Data parsing and transformation
   - Streaming large datasets

---

## Available Plugins in This Repository

Study these existing plugins for real-world examples:

- **OpenAI** - LLM integration with custom AI functions
- **Docker** - Container, image, network, and volume management
- **Kubernetes** - k8s resource queries
- **System** - System utilities and range generators
- **Postgres** - Database connectivity patterns
- **Git** - Version control system integration
- **Time** - Date/time manipulation utilities

---

## Support and Community

- **Issues**: Report bugs or request features in the GitHub repository
- **Discussions**: Join the community discussions for questions and ideas
- **Examples**: Check existing plugins for patterns and inspiration
- **Documentation**: Contribute to improving these guides

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Ready to build your first Musoq plugin?** Start with the [Quick Start](#quick-start-5-minutes) section above and have a working plugin in minutes!

This comprehensive guide provides everything needed for users or AI agents to create powerful, production-ready Musoq plugins. The combination of quick-start templates, detailed explanations, and working examples covers all skill levels and use cases, making Musoq plugin development accessible and efficient.