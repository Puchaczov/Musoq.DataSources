# Musoq Plugin Development Tutorial

Welcome to the comprehensive tutorial for creating Musoq plugins! In this guide, I'll teach you how to build powerful data source plugins from the ground up. By the end of this tutorial, you'll understand every component of a Musoq plugin and be able to create your own to query any data source using SQL-like syntax.

## Table of Contents

1. [Understanding Musoq Plugins](#understanding-musoq-plugins)
2. [Prerequisites and Setup](#prerequisites-and-setup)
3. [Core Concepts and Architecture](#core-concepts-and-architecture)
4. [Building Your First Plugin](#building-your-first-plugin)
5. [Understanding Each Component](#understanding-each-component)
6. [Essential XML Metadata](#essential-xml-metadata)
7. [Documentation and Build Configuration](#documentation-and-build-configuration)
8. [Testing and Validation](#testing-and-validation)
9. [Advanced Patterns and Features](#advanced-patterns-and-features)
10. [Best Practices and Common Patterns](#best-practices-and-common-patterns)
11. [Learning from Real-World Examples](#learning-from-real-world-examples)
12. [Common Use Cases](#common-use-cases)
13. [Support and Community](#support-and-community)

---

## Understanding Musoq Plugins

### What is a Musoq Plugin?

A Musoq plugin is a .NET library that extends Musoq's capability to query data sources that aren't natively supported. Think of it as a bridge between your data and SQL queries.

**Example**: Instead of writing custom code to parse JSON files and filter data, you can write:
```sql
SELECT Name, Age FROM #json.file('users.json') WHERE Age > 25
```

### How Plugins Work

When Musoq encounters a query like `#myplugin.table()`, it:

1. **Locates your plugin** using the schema name (`myplugin`)
2. **Instantiates your schema class** to understand available tables
3. **Creates a row source** to fetch data from your data source
4. **Maps your data** to SQL-queryable rows and columns
5. **Applies SQL operations** (WHERE, JOIN, GROUP BY, etc.) on your data

### Plugin Lifecycle

```
SQL Query → Schema Resolution → Table Metadata → Row Source Creation → Data Retrieval → SQL Processing → Results
```

Let's understand each step:

**1. Schema Resolution**: Musoq finds your plugin by name
**2. Table Metadata**: Your plugin describes what columns are available
**3. Row Source Creation**: Your plugin creates an object to fetch data
**4. Data Retrieval**: Your plugin fetches actual data from the source
**5. SQL Processing**: Musoq applies SQL operations on your data

---

## Prerequisites and Setup

Before we begin building plugins, let's make sure you have everything you need and understand the development environment.

### Required Tools

- **.NET 8.0 SDK or later** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **A code editor** - Visual Studio, VS Code, or any C# IDE
- **Basic C# knowledge** - Understanding classes, interfaces, and async programming
- **SQL familiarity** - Basic SELECT, WHERE, JOIN concepts

### Understanding the Development Environment

When you build a Musoq plugin, you're creating a **class library** that Musoq can dynamically load. Your plugin will be compiled into a DLL that other applications can reference and use.

**Key concept**: Musoq loads your plugin at runtime and uses reflection to discover its capabilities. This means your plugin must follow specific conventions and interfaces.

### Setting Up Your Development Environment

1. **Create a workspace** for your plugin development:
   ```bash
   mkdir MyMusoqPlugin
   cd MyMusoqPlugin
   ```

2. **Understand the Musoq ecosystem** by examining existing plugins in this repository. Each plugin in the `Musoq.DataSources.*` folders demonstrates different patterns and complexity levels.

3. **Clone the repository** to have reference implementations available:
   ```bash
   git clone https://github.com/Puchaczov/Musoq.DataSources.git
   ```

---

## Core Concepts and Architecture

Before we start coding, let's understand the fundamental concepts that make Musoq plugins work.

### The Five Essential Components

Every Musoq plugin consists of exactly five key components. Think of them as the building blocks:

#### 1. **Schema** - The Plugin's Main Interface
- **Purpose**: The entry point that tells Musoq "I exist and here's what I can do"
- **Responsibility**: Defines available tables and handles requests
- **Analogy**: Like a restaurant menu - it lists what's available

#### 2. **Entity** - Your Data Model  
- **Purpose**: Represents the structure of your data
- **Responsibility**: Defines properties that become SQL columns
- **Analogy**: Like a database table schema - defines what fields exist

#### 3. **Table** - Column Metadata Definition
- **Purpose**: Describes the structure and types of your data to Musoq
- **Responsibility**: Maps entity properties to SQL column information
- **Analogy**: Like column definitions in CREATE TABLE statement

#### 4. **RowSource** - The Data Fetcher
- **Purpose**: Actually retrieves data from your data source
- **Responsibility**: Connects to external systems and returns data
- **Analogy**: Like a database driver - handles the actual data retrieval

#### 5. **Helper** - The Column Mapping Bridge
- **Purpose**: Creates efficient mappings between entity properties and table columns
- **Responsibility**: Provides fast access patterns for data retrieval
- **Analogy**: Like an index - speeds up data access

### How These Components Work Together

```
SQL Query: SELECT Name FROM #myplugin.users()
    ↓
1. Schema receives the request for "users" table
    ↓  
2. Schema asks Table for column information about "users"
    ↓
3. Schema creates RowSource to fetch actual user data
    ↓
4. RowSource uses Entity to structure the data
    ↓
5. Helper provides efficient column access patterns
    ↓
6. Data flows back as SQL-queryable results
```

### The Plugin Directory Structure

Here's how we'll organize our plugin files:

```
Musoq.DataSources.MyPlugin/
├── AssemblyInfo.cs              # Plugin registration
├── MyPluginSchema.cs            # Main schema class (Component #1)
├── Entities/
│   └── MyEntity.cs              # Data model (Component #2)
├── Tables/
│   ├── MyTable.cs               # Table metadata (Component #3)
│   └── MyTableHelper.cs         # Column mappings (Component #5)
├── Sources/
│   └── MyRowSource.cs           # Data fetcher (Component #4)
├── MyPluginLibrary.cs           # Custom functions (optional)
└── MyPlugin.csproj              # Project configuration
```

**Why this structure?** Each component has a distinct responsibility, making the code easier to understand, test, and maintain.

### Understanding Data Flow

Let's trace through what happens when someone runs a SQL query:

**Step 1: Discovery**
- User runs: `SELECT * FROM #weather.current()`
- Musoq looks for a schema named "weather"

**Step 2: Schema Resolution**
- Your Schema class gets instantiated
- Musoq calls `GetTableByName("current", ...)` on your schema

**Step 3: Metadata Resolution**
- Your Table class describes what columns are available
- Helper class provides efficient access patterns

**Step 4: Data Retrieval**
- Musoq calls `GetRowSource("current", ...)` on your schema
- Your RowSource fetches actual weather data
- Data gets packaged into Entity objects

**Step 5: SQL Processing**
- Musoq applies SQL operations (WHERE, ORDER BY, etc.) on your data
- Results are returned to the user

---

## Building Your First Plugin

Now let's build a complete plugin from scratch! We'll create a "Weather" plugin that provides current weather data. I'll guide you through each step, explaining why we're doing what we're doing.

### Step 1: Create the Project Foundation

First, let's create a new .NET class library:

```bash
dotnet new classlib -n Musoq.DataSources.Weather
cd Musoq.DataSources.Weather
```

**Why this naming?** The `Musoq.DataSources.*` naming convention helps organize plugins and indicates their purpose clearly.

### Step 2: Configure the Project File

Replace the contents of `Musoq.DataSources.Weather.csproj`:

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
        <PackageProjectUrl>https://github.com/YourGitHub/Weather-Plugin</PackageProjectUrl>
        <PackageLicenseFile>LICENSE</PackageLicenseFile>
        <PackageTags>sql, weather, dotnet-core</PackageTags>
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <IncludeSymbols>true</IncludeSymbols>
        <SymbolPackageFormat>snupkg</SymbolPackageFormat>
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <PackageId>Musoq.DataSources.Weather</PackageId>
    </PropertyGroup>

    <!-- CRITICAL: This target ensures XML documentation is included -->
    <Target Name="_ResolveCopyLocalNuGetPackageXmls" AfterTargets="ResolveReferences">
        <ItemGroup>
            <ReferenceCopyLocalPaths Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')" 
                                    Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)' != '' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
        </ItemGroup>
    </Target>

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
```

**Key points about this configuration:**
- `GenerateDocumentationFile` enables XML documentation generation (critical!)
- `EnableDynamicLoading` allows Musoq to load your plugin at runtime
- The special `_ResolveCopyLocalNuGetPackageXmls` target includes XML metadata in packages
- We reference the three essential Musoq packages

### Step 3: Register Your Plugin

Create `AssemblyInfo.cs` in the root directory:

```csharp
using Musoq.Schema.Attributes;

[assembly: PluginSchemas("weather")]
```

**What's happening here?** This tells Musoq that your assembly contains a schema named "weather". When someone writes `#weather.something()`, Musoq will look for this registration.

### Step 4: Design Your Data Model (Entity)

Create the `Entities/` directory and add `WeatherEntity.cs`:

```csharp
namespace Musoq.DataSources.Weather.Entities;

/// <summary>
/// Represents current weather information for a location
/// </summary>
public class WeatherEntity
{
    public string Location { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsRaining { get; set; }
}
```

**Why this structure?** Each property will become a column in our SQL table. We use descriptive names and appropriate data types. The properties are simple values that SQL can easily understand.

### Step 5: Create the Helper (Column Mappings)

Create the `Tables/` directory and add `WeatherTableHelper.cs`:

```csharp
using Musoq.Schema;
using Musoq.DataSources.Weather.Entities;

namespace Musoq.DataSources.Weather.Tables;

internal static class WeatherTableHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<WeatherEntity, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static WeatherTableHelper()
    {
        // Map column names to their index positions
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(WeatherEntity.Location), 0},
            {nameof(WeatherEntity.Temperature), 1},
            {nameof(WeatherEntity.Description), 2},
            {nameof(WeatherEntity.Humidity), 3},
            {nameof(WeatherEntity.WindSpeed), 4},
            {nameof(WeatherEntity.LastUpdated), 5},
            {nameof(WeatherEntity.IsRaining), 6}
        };
        
        // Map column indices to property accessors
        IndexToMethodAccessMap = new Dictionary<int, Func<WeatherEntity, object?>>
        {
            {0, entity => entity.Location},
            {1, entity => entity.Temperature},
            {2, entity => entity.Description},
            {3, entity => entity.Humidity},
            {4, entity => entity.WindSpeed},
            {5, entity => entity.LastUpdated},
            {6, entity => entity.IsRaining}
        };
        
        // Define column metadata for SQL
        Columns = new[]
        {
            new SchemaColumn(nameof(WeatherEntity.Location), 0, typeof(string)),
            new SchemaColumn(nameof(WeatherEntity.Temperature), 1, typeof(double)),
            new SchemaColumn(nameof(WeatherEntity.Description), 2, typeof(string)),
            new SchemaColumn(nameof(WeatherEntity.Humidity), 3, typeof(double)),
            new SchemaColumn(nameof(WeatherEntity.WindSpeed), 4, typeof(double)),
            new SchemaColumn(nameof(WeatherEntity.LastUpdated), 5, typeof(DateTime)),
            new SchemaColumn(nameof(WeatherEntity.IsRaining), 6, typeof(bool))
        };
    }
}
```

**What's this doing?** This helper class creates efficient mappings for Musoq to:
1. Look up columns by name (for `SELECT Location`)
2. Access entity properties by index (for performance)
3. Understand column types and metadata

**Why three mappings?** Each serves a different purpose:
- `NameToIndexMap`: "What's the index of the 'Temperature' column?" → 1
- `IndexToMethodAccessMap`: "How do I get the value at index 1?" → `entity => entity.Temperature`
- `Columns`: "What type is column 1?" → `double`

### Step 6: Create the Table Definition

Add `WeatherTable.cs` in the `Tables/` directory:

```csharp
using Musoq.Schema;
using Musoq.DataSources.Weather.Entities;

namespace Musoq.DataSources.Weather.Tables;

internal class WeatherTable : ISchemaTable
{
    public ISchemaColumn[] Columns => WeatherTableHelper.Columns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(WeatherEntity));

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

**What's the Table class for?** It implements the `ISchemaTable` interface that Musoq uses to understand your table structure. When Musoq needs to know "What columns does this table have?", it asks this class.

### Step 7: Implement the Data Source (RowSource)

Create the `Sources/` directory and add `WeatherRowSource.cs`:

```csharp
using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.DataSources.Weather.Entities;
using Musoq.DataSources.Weather.Tables;

namespace Musoq.DataSources.Weather.Sources;

internal class WeatherRowSource : RowSourceBase<WeatherEntity>
{
    private readonly string _location;
    private readonly CancellationToken _cancellationToken;

    public WeatherRowSource(RuntimeContext runtimeContext, string? location = null)
    {
        // Use provided location or default to environment variable
        _location = location ?? runtimeContext.EnvironmentVariables.GetValueOrDefault("WEATHER_LOCATION", "London");
        _cancellationToken = runtimeContext.EndWorkToken;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        // Fetch weather data
        var weatherData = GetWeatherData();

        // Convert to object resolvers that Musoq can understand
        var resolvers = weatherData.Select(entity => 
            new EntityResolver<WeatherEntity>(entity, WeatherTableHelper.NameToIndexMap, WeatherTableHelper.IndexToMethodAccessMap))
            .ToList();

        // Add to the collection for Musoq to process
        chunkedSource.Add(resolvers);
    }

    private List<WeatherEntity> GetWeatherData()
    {
        // For now, return mock data. In a real plugin, you'd call a weather API
        return new List<WeatherEntity>
        {
            new WeatherEntity 
            { 
                Location = _location,
                Temperature = 22.5,
                Description = "Partly Cloudy",
                Humidity = 65.0,
                WindSpeed = 12.3,
                LastUpdated = DateTime.Now,
                IsRaining = false
            }
        };
    }
}
```

**Understanding the RowSource:** This is where the magic happens! The `CollectChunks` method is called by Musoq when it needs data. Here's what's happening:

1. **Data Retrieval**: `GetWeatherData()` fetches actual data (in a real plugin, this would call an API)
2. **Entity Resolution**: We wrap each entity in an `EntityResolver` that knows how to extract values efficiently
3. **Data Delivery**: We add the data to `chunkedSource` for Musoq to process

**Why chunking?** For large datasets, you can add multiple chunks, allowing Musoq to process data incrementally rather than loading everything into memory.

### Step 8: Create the Schema (Main Interface)

Finally, create the main `WeatherSchema.cs` in the root directory:

```csharp
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using Musoq.DataSources.Weather.Sources;
using Musoq.DataSources.Weather.Tables;

namespace Musoq.DataSources.Weather;

/// <description>
/// Provides access to current weather information for any location
/// </description>
/// <short-description>
/// Weather data source for current conditions
/// </short-description>
/// <project-url>https://github.com/YourGitHub/Weather-Plugin</project-url>
public class WeatherSchema : SchemaBase
{
    private const string SchemaName = "weather";
    private const string CurrentWeatherTable = "current";

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="WEATHER_LOCATION" isRequired="false">Default location for weather queries</environmentVariable>
    /// </environmentVariables>
    /// #weather.current()
    /// </from>
    /// <description>Gets current weather for the default location</description>
    /// <columns>
    /// <column name="Location" type="string">Location name</column>
    /// <column name="Temperature" type="double">Temperature in Celsius</column>
    /// <column name="Description" type="string">Weather condition description</column>
    /// <column name="Humidity" type="double">Humidity percentage</column>
    /// <column name="WindSpeed" type="double">Wind speed in km/h</column>
    /// <column name="LastUpdated" type="DateTime">Last update timestamp</column>
    /// <column name="IsRaining" type="bool">Whether it's currently raining</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Location name (city, coordinates, etc.)</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#weather.current(string location)</from>
    /// <description>Gets current weather for a specific location</description>
    /// <columns>
    /// <column name="Location" type="string">Location name</column>
    /// <column name="Temperature" type="double">Temperature in Celsius</column>
    /// <column name="Description" type="string">Weather condition description</column>
    /// <column name="Humidity" type="double">Humidity percentage</column>
    /// <column name="WindSpeed" type="double">Wind speed in km/h</column>
    /// <column name="LastUpdated" type="DateTime">Last update timestamp</column>
    /// <column name="IsRaining" type="bool">Whether it's currently raining</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public WeatherSchema() : base(SchemaName, CreateLibrary())
    {
    }

    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            CurrentWeatherTable => new WeatherTable(),
            _ => throw new NotSupportedException($"Table '{name}' is not supported.")
        };
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            CurrentWeatherTable => new WeatherRowSource(runtimeContext, parameters.Length > 0 ? parameters[0]?.ToString() : null),
            _ => throw new NotSupportedException($"Table '{name}' is not supported.")
        };
    }

    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<WeatherRowSource>(CurrentWeatherTable));
        return constructors.ToArray();
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        // No custom functions for now
        return new MethodsAggregator(methodsManager);
    }
}
```

**Understanding the Schema class:** This is the conductor of your plugin orchestra. Notice:

1. **XML Documentation**: The extensive comments above the constructor are essential - they tell Musoq how to use your plugin
2. **GetTableByName**: When someone requests `#weather.current()`, this method returns a `WeatherTable`
3. **GetRowSource**: This creates the actual data source that fetches weather data
4. **GetConstructors**: This tells Musoq what parameters your methods accept

### Step 9: Build and Test

Now let's build our plugin:

```bash
dotnet build
```

If everything compiles successfully, congratulations! You've just built your first Musoq plugin.

### Step 10: Understanding What We Built

Let's trace through what happens when someone runs this query:

```sql
SELECT Location, Temperature FROM #weather.current('Paris') WHERE Temperature > 20
```

1. **Musoq sees `#weather.current('Paris')`** and looks for the "weather" schema
2. **Your AssemblyInfo.cs** tells Musoq that this assembly provides the "weather" schema
3. **Musoq instantiates WeatherSchema** and calls `GetTableByName("current", ...)`
4. **WeatherSchema returns a WeatherTable** describing the available columns
5. **Musoq calls GetRowSource("current", ...)** with "Paris" as a parameter
6. **WeatherSchema creates a WeatherRowSource** with location="Paris"
7. **WeatherRowSource.CollectChunks()** fetches weather data for Paris
8. **Musoq applies the SQL operations** (SELECT specific columns, WHERE temperature > 20)
9. **Results are returned** to the user

This is the fundamental flow of every Musoq plugin!

---

## Understanding Each Component

Now that you've built a complete plugin, let's dive deeper into each component to understand exactly what they do and how to customize them for different scenarios.

### Component 1: The Entity - Your Data Model

The Entity represents the structure of your data. It's the C# class that models what a single record looks like.

#### Design Principles for Entities

**1. Keep it simple**: Use basic data types that SQL understands:
```csharp
// Good - Simple, SQL-friendly types
public class UserEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
```

**2. Handle complex data appropriately**: For complex data, use collections or nested objects:
```csharp
// Good - Complex types that make sense
public class ProductEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

**3. Use nullable types when appropriate**:
```csharp
public class WeatherEntity
{
    public string Location { get; set; } = string.Empty;
    public double? Temperature { get; set; }  // Might not be available
    public DateTime? LastUpdated { get; set; } // Might be null for new records
}
```

#### Common Entity Patterns

**Simple Record Entity** (like our weather plugin):
```csharp
public class SimpleEntity
{
    public string Id { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
```

**API Response Entity** (for REST API integrations):
```csharp
public class ApiResponseEntity  
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
```

**File-based Entity** (for file processing):
```csharp
public class FileEntity
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string Content { get; set; } = string.Empty;
}
```

### Component 2: The Helper - Efficient Data Access

The Helper class creates mappings that allow Musoq to efficiently access your entity properties. 

#### Understanding the Three Mappings

Let's look at why each mapping exists:

```csharp
static MyTableHelper()
{
    // 1. Name-to-Index: "What position is the 'Temperature' column?"
    NameToIndexMap = new Dictionary<string, int>
    {
        {nameof(WeatherEntity.Location), 0},     // "Location" → 0
        {nameof(WeatherEntity.Temperature), 1},  // "Temperature" → 1
        {nameof(WeatherEntity.Description), 2}   // "Description" → 2
    };
    
    // 2. Index-to-Accessor: "How do I get the value at position 1?"
    IndexToMethodAccessMap = new Dictionary<int, Func<WeatherEntity, object?>>
    {
        {0, entity => entity.Location},        // Position 0 → get Location
        {1, entity => entity.Temperature},     // Position 1 → get Temperature  
        {2, entity => entity.Description}      // Position 2 → get Description
    };
    
    // 3. Column Metadata: "What type is position 1? What's its name?"
    Columns = new[]
    {
        new SchemaColumn("Location", 0, typeof(string)),        // Position 0: string column
        new SchemaColumn("Temperature", 1, typeof(double)),     // Position 1: double column
        new SchemaColumn("Description", 2, typeof(string))      // Position 2: string column
    };
}
```

**Why not just use reflection?** While we could use reflection to access properties, these pre-built mappings are much faster. When processing thousands of rows, this performance difference matters significantly.

#### Helper Generation Pattern

Here's a useful pattern for generating helpers systematically:

```csharp
private static (Dictionary<string, int> nameToIndex, 
               Dictionary<int, Func<T, object?>> indexToAccessor,
               SchemaColumn[] columns) 
               GenerateMappings<T>()
{
    var properties = typeof(T).GetProperties();
    var nameToIndex = new Dictionary<string, int>();
    var indexToAccessor = new Dictionary<int, Func<T, object?>>();
    var columns = new SchemaColumn[properties.Length];

    for (int i = 0; i < properties.Length; i++)
    {
        var prop = properties[i];
        nameToIndex[prop.Name] = i;
        indexToAccessor[i] = entity => prop.GetValue(entity);
        columns[i] = new SchemaColumn(prop.Name, i, prop.PropertyType);
    }

    return (nameToIndex, indexToAccessor, columns);
}
```

### Component 3: The Table - Schema Definition

The Table class implements `ISchemaTable` and tells Musoq about your table structure.

#### Understanding ISchemaTable

```csharp
public interface ISchemaTable
{
    ISchemaColumn[] Columns { get; }                           // All available columns
    SchemaTableMetadata Metadata { get; }                     // Table metadata
    ISchemaColumn? GetColumnByName(string name);              // Find one column by name
    ISchemaColumn[] GetColumnsByName(string name);            // Find multiple columns by name
}
```

**When would you have multiple columns with the same name?** Rarely, but it can happen with complex schemas or when joining data sources.

#### Custom Table Implementations

Most tables follow this simple pattern:
```csharp
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

### Component 4: The RowSource - Data Retrieval Engine

The RowSource is where you implement the actual data fetching logic. This is the most important and most customizable component.

#### Understanding RowSourceBase<T>

The `RowSourceBase<T>` class provides a framework for data collection:

```csharp
public abstract class RowSourceBase<T> : RowSource
{
    protected abstract void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource);
}
```

**Your job**: Implement `CollectChunks` to fetch data and add it to the collection.

#### Data Fetching Patterns

**Pattern 1: Simple Collection** (like our weather example)
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    // Get all data at once
    var allData = GetAllData();
    
    // Convert to resolvers
    var resolvers = allData.Select(entity => 
        new EntityResolver<MyEntity>(entity, MyTableHelper.NameToIndexMap, MyTableHelper.IndexToMethodAccessMap))
        .ToList();
    
    // Add as a single chunk
    chunkedSource.Add(resolvers);
}
```

**Pattern 2: Chunked Processing** (for large datasets)
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    const int chunkSize = 1000;
    int offset = 0;
    
    while (true)
    {
        var chunk = GetDataChunk(offset, chunkSize);
        if (!chunk.Any()) break;
        
        var resolvers = chunk.Select(entity => 
            new EntityResolver<MyEntity>(entity, MyTableHelper.NameToIndexMap, MyTableHelper.IndexToMethodAccessMap))
            .ToList();
        
        chunkedSource.Add(resolvers);
        offset += chunkSize;
    }
}
```

**Pattern 3: Streaming Data** (for APIs with pagination)
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    string? nextPageToken = null;
    
    do
    {
        var (data, newNextPageToken) = GetDataPage(nextPageToken);
        nextPageToken = newNextPageToken;
        
        if (data.Any())
        {
            var resolvers = data.Select(entity => 
                new EntityResolver<MyEntity>(entity, MyTableHelper.NameToIndexMap, MyTableHelper.IndexToMethodAccessMap))
                .ToList();
            
            chunkedSource.Add(resolvers);
        }
    } 
    while (nextPageToken != null);
}
```

#### Handling Parameters in RowSource

Parameters from SQL queries are passed to your RowSource constructor:

```csharp
public class MyRowSource : RowSourceBase<MyEntity>
{
    private readonly string _query;
    private readonly int _maxResults;

    public MyRowSource(RuntimeContext runtimeContext, string? query = null, int maxResults = 100)
    {
        _query = query ?? "default";
        _maxResults = maxResults;
    }
}
```

**Corresponding SQL**: `SELECT * FROM #myplugin.data('search term', 50)`

#### Using Environment Variables

Access environment variables through the RuntimeContext:

```csharp
public MyRowSource(RuntimeContext runtimeContext)
{
    // Required variable - will throw if missing
    var apiKey = runtimeContext.EnvironmentVariables["API_KEY"];
    
    // Optional with default
    var baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("BASE_URL", "https://api.example.com");
    
    // Check if variable exists
    if (runtimeContext.EnvironmentVariables.TryGetValue("OPTIONAL_VAR", out var value))
    {
        // Use the optional variable
    }
}
```

### Component 5: The Schema - The Orchestrator

The Schema class is the main coordinator that ties everything together.

#### Understanding Schema Responsibilities

1. **Table Resolution**: "What table does 'users' refer to?"
2. **Row Source Creation**: "Create a data source for the 'users' table"
3. **Constructor Information**: "What parameters does the 'users' table accept?"
4. **Library Integration**: "What custom functions are available?"

#### Schema Method Patterns

**Simple Schema** (single table):
```csharp
public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
{
    return name.ToLowerInvariant() switch
    {
        "data" => new MyTable(),
        _ => throw new NotSupportedException($"Table '{name}' is not supported.")
    };
}

public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
{
    return name.ToLowerInvariant() switch
    {
        "data" => new MyRowSource(runtimeContext),
        _ => throw new NotSupportedException($"Table '{name}' is not supported.")
    };
}
```

**Multi-table Schema** (multiple related tables):
```csharp
public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
{
    return name.ToLowerInvariant() switch
    {
        "users" => new UsersTable(),
        "orders" => new OrdersTable(),
        "products" => new ProductsTable(),
        _ => throw new NotSupportedException($"Table '{name}' is not supported.")
    };
}

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

---

## Essential XML Metadata

Now that you understand the basic components, let's learn about one of the most critical aspects of plugin development: XML metadata annotations. These annotations are not just documentation—they're essential for Musoq to understand and use your plugin correctly.

### Why XML Metadata Matters

When you write SQL like `SELECT * FROM #weather.current('Paris')`, Musoq needs to know:

- **What is the 'weather' schema?** (Your plugin's purpose)
- **What is the 'current' method?** (Available tables/functions)
- **What parameters does it accept?** ('Paris' string parameter)
- **What columns will be returned?** (Location, Temperature, etc.)
- **Are there any requirements?** (API keys, environment variables)

The XML metadata provides all this information. **Without proper XML metadata, your plugin won't work correctly with Musoq.**

### Understanding XML Metadata Structure

XML metadata goes in the constructor comments of your Schema class. Here's the basic structure:

```csharp
/// <description>
/// Main description of what your plugin does
/// </description>
/// <short-description>
/// Brief one-line description
/// </short-description>
/// <project-url>https://github.com/YourRepo/YourPlugin</project-url>
public class YourSchema : SchemaBase
{
    /// <virtual-constructors>
    /// <!-- Documentation for each table/method goes here -->
    /// </virtual-constructors>
    public YourSchema() : base("yourschema", CreateLibrary())
    {
    }
}
```

### Step-by-Step: Adding XML Metadata to Our Weather Plugin

Let's enhance our weather plugin with comprehensive XML metadata. I'll show you exactly what each part does.

#### Step 1: Document the Schema Purpose

```csharp
/// <description>
/// Provides access to current weather information for any location worldwide.
/// Supports querying real-time weather conditions including temperature, humidity,
/// wind speed, and weather descriptions.
/// </description>
/// <short-description>
/// Real-time weather data source
/// </short-description>
/// <project-url>https://github.com/YourGitHub/Weather-Plugin</project-url>
public class WeatherSchema : SchemaBase
```

**What this does:**
- `<description>`: Detailed explanation shown in help systems
- `<short-description>`: Brief summary for quick reference
- `<project-url>`: Link to your plugin's documentation

#### Step 2: Document Virtual Constructors (Methods)

Virtual constructors define how users can call your plugin. Each method needs documentation:

```csharp
/// <virtual-constructors>
/// <virtual-constructor>
/// <examples>
/// <example>
/// <from>
/// <environmentVariables>
/// <environmentVariable name="WEATHER_API_KEY" isRequired="true">API key for weather service</environmentVariable>
/// <environmentVariable name="WEATHER_LOCATION" isRequired="false">Default location for weather queries</environmentVariable>
/// </environmentVariables>
/// #weather.current()
/// </from>
/// <description>Gets current weather for the default location (from WEATHER_LOCATION environment variable)</description>
/// <columns>
/// <column name="Location" type="string">Location name</column>
/// <column name="Temperature" type="double">Temperature in Celsius</column>
/// <column name="Description" type="string">Weather condition description</column>
/// <column name="Humidity" type="double">Humidity percentage (0-100)</column>
/// <column name="WindSpeed" type="double">Wind speed in km/h</column>
/// <column name="LastUpdated" type="DateTime">Last update timestamp</column>
/// <column name="IsRaining" type="bool">Whether it's currently raining</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
/// </virtual-constructors>
```

**Breaking this down:**

1. **Environment Variables**: Documents what environment variables your plugin uses
   - `isRequired="true"`: Must be set or plugin fails
   - `isRequired="false"`: Optional, plugin provides defaults

2. **From**: Shows exactly how to call your plugin in SQL
   - `#weather.current()`: The actual SQL syntax

3. **Description**: Explains what this specific method does

4. **Columns**: Documents each column returned by this method
   - `name`: Column name as it appears in SQL
   - `type`: .NET type (string, int, double, DateTime, bool, etc.)
   - Description text: What this column contains

#### Step 3: Document Method Overloads

When your plugin accepts parameters, document each variation:

```csharp
/// <virtual-constructor>
/// <virtual-param>Location name (city, coordinates, or address)</virtual-param>
/// <examples>
/// <example>
/// <from>#weather.current(string location)</from>
/// <description>Gets current weather for a specific location</description>
/// <columns>
/// <column name="Location" type="string">Location name</column>
/// <column name="Temperature" type="double">Temperature in Celsius</column>
/// <column name="Description" type="string">Weather condition description</column>
/// <column name="Humidity" type="double">Humidity percentage (0-100)</column>
/// <column name="WindSpeed" type="double">Wind speed in km/h</column>
/// <column name="LastUpdated" type="DateTime">Last update timestamp</column>
/// <column name="IsRaining" type="bool">Whether it's currently raining</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
```

**Key points:**
- `<virtual-param>`: Documents each parameter your method accepts
- Multiple `<virtual-constructor>` blocks for different parameter combinations
- Each parameter gets its own description

### Understanding Column Types

Musoq supports various .NET types in columns. Here's how to document them properly:

#### Basic Types
```xml
<column name="Id" type="int">Unique identifier</column>
<column name="Name" type="string">Display name</column>
<column name="Price" type="decimal">Price in USD</column>
<column name="IsActive" type="bool">Whether item is active</column>
<column name="CreatedDate" type="DateTime">Creation timestamp</column>
```

#### Nullable Types
```xml
<column name="OptionalDate" type="DateTime?">Optional date field</column>
<column name="OptionalPrice" type="decimal?">Price if available</column>
```

#### Collections and Arrays
```xml
<column name="Tags" type="string[]">Array of tag strings</column>
<column name="Categories" type="IList&lt;string&gt;">List of categories</column>
<column name="Metadata" type="IDictionary&lt;string, object&gt;">Key-value metadata</column>
```

**Note**: Use `&lt;` and `&gt;` instead of `<` and `>` in XML for generic types.

#### Custom Object Types
```xml
<column name="Address" type="AddressEntity">Address information object</column>
<column name="Permissions" type="PermissionEntity[]">Array of permission objects</column>
```

### Dynamic vs Static Columns

Sometimes you don't know the columns at compile time. For example, querying a database table or an API that returns varying schemas.

#### Static Columns (You Know the Structure)
```xml
<columns>
<column name="Id" type="string">User identifier</column>
<column name="Name" type="string">Full name</column>
<column name="Email" type="string">Email address</column>
</columns>
```

#### Dynamic Columns (Structure Determined at Runtime)
```xml
<columns isDynamic="true"></columns>
```

**When to use dynamic columns:**
- Database plugins (table structures vary)
- API plugins (response schemas change)
- File plugins (CSV files with unknown headers)
- AI plugins (responses have varying formats)

### Environment Variable Patterns

Document environment variables to help users configure your plugin:

#### Required Variables
```xml
<environmentVariables>
<environmentVariable name="API_KEY" isRequired="true">Your service API key</environmentVariable>
<environmentVariable name="API_SECRET" isRequired="true">Your service API secret</environmentVariable>
</environmentVariables>
```

#### Optional Variables with Defaults
```xml
<environmentVariables>
<environmentVariable name="BASE_URL" isRequired="false">Custom API base URL (default: https://api.service.com)</environmentVariable>
<environmentVariable name="TIMEOUT_SECONDS" isRequired="false">Request timeout in seconds (default: 30)</environmentVariable>
</environmentVariables>
```

#### Using Environment Variables in Code

After documenting them, use environment variables in your RowSource:

```csharp
public class WeatherRowSource : RowSourceBase<WeatherEntity>
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly int _timeoutSeconds;

    public WeatherRowSource(RuntimeContext runtimeContext, string? location = null)
    {
        // Required variables - will throw if missing
        _apiKey = runtimeContext.EnvironmentVariables["WEATHER_API_KEY"];
        
        // Optional variables with defaults
        _baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("BASE_URL", "https://api.openweathermap.org");
        _timeoutSeconds = int.Parse(runtimeContext.EnvironmentVariables.GetValueOrDefault("TIMEOUT_SECONDS", "30"));
        
        _location = location ?? runtimeContext.EnvironmentVariables.GetValueOrDefault("WEATHER_LOCATION", "London");
    }
}
```

### Complete XML Metadata Example

Here's our weather plugin with complete, comprehensive XML metadata:

```csharp
/// <description>
/// Provides access to current weather information for any location worldwide.
/// Supports querying real-time weather conditions including temperature, humidity,
/// wind speed, and weather descriptions through integration with weather APIs.
/// </description>
/// <short-description>
/// Real-time weather data source
/// </short-description>
/// <project-url>https://github.com/YourGitHub/Weather-Plugin</project-url>
public class WeatherSchema : SchemaBase
{
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="WEATHER_API_KEY" isRequired="true">API key for weather service (get from openweathermap.org)</environmentVariable>
    /// <environmentVariable name="WEATHER_LOCATION" isRequired="false">Default location for weather queries (default: London)</environmentVariable>
    /// </environmentVariables>
    /// #weather.current()
    /// </from>
    /// <description>Gets current weather for the default location specified in WEATHER_LOCATION environment variable</description>
    /// <columns>
    /// <column name="Location" type="string">Location name or coordinates</column>
    /// <column name="Temperature" type="double">Temperature in Celsius</column>
    /// <column name="Description" type="string">Weather condition description (e.g., "Partly Cloudy")</column>
    /// <column name="Humidity" type="double">Humidity percentage (0-100)</column>
    /// <column name="WindSpeed" type="double">Wind speed in kilometers per hour</column>
    /// <column name="LastUpdated" type="DateTime">Timestamp when weather data was last updated</column>
    /// <column name="IsRaining" type="bool">Whether it's currently raining at the location</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Location name, coordinates (lat,lon), or address</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#weather.current(string location)</from>
    /// <description>Gets current weather for a specific location</description>
    /// <columns>
    /// <column name="Location" type="string">Location name or coordinates</column>
    /// <column name="Temperature" type="double">Temperature in Celsius</column>
    /// <column name="Description" type="string">Weather condition description (e.g., "Partly Cloudy")</column>
    /// <column name="Humidity" type="double">Humidity percentage (0-100)</column>
    /// <column name="WindSpeed" type="double">Wind speed in kilometers per hour</column>
    /// <column name="LastUpdated" type="DateTime">Timestamp when weather data was last updated</column>
    /// <column name="IsRaining" type="bool">Whether it's currently raining at the location</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public WeatherSchema() : base("weather", CreateLibrary())
    {
    }
}
```

This comprehensive XML metadata enables:
- **IntelliSense support** in IDEs
- **Help system integration** for documentation
- **Parameter validation** by Musoq
- **Schema discovery** for tools and applications

---

## Documentation and Build Configuration

Now that you understand XML metadata, let's learn how to configure your project so that this metadata gets properly generated and included in your plugin package.

### Critical Build Configuration

⚠️ **Without proper build configuration, your XML metadata won't be available to Musoq at runtime!**

#### The Essential .csproj Configuration

Every Musoq plugin **must** include this exact configuration in your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        
        <!-- CRITICAL: These two properties are mandatory -->
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        
        <!-- Package metadata -->
        <Version>1.0.0</Version>
        <Authors>Your Name</Authors>
        <Product>Musoq</Product>
        <PackageProjectUrl>https://github.com/YourGitHub/Weather-Plugin</PackageProjectUrl>
        <PackageLicenseFile>LICENSE</PackageLicenseFile>
        <PackageTags>sql, weather, dotnet-core</PackageTags>
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <IncludeSymbols>true</IncludeSymbols>
        <SymbolPackageFormat>snupkg</SymbolPackageFormat>
        <PackageId>Musoq.DataSources.Weather</PackageId>
    </PropertyGroup>

    <!-- CRITICAL: This target ensures XML documentation is packaged correctly -->
    <Target Name="_ResolveCopyLocalNuGetPackageXmls" AfterTargets="ResolveReferences">
        <ItemGroup>
            <ReferenceCopyLocalPaths Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')" 
                                    Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)' != '' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
        </ItemGroup>
    </Target>

    <!-- Musoq dependencies -->
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

#### Understanding Each Configuration Element

**`<EnableDynamicLoading>true</EnableDynamicLoading>`**
- Allows Musoq to load your plugin at runtime
- Without this, your plugin can't be dynamically discovered

**`<GenerateDocumentationFile>true</GenerateDocumentationFile>`**
- Generates XML documentation from your XML comments
- Creates a `.xml` file alongside your `.dll`

**`<Target Name="_ResolveCopyLocalNuGetPackageXmls" ...>`**
- Ensures XML documentation files are included in NuGet packages
- **This is the most commonly forgotten but critical piece**
- Without this target, XML metadata won't be available at runtime

#### Verifying Your Configuration

After building your plugin, check that XML files are generated:

```bash
dotnet build
ls bin/Debug/net8.0/
```

You should see both:
- `Musoq.DataSources.Weather.dll` (your compiled plugin)
- `Musoq.DataSources.Weather.xml` (your XML metadata)

If the `.xml` file is missing, check your configuration.

### Understanding the Documentation Generation Process

Here's what happens when you build your plugin:

1. **Compilation**: C# compiler processes your code
2. **XML Generation**: Compiler extracts XML comments and creates `.xml` file
3. **Package Creation**: MSBuild packages both `.dll` and `.xml` files
4. **Runtime Discovery**: Musoq loads your plugin and reads the XML metadata

### Testing Your Configuration

Create this simple test to verify your XML metadata is working:

```csharp
// Add this test method to verify XML generation
[Fact]
public void VerifyXmlDocumentationExists()
{
    var assemblyLocation = typeof(WeatherSchema).Assembly.Location;
    var xmlPath = Path.ChangeExtension(assemblyLocation, ".xml");
    
    Assert.True(File.Exists(xmlPath), $"XML documentation file not found at {xmlPath}");
    
    var xmlContent = File.ReadAllText(xmlPath);
    Assert.Contains("WeatherSchema", xmlContent);
    Assert.Contains("virtual-constructors", xmlContent);
}
```

---

## Testing and Validation

Testing your plugin ensures it works correctly and integrates properly with Musoq. Let's learn how to create comprehensive tests for all aspects of your plugin.

### Setting Up Your Test Project

Create a test project alongside your plugin:

```bash
dotnet new xunit -n Musoq.DataSources.Weather.Tests
cd Musoq.DataSources.Weather.Tests
```

Configure the test project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
        <PackageReference Include="xunit" Version="2.4.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
        <PackageReference Include="Moq" Version="4.20.69" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Musoq.DataSources.Weather\Musoq.DataSources.Weather.csproj" />
    </ItemGroup>
</Project>
```

### Testing Strategy: The Five-Layer Approach

Test each component of your plugin independently:

#### 1. Entity Testing
```csharp
public class WeatherEntityTests
{
    [Fact]
    public void WeatherEntity_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var entity = new WeatherEntity();

        // Assert
        Assert.Equal(string.Empty, entity.Location);
        Assert.Equal(string.Empty, entity.Description);
        Assert.Equal(0.0, entity.Temperature);
        Assert.Equal(0.0, entity.Humidity);
        Assert.Equal(0.0, entity.WindSpeed);
        Assert.False(entity.IsRaining);
    }

    [Fact]
    public void WeatherEntity_Should_Accept_All_Property_Values()
    {
        // Arrange
        var entity = new WeatherEntity
        {
            Location = "Paris",
            Temperature = 22.5,
            Description = "Sunny",
            Humidity = 65.0,
            WindSpeed = 12.3,
            LastUpdated = DateTime.Now,
            IsRaining = false
        };

        // Assert
        Assert.Equal("Paris", entity.Location);
        Assert.Equal(22.5, entity.Temperature);
        Assert.Equal("Sunny", entity.Description);
        Assert.Equal(65.0, entity.Humidity);
        Assert.Equal(12.3, entity.WindSpeed);
        Assert.False(entity.IsRaining);
    }
}
```

#### 2. Helper Testing
```csharp
public class WeatherTableHelperTests
{
    [Fact]
    public void NameToIndexMap_Should_Map_All_Properties()
    {
        // Arrange & Act
        var map = WeatherTableHelper.NameToIndexMap;

        // Assert
        Assert.Equal(0, map[nameof(WeatherEntity.Location)]);
        Assert.Equal(1, map[nameof(WeatherEntity.Temperature)]);
        Assert.Equal(2, map[nameof(WeatherEntity.Description)]);
        Assert.Equal(3, map[nameof(WeatherEntity.Humidity)]);
        Assert.Equal(4, map[nameof(WeatherEntity.WindSpeed)]);
        Assert.Equal(5, map[nameof(WeatherEntity.LastUpdated)]);
        Assert.Equal(6, map[nameof(WeatherEntity.IsRaining)]);
    }

    [Fact]
    public void IndexToMethodAccessMap_Should_Return_Correct_Values()
    {
        // Arrange
        var entity = new WeatherEntity
        {
            Location = "Tokyo",
            Temperature = 25.0,
            Description = "Clear",
            Humidity = 70.0,
            WindSpeed = 8.5,
            LastUpdated = new DateTime(2024, 1, 1),
            IsRaining = true
        };

        var accessor = WeatherTableHelper.IndexToMethodAccessMap;

        // Act & Assert
        Assert.Equal("Tokyo", accessor[0](entity));
        Assert.Equal(25.0, accessor[1](entity));
        Assert.Equal("Clear", accessor[2](entity));
        Assert.Equal(70.0, accessor[3](entity));
        Assert.Equal(8.5, accessor[4](entity));
        Assert.Equal(new DateTime(2024, 1, 1), accessor[5](entity));
        Assert.True((bool)accessor[6](entity)!);
    }

    [Fact]
    public void Columns_Should_Have_Correct_Metadata()
    {
        // Arrange & Act
        var columns = WeatherTableHelper.Columns;

        // Assert
        Assert.Equal(7, columns.Length);
        Assert.Equal("Location", columns[0].ColumnName);
        Assert.Equal(typeof(string), columns[0].ColumnType);
        Assert.Equal("Temperature", columns[1].ColumnName);
        Assert.Equal(typeof(double), columns[1].ColumnType);
    }
}
```

#### 3. Table Testing
```csharp
public class WeatherTableTests
{
    [Fact]
    public void Table_Should_Return_Correct_Columns()
    {
        // Arrange
        var table = new WeatherTable();

        // Act
        var columns = table.Columns;

        // Assert
        Assert.Equal(7, columns.Length);
        Assert.Contains(columns, c => c.ColumnName == "Location");
        Assert.Contains(columns, c => c.ColumnName == "Temperature");
    }

    [Fact]
    public void GetColumnByName_Should_Return_Correct_Column()
    {
        // Arrange
        var table = new WeatherTable();

        // Act
        var column = table.GetColumnByName("Temperature");

        // Assert
        Assert.NotNull(column);
        Assert.Equal("Temperature", column.ColumnName);
        Assert.Equal(typeof(double), column.ColumnType);
    }

    [Fact]
    public void GetColumnByName_Should_Return_Null_For_Unknown_Column()
    {
        // Arrange
        var table = new WeatherTable();

        // Act
        var column = table.GetColumnByName("NonExistentColumn");

        // Assert
        Assert.Null(column);
    }
}
```

#### 4. RowSource Testing
```csharp
public class WeatherRowSourceTests
{
    [Fact]
    public void RowSource_Should_Return_Data_With_Default_Location()
    {
        // Arrange
        var environmentVars = new Dictionary<string, string>();
        var runtimeContext = new RuntimeContext(CancellationToken.None, environmentVars);
        var rowSource = new WeatherRowSource(runtimeContext);

        // Act
        var rows = rowSource.Rows.ToList();

        // Assert
        Assert.NotEmpty(rows);
        Assert.Single(rows);
        
        var resolver = rows.First();
        Assert.Equal("London", resolver[0]); // Default location
    }

    [Fact]
    public void RowSource_Should_Use_Provided_Location()
    {
        // Arrange
        var environmentVars = new Dictionary<string, string>();
        var runtimeContext = new RuntimeContext(CancellationToken.None, environmentVars);
        var rowSource = new WeatherRowSource(runtimeContext, "Paris");

        // Act
        var rows = rowSource.Rows.ToList();

        // Assert
        Assert.NotEmpty(rows);
        var resolver = rows.First();
        Assert.Equal("Paris", resolver[0]); // Provided location
    }

    [Fact]
    public void RowSource_Should_Use_Environment_Variable_Location()
    {
        // Arrange
        var environmentVars = new Dictionary<string, string>
        {
            ["WEATHER_LOCATION"] = "Tokyo"
        };
        var runtimeContext = new RuntimeContext(CancellationToken.None, environmentVars);
        var rowSource = new WeatherRowSource(runtimeContext);

        // Act
        var rows = rowSource.Rows.ToList();

        // Assert
        Assert.NotEmpty(rows);
        var resolver = rows.First();
        Assert.Equal("Tokyo", resolver[0]); // Environment variable location
    }
}
```

#### 5. Schema Integration Testing
```csharp
public class WeatherSchemaTests
{
    [Fact]
    public void Schema_Should_Return_Correct_Table()
    {
        // Arrange
        var schema = new WeatherSchema();
        var runtimeContext = new RuntimeContext(CancellationToken.None, new Dictionary<string, string>());

        // Act
        var table = schema.GetTableByName("current", runtimeContext);

        // Assert
        Assert.NotNull(table);
        Assert.IsType<WeatherTable>(table);
    }

    [Fact]
    public void Schema_Should_Return_Correct_RowSource()
    {
        // Arrange
        var schema = new WeatherSchema();
        var runtimeContext = new RuntimeContext(CancellationToken.None, new Dictionary<string, string>());

        // Act
        var rowSource = schema.GetRowSource("current", runtimeContext);

        // Assert
        Assert.NotNull(rowSource);
        Assert.IsType<WeatherRowSource>(rowSource);
    }

    [Fact]
    public void Schema_Should_Throw_For_Unknown_Table()
    {
        // Arrange
        var schema = new WeatherSchema();
        var runtimeContext = new RuntimeContext(CancellationToken.None, new Dictionary<string, string>());

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => schema.GetTableByName("unknown", runtimeContext));
        Assert.Throws<NotSupportedException>(() => schema.GetRowSource("unknown", runtimeContext));
    }

    [Fact]
    public void GetConstructors_Should_Return_Valid_Information()
    {
        // Arrange
        var schema = new WeatherSchema();

        // Act
        var constructors = schema.GetConstructors();

        // Assert
        Assert.NotEmpty(constructors);
        Assert.Contains(constructors, c => c.MethodName == "current");
    }
}
```

### Running Your Tests

Execute your tests to verify everything works:

```bash
dotnet test
```

Your output should show all tests passing:
```
Test run for Musoq.DataSources.Weather.Tests.dll(.NETCoreApp,Version=v8.0)
Microsoft (R) Test Execution Command Line Tool Version 17.8.0
Starting test execution, please wait...

Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15
```

### Integration Testing with Actual Musoq

For complete validation, test your plugin with actual Musoq:

```csharp
[Fact]
public void Plugin_Should_Work_With_Musoq_Engine()
{
    // This test requires the actual Musoq engine
    // Add Musoq.Engine package reference for this test
    
    var engine = new MusoqEngine();
    var environmentVariables = new Dictionary<string, string>
    {
        ["WEATHER_LOCATION"] = "London"
    };
    
    var query = "SELECT Location, Temperature FROM #weather.current()";
    var result = engine.Run(query, environmentVariables);
    
    Assert.NotEmpty(result);
    Assert.Equal("London", result.First()["Location"]);
}
```

---

## Advanced Patterns and Features

Now that you've mastered the basics, let's explore advanced patterns that will help you build sophisticated, production-ready plugins.

### Custom Functions and Libraries

Beyond basic data retrieval, you can add custom functions that users can call in their SQL queries.

#### Creating a Plugin Library

Add a library class to extend your plugin's capabilities:

```csharp
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Musoq.DataSources.Weather.Entities;

namespace Musoq.DataSources.Weather;

/// <summary>
/// Custom functions for weather data manipulation
/// </summary>
public class WeatherLibrary : LibraryBase
{
    /// <summary>
    /// Converts temperature from Celsius to Fahrenheit
    /// </summary>
    [BindableMethod]
    public double ToFahrenheit([InjectSpecificSource(typeof(WeatherEntity))] WeatherEntity weather)
    {
        return (weather.Temperature * 9.0 / 5.0) + 32.0;
    }

    /// <summary>
    /// Determines if weather is comfortable for outdoor activities
    /// </summary>
    [BindableMethod]
    public bool IsComfortable([InjectSpecificSource(typeof(WeatherEntity))] WeatherEntity weather)
    {
        return weather.Temperature >= 18.0 && 
               weather.Temperature <= 26.0 && 
               !weather.IsRaining && 
               weather.WindSpeed < 20.0;
    }

    /// <summary>
    /// Categorizes temperature into comfort levels
    /// </summary>
    [BindableMethod]
    public string TemperatureCategory([InjectSpecificSource(typeof(WeatherEntity))] WeatherEntity weather)
    {
        return weather.Temperature switch
        {
            < 0 => "Freezing",
            < 10 => "Cold",
            < 20 => "Cool",
            < 30 => "Warm",
            _ => "Hot"
        };
    }

    /// <summary>
    /// Calculates wind chill factor
    /// </summary>
    [BindableMethod]
    public double WindChill([InjectSpecificSource(typeof(WeatherEntity))] WeatherEntity weather)
    {
        if (weather.Temperature >= 10.0 || weather.WindSpeed < 4.8)
            return weather.Temperature;

        var temp = weather.Temperature;
        var windSpeed = weather.WindSpeed;
        
        return 13.12 + 0.6215 * temp - 11.37 * Math.Pow(windSpeed, 0.16) + 0.3965 * temp * Math.Pow(windSpeed, 0.16);
    }
}
```

#### Registering Your Library

Update your Schema to include the library:

```csharp
public class WeatherSchema : SchemaBase
{
    public WeatherSchema() : base("weather", CreateLibrary())
    {
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new WeatherLibrary();
        methodsManager.RegisterLibraries(library);
        return new MethodsAggregator(methodsManager);
    }
}
```

#### Using Custom Functions in SQL

Users can now write more sophisticated queries:

```sql
-- Convert temperature to Fahrenheit and categorize
SELECT 
    Location,
    Temperature,
    ToFahrenheit() as TempF,
    TemperatureCategory() as Category,
    IsComfortable() as IsComfortable,
    WindChill() as FeelsLike
FROM #weather.current('New York')

-- Find comfortable weather locations
SELECT Location, Temperature, Description
FROM #weather.current()
WHERE IsComfortable() = true

-- Group by temperature categories
SELECT 
    TemperatureCategory() as Category,
    COUNT(*) as LocationCount,
    AVG(Temperature) as AvgTemp
FROM #weather.current()
GROUP BY TemperatureCategory()
```

### Multi-Table Schemas

Real-world plugins often need to expose multiple related tables. Here's how to structure them:

#### Example: Enhanced Weather Plugin with Multiple Tables

```csharp
public class WeatherSchema : SchemaBase
{
    private const string CurrentWeatherTable = "current";
    private const string ForecastTable = "forecast";
    private const string HistoricalTable = "historical";
    private const string AlertsTable = "alerts";

    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            CurrentWeatherTable => new CurrentWeatherTable(),
            ForecastTable => new ForecastTable(),
            HistoricalTable => new HistoricalTable(),
            AlertsTable => new AlertsTable(),
            _ => throw new NotSupportedException($"Table '{name}' is not supported.")
        };
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            CurrentWeatherTable => new CurrentWeatherRowSource(runtimeContext, parameters),
            ForecastTable => new ForecastRowSource(runtimeContext, parameters),
            HistoricalTable => new HistoricalRowSource(runtimeContext, parameters),
            AlertsTable => new AlertsRowSource(runtimeContext, parameters),
            _ => throw new NotSupportedException($"Table '{name}' is not supported.")
        };
    }

    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<CurrentWeatherRowSource>(CurrentWeatherTable));
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<ForecastRowSource>(ForecastTable));
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<HistoricalRowSource>(HistoricalTable));
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<AlertsRowSource>(AlertsTable));
        return constructors.ToArray();
    }
}
```

This enables complex queries across multiple tables:

```sql
-- Join current weather with forecast
SELECT 
    c.Location,
    c.Temperature as CurrentTemp,
    f.Temperature as ForecastTemp,
    f.Date as ForecastDate
FROM #weather.current('London') c
JOIN #weather.forecast('London', 7) f ON c.Location = f.Location

-- Find locations with weather alerts
SELECT 
    a.Location,
    a.AlertType,
    a.Severity,
    c.Temperature
FROM #weather.alerts() a
JOIN #weather.current() c ON a.Location = c.Location
WHERE a.Severity = 'High'
```

### Handling Complex Data Types

Modern data sources often return complex, nested data structures. Here's how to handle them effectively:

#### Entity with Complex Properties

```csharp
public class WeatherEntity
{
    public string Location { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public List<string> Conditions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public WeatherDetails Details { get; set; } = new();
    public List<WeatherAlert> Alerts { get; set; } = new();
}

public class WeatherDetails
{
    public double Pressure { get; set; }
    public double Visibility { get; set; }
    public string UVIndex { get; set; } = string.Empty;
}

public class WeatherAlert
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
```

#### Helper for Complex Types

```csharp
internal static class WeatherTableHelper
{
    static WeatherTableHelper()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(WeatherEntity.Location), 0},
            {nameof(WeatherEntity.Temperature), 1},
            {nameof(WeatherEntity.Conditions), 2},
            {nameof(WeatherEntity.Metadata), 3},
            {nameof(WeatherEntity.Details), 4},
            {nameof(WeatherEntity.Alerts), 5}
        };
        
        IndexToMethodAccessMap = new Dictionary<int, Func<WeatherEntity, object?>>
        {
            {0, entity => entity.Location},
            {1, entity => entity.Temperature},
            {2, entity => entity.Conditions},           // List<string>
            {3, entity => entity.Metadata},             // Dictionary<string, object>
            {4, entity => entity.Details},              // Complex object
            {5, entity => entity.Alerts}                // List<WeatherAlert>
        };
        
        Columns = new[]
        {
            new SchemaColumn(nameof(WeatherEntity.Location), 0, typeof(string)),
            new SchemaColumn(nameof(WeatherEntity.Temperature), 1, typeof(double)),
            new SchemaColumn(nameof(WeatherEntity.Conditions), 2, typeof(List<string>)),
            new SchemaColumn(nameof(WeatherEntity.Metadata), 3, typeof(Dictionary<string, object>)),
            new SchemaColumn(nameof(WeatherEntity.Details), 4, typeof(WeatherDetails)),
            new SchemaColumn(nameof(WeatherEntity.Alerts), 5, typeof(List<WeatherAlert>))
        };
    }
}
```

#### XML Documentation for Complex Types

```xml
<columns>
<column name="Location" type="string">Weather station location</column>
<column name="Temperature" type="double">Temperature in Celsius</column>
<column name="Conditions" type="IList&lt;string&gt;">List of weather conditions</column>
<column name="Metadata" type="IDictionary&lt;string, object&gt;">Additional weather metadata</column>
<column name="Details" type="WeatherDetails">Detailed weather measurements</column>
<column name="Alerts" type="WeatherAlert[]">Active weather alerts</column>
</columns>
```

### Error Handling and Resilience Patterns

Production plugins need robust error handling:

#### RowSource with Error Handling

```csharp
internal class WeatherRowSource : RowSourceBase<WeatherEntity>
{
    private readonly string _apiKey;
    private readonly string _location;
    private readonly ILogger _logger;

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        try
        {
            var weatherData = GetWeatherDataWithRetry();
            
            if (!weatherData.Any())
            {
                _logger.LogWarning("No weather data returned for location: {Location}", _location);
                return;
            }

            var resolvers = weatherData.Select(entity => 
                new EntityResolver<WeatherEntity>(entity, WeatherTableHelper.NameToIndexMap, WeatherTableHelper.IndexToMethodAccessMap))
                .ToList();

            chunkedSource.Add(resolvers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve weather data for location: {Location}", _location);
            
            // Option 1: Re-throw to fail the query
            throw new InvalidOperationException($"Weather data unavailable for {_location}: {ex.Message}", ex);
            
            // Option 2: Return empty results
            // chunkedSource.Add(new List<IObjectResolver>());
            
            // Option 3: Return error data
            // var errorEntity = new WeatherEntity { Location = _location, Description = "Error: " + ex.Message };
            // var errorResolver = new EntityResolver<WeatherEntity>(errorEntity, ...);
            // chunkedSource.Add(new List<IObjectResolver> { errorResolver });
        }
    }

    private List<WeatherEntity> GetWeatherDataWithRetry()
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(1);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return GetWeatherData();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("Attempt {Attempt} failed, retrying in {Delay}ms: {Error}", 
                    attempt, delay.TotalMilliseconds, ex.Message);
                
                Thread.Sleep(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        throw new InvalidOperationException($"Failed to retrieve weather data after {maxRetries} attempts");
    }

    private List<WeatherEntity> GetWeatherData()
    {
        // Your actual data retrieval logic here
        // This could throw HttpRequestException, JsonException, etc.
        return new List<WeatherEntity>();
    }
}
```

### Performance Optimization Patterns

#### Chunked Data Processing

For large datasets, implement efficient chunking:

```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    const int chunkSize = 1000;
    int totalProcessed = 0;
    
    using var dataStream = GetDataStream(); // IEnumerable<WeatherEntity>
    
    var chunk = new List<WeatherEntity>(chunkSize);
    
    foreach (var entity in dataStream)
    {
        chunk.Add(entity);
        
        if (chunk.Count >= chunkSize)
        {
            ProcessAndAddChunk(chunk, chunkedSource);
            chunk.Clear();
            totalProcessed += chunkSize;
            
            // Optional: Log progress for long-running operations
            if (totalProcessed % 10000 == 0)
            {
                _logger.LogInformation("Processed {Count} weather records", totalProcessed);
            }
        }
    }
    
    // Process remaining items
    if (chunk.Any())
    {
        ProcessAndAddChunk(chunk, chunkedSource);
    }
}

private void ProcessAndAddChunk(List<WeatherEntity> entities, BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    var resolvers = entities.Select(entity => 
        new EntityResolver<WeatherEntity>(entity, WeatherTableHelper.NameToIndexMap, WeatherTableHelper.IndexToMethodAccessMap))
        .ToList();
    
    chunkedSource.Add(resolvers);
}
```

#### Caching Strategies

For APIs with rate limits or slow responses:

```csharp
internal class WeatherRowSource : RowSourceBase<WeatherEntity>
{
    private static readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 1000
    });

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var cacheKey = $"weather_{_location}_{DateTime.Now:yyyyMMddHH}"; // Cache per hour
        
        if (_cache.TryGetValue(cacheKey, out List<WeatherEntity>? cachedData))
        {
            ProcessCachedData(cachedData!, chunkedSource);
            return;
        }

        var freshData = GetWeatherData();
        
        // Cache for 1 hour
        _cache.Set(cacheKey, freshData, TimeSpan.FromHours(1));
        
        ProcessCachedData(freshData, chunkedSource);
    }
}
```

---

## Best Practices and Common Patterns

Through building many plugins, the Musoq community has developed best practices that ensure reliable, maintainable, and performant plugins. Let me teach you these essential patterns.

### The Golden Rules of Plugin Development

#### 1. Always Provide Comprehensive XML Metadata
**This is the most critical rule.** Your XML metadata is not optional documentation—it's essential functionality:

```csharp
/// <description>
/// [Always write a clear, concise description of what your plugin does]
/// </description>
/// <virtual-constructors>
/// <virtual-constructor>
/// <virtual-param>[Document every parameter clearly]</virtual-param>
/// <examples>
/// <example>
/// <from>
/// <environmentVariables>
/// <environmentVariable name="API_KEY" isRequired="true">[Explain what this is for]</environmentVariable>
/// </environmentVariables>
/// #yourschema.method(string param)
/// </from>
/// <description>[Explain exactly what this method does]</description>
/// <columns>
/// <column name="ColumnName" type="type">[Describe what this column contains]</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
/// </virtual-constructors>
```

**Why this matters:**
- Enables IntelliSense and auto-completion
- Provides help documentation
- Validates parameter usage
- Essential for schema discovery

#### 2. Handle Errors Gracefully
Production plugins must handle failures elegantly:

```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    try
    {
        var data = GetDataFromSource();
        // Process data...
    }
    catch (HttpRequestException ex)
    {
        throw new InvalidOperationException($"Network error while fetching data: {ex.Message}", ex);
    }
    catch (JsonException ex)
    {
        throw new InvalidOperationException($"Invalid data format received: {ex.Message}", ex);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Unexpected error in {GetType().Name}: {ex.Message}", ex);
    }
}
```

#### 3. Use Environment Variables Properly
Follow consistent patterns for configuration:

```csharp
public MyRowSource(RuntimeContext runtimeContext, string? customParam = null)
{
    // Required variables - fail fast if missing
    _apiKey = runtimeContext.EnvironmentVariables["MY_API_KEY"];
    
    // Optional variables with sensible defaults
    _timeout = int.Parse(runtimeContext.EnvironmentVariables.GetValueOrDefault("MY_TIMEOUT", "30"));
    _baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("MY_BASE_URL", "https://api.default.com");
    
    // Parameter overrides environment variable
    _endpoint = customParam ?? runtimeContext.EnvironmentVariables.GetValueOrDefault("MY_ENDPOINT", "default");
}
```

#### 4. Design for Performance
Think about performance from the beginning:

```csharp
// Good: Process data in chunks
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    const int chunkSize = 1000;
    var buffer = new List<MyEntity>(chunkSize);
    
    foreach (var item in GetDataStream())
    {
        buffer.Add(item);
        
        if (buffer.Count >= chunkSize)
        {
            ProcessChunk(buffer, chunkedSource);
            buffer.Clear();
        }
    }
    
    if (buffer.Any())
        ProcessChunk(buffer, chunkedSource);
}

// Bad: Load everything into memory at once
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    var allData = GetAllDataAtOnce(); // Could cause OutOfMemoryException
    var resolvers = allData.Select(entity => new EntityResolver<MyEntity>(...)).ToList();
    chunkedSource.Add(resolvers);
}
```

#### 5. Follow Consistent Naming Conventions

**File Organization:**
```
Musoq.DataSources.YourPlugin/
├── YourPluginSchema.cs           # Main schema
├── Entities/
│   └── YourEntity.cs            # Data models
├── Tables/
│   ├── YourTable.cs             # Table definitions
│   └── YourTableHelper.cs       # Column mappings
├── Sources/
│   └── YourRowSource.cs         # Data sources
└── YourPluginLibrary.cs         # Custom functions
```

**Naming Pattern:**
- Schema: `{PluginName}Schema`
- Entity: `{DataType}Entity`
- Table: `{DataType}Table`
- Helper: `{DataType}TableHelper`
- RowSource: `{DataType}RowSource`
- Library: `{PluginName}Library`

### Architecture Decision Patterns

#### When to Use Multiple Tables vs Single Table

**Use Multiple Tables When:**
- Different data types with distinct schemas
- Related but separate concerns (users vs orders vs products)
- Different data sources within the same domain

**Use Single Table When:**
- One primary data type
- Simple, focused plugin
- All data comes from the same API/source

#### Static vs Dynamic Columns

**Choose Static Columns When:**
```xml
<columns>
<column name="Id" type="string">Known column with fixed schema</column>
<column name="Name" type="string">Another known column</column>
</columns>
```
- You know the exact schema at compile time
- Data structure is stable
- You want strong typing and IntelliSense

**Choose Dynamic Columns When:**
```xml
<columns isDynamic="true"></columns>
```
- Schema varies based on runtime data
- API responses have different structures
- Database tables with unknown schemas
- File formats with varying columns

#### Simple vs Complex Entities

**Simple Entity Pattern:**
```csharp
public class SimpleEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; }
}
```

**Complex Entity Pattern:**
```csharp
public class ComplexEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public NestedObject Details { get; set; } = new();
}
```

Use complex entities when your data source naturally returns nested or structured data.

### Data Source Integration Patterns

#### HTTP API Integration Pattern
```csharp
internal class ApiRowSource : RowSourceBase<ApiEntity>
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public ApiRowSource(RuntimeContext runtimeContext, string? endpoint = null)
    {
        _httpClient = new HttpClient();
        _apiKey = runtimeContext.EnvironmentVariables["API_KEY"];
        _baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("API_BASE_URL", "https://api.default.com");
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        
        var response = _httpClient.GetStringAsync($"{_baseUrl}/data").GetAwaiter().GetResult();
        var apiData = JsonSerializer.Deserialize<List<ApiEntity>>(response) ?? new List<ApiEntity>();

        var resolvers = apiData.Select(entity => 
            new EntityResolver<ApiEntity>(entity, ApiTableHelper.NameToIndexMap, ApiTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

#### File Processing Pattern
```csharp
internal class FileRowSource : RowSourceBase<FileEntity>
{
    private readonly string _filePath;

    public FileRowSource(RuntimeContext runtimeContext, string filePath)
    {
        _filePath = filePath;
        
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"File not found: {_filePath}");
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        const int chunkSize = 1000;
        var buffer = new List<FileEntity>(chunkSize);

        foreach (var line in File.ReadLines(_filePath))
        {
            var entity = ParseLine(line);
            buffer.Add(entity);

            if (buffer.Count >= chunkSize)
            {
                ProcessChunk(buffer, chunkedSource);
                buffer.Clear();
            }
        }

        if (buffer.Any())
            ProcessChunk(buffer, chunkedSource);
    }

    private FileEntity ParseLine(string line)
    {
        // Your parsing logic here
        return new FileEntity { Content = line };
    }
}
```

#### Database Connection Pattern
```csharp
internal class DatabaseRowSource : RowSourceBase<DatabaseEntity>
{
    private readonly string _connectionString;

    public DatabaseRowSource(RuntimeContext runtimeContext, string? query = null)
    {
        _connectionString = runtimeContext.EnvironmentVariables["CONNECTION_STRING"];
        _query = query ?? "SELECT * FROM DefaultTable";
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(_query, connection);
        using var reader = command.ExecuteReader();

        var entities = new List<DatabaseEntity>();
        while (reader.Read())
        {
            entities.Add(new DatabaseEntity
            {
                Id = reader["Id"].ToString()!,
                Name = reader["Name"].ToString()!,
                // Map other columns...
            });
        }

        var resolvers = entities.Select(entity => 
            new EntityResolver<DatabaseEntity>(entity, DatabaseTableHelper.NameToIndexMap, DatabaseTableHelper.IndexToMethodAccessMap))
            .ToList();

        chunkedSource.Add(resolvers);
    }
}
```

### Testing Best Practices

#### Comprehensive Test Structure
```csharp
// 1. Entity Tests - Verify data model
public class WeatherEntityTests { /* ... */ }

// 2. Helper Tests - Verify column mappings
public class WeatherTableHelperTests { /* ... */ }

// 3. Table Tests - Verify schema metadata
public class WeatherTableTests { /* ... */ }

// 4. RowSource Tests - Verify data retrieval
public class WeatherRowSourceTests { /* ... */ }

// 5. Schema Integration Tests - Verify everything works together
public class WeatherSchemaTests { /* ... */ }

// 6. Library Tests - Verify custom functions
public class WeatherLibraryTests { /* ... */ }
```

#### Mock External Dependencies
```csharp
[Fact]
public void RowSource_Should_Handle_API_Errors_Gracefully()
{
    // Arrange
    var mockHttpClient = new Mock<HttpClient>();
    mockHttpClient.Setup(x => x.GetStringAsync(It.IsAny<string>()))
              .ThrowsAsync(new HttpRequestException("Network error"));

    var runtimeContext = new RuntimeContext(CancellationToken.None, 
        new Dictionary<string, string> { ["API_KEY"] = "test-key" });

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => 
    {
        var rowSource = new WeatherRowSource(runtimeContext);
        var rows = rowSource.Rows.ToList();
    });

    Assert.Contains("Network error", exception.Message);
}
```

### Common Pitfalls to Avoid

#### ❌ Don't Forget XML Metadata
```csharp
// Bad: No XML metadata
public class BadSchema : SchemaBase
{
    public BadSchema() : base("bad", CreateLibrary()) { }
}

// Good: Comprehensive XML metadata
/// <description>Detailed description</description>
/// <virtual-constructors>...</virtual-constructors>
public class GoodSchema : SchemaBase
{
    public GoodSchema() : base("good", CreateLibrary()) { }
}
```

#### ❌ Don't Ignore Performance
```csharp
// Bad: Loading everything into memory
var allData = GetMillionsOfRecords().ToList();

// Good: Processing in chunks
foreach (var chunk in GetMillionsOfRecords().Chunk(1000))
{
    ProcessChunk(chunk);
}
```

#### ❌ Don't Ignore Errors
```csharp
// Bad: Swallowing exceptions
try 
{
    var data = GetData();
} 
catch 
{ 
    // Silent failure
}

// Good: Proper error handling
try 
{
    var data = GetData();
} 
catch (Exception ex)
{
    throw new InvalidOperationException($"Failed to get data: {ex.Message}", ex);
}
```

#### ❌ Don't Hardcode Configuration
```csharp
// Bad: Hardcoded values
private const string ApiUrl = "https://hardcoded.api.com";

// Good: Environment variable configuration
var apiUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("API_URL", "https://default.api.com");
```

---

## Learning from Real-World Examples

The best way to master plugin development is by studying the excellent examples already in this repository. Each plugin demonstrates different patterns and complexity levels. Let me guide you through the most instructive examples.

### Study Path: From Simple to Complex

#### Level 1: System Plugin - Understanding the Basics
**Location**: `Musoq.DataSources.System/`

Start here to understand fundamental concepts:

**What to study:**
- `SystemSchema.cs` - Simple schema with two tables (`dual`, `range`)
- `DualEntity.cs` - Minimal entity with just one property
- `RangeItemEntity.cs` - Simple numeric data
- `EmptyLibrary.cs` - Minimal library implementation

**Key lessons:**
```csharp
// Simple entity design
public class DualEntity
{
    public string Value { get; set; } = string.Empty;
}

// Basic schema with multiple tables
public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
{
    return name.ToLowerInvariant() switch
    {
        "dual" => new DualRowSource(runtimeContext),
        "range" => new RangeSource(runtimeContext, parameters),
        _ => throw new NotSupportedException($"Table '{name}' is not supported.")
    };
}

// Method overloads handling
public override SchemaMethodInfo[] GetConstructors()
{
    var constructors = new List<SchemaMethodInfo>();
    constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<DualRowSource>("dual"));
    constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<RangeSource>("range"));
    return constructors.ToArray();
}
```

**XML metadata patterns:**
- Simple virtual constructors
- Parameter documentation with `<virtual-param>`
- Multiple examples for method overloads

#### Level 2: OpenAI Plugin - API Integration Patterns
**Location**: `Musoq.DataSources.OpenAI/`

Study this for HTTP API integration:

**What to study:**
- `OpenAiSchema.cs` - Complex XML metadata with dynamic columns
- `OpenAiRowSource.cs` - HTTP client usage and error handling
- `OpenAiLibrary.cs` - Custom functions with entity injection
- `OpenAiEntity.cs` - Complex entity with multiple data types

**Key lessons:**
```csharp
// Dynamic columns for varying API responses
/// <columns isDynamic="true"></columns>

// Environment variable patterns
var apiKey = runtimeContext.EnvironmentVariables["OPENAI_API_KEY"];
var baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("OPENAI_BASE_URL", "https://api.openai.com");

// HTTP client with authentication
_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

// Complex virtual parameter documentation
/// <virtual-param>Models to use: gpt-4, gpt-4-32k, gpt-4-vision-preview, gpt-4-turbo-preview, gpt-3.5-turbo, gpt-3.5-turbo-1106, gpt-3.5-turbo-16k, gpt-3.5-turbo-instruct, babbage-002, davinci-002</virtual-param>
```

**Advanced patterns:**
- Dynamic column generation based on API responses
- Comprehensive error handling for network operations
- Custom functions that work with AI responses

#### Level 3: Docker Plugin - Multi-Table Complex Schema
**Location**: `Musoq.DataSources.Docker/`

Study this for complex, multi-table plugins:

**What to study:**
- `DockerSchema.cs` - Multiple related tables in one schema
- Multiple entity types (Container, Image, Network, Volume)
- Complex data types and nested objects
- Rich helper functions and metadata

**Key lessons:**
```csharp
// Multiple related tables
public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
{
    return name.ToLowerInvariant() switch
    {
        "containers" => new ContainersRowSource(runtimeContext, parameters),
        "images" => new ImagesRowSource(runtimeContext, parameters),
        "networks" => new NetworksRowSource(runtimeContext, parameters),
        "volumes" => new VolumesRowSource(runtimeContext, parameters),
        _ => throw new NotSupportedException($"Table '{name}' is not supported.")
    };
}

// Complex data types in entities
public class ContainerEntity
{
    public string Id { get; set; } = string.Empty;
    public IDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    public IList<string> Ports { get; set; } = new List<string>();
    public IList<MountPoint> Mounts { get; set; } = new List<MountPoint>();
    public SummaryNetworkSettings NetworkSettings { get; set; } = new();
}

// Complex XML metadata for nested types
/// <column name="Labels" type="IDictionary&lt;string, string&gt;">Assigned labels to specific container</column>
/// <column name="Ports" type="IList&lt;string&gt;">Mapped ports</column>
/// <column name="Mounts" type="IList&lt;MountPoint&gt;">Mounted points</column>
```

**Advanced patterns:**
- External service integration (Docker API)
- Complex object hierarchies
- Multiple table coordination

#### Level 4: CANBus Plugin - Advanced Metadata and Additional Tables
**Location**: `Musoq.DataSources.CANBus/`

Study this for the most advanced XML metadata patterns:

**What to study:**
- `CANBusSchema.cs` - Advanced XML with additional tables
- Complex entity relationships
- Advanced virtual constructor patterns

**Key lessons:**
```csharp
// Additional tables documentation
/// <additional-tables>
/// <additional-table>
/// <description>Represent possible values of a signal</description>
/// <columns type="ValueMapEntity[]">
/// <column name="Value" type="int">Value of signal</column>
/// <column name="Name" type="string">Name of the value</column>
/// </columns>
/// </additional-table>
/// </additional-tables>

// Complex entity arrays
/// <column name="Signals" type="SignalEntity[]">Signals of the message</column>
/// <column name="Receiver" type="string[]">Receiver for the signal entity</column>

// File processing patterns
public class MessageEntity
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SignalEntity[] Signals { get; set; } = Array.Empty<SignalEntity>();
}
```

**Advanced patterns:**
- File format parsing and processing
- Complex metadata documentation
- Entity arrays and relationships

#### Level 5: Postgres Plugin - Database Connectivity
**Location**: `Musoq.DataSources.Postgres/`

Study this for database integration patterns:

**What to study:**
- Database connection handling
- Dynamic schema discovery
- SQL query generation and execution
- Connection string management

**Key lessons:**
```csharp
// Database connection patterns
var connectionString = runtimeContext.EnvironmentVariables["NPGSQL_CONNECTION_STRING"];

// Dynamic columns for unknown database schemas
/// <columns isDynamic="true"></columns>

// Database-specific error handling
try
{
    using var connection = new NpgsqlConnection(_connectionString);
    connection.Open();
    // Database operations...
}
catch (NpgsqlException ex)
{
    throw new InvalidOperationException($"Database error: {ex.Message}", ex);
}
```

### Pattern Recognition Exercises

As you study these plugins, look for these recurring patterns:

#### 1. **Environment Variable Usage Patterns**
```csharp
// Required (will throw if missing)
var apiKey = runtimeContext.EnvironmentVariables["API_KEY"];

// Optional with default
var timeout = int.Parse(runtimeContext.EnvironmentVariables.GetValueOrDefault("TIMEOUT", "30"));

// Optional, used only if present
if (runtimeContext.EnvironmentVariables.TryGetValue("OPTIONAL_CONFIG", out var config))
{
    // Use optional configuration
}
```

#### 2. **Error Handling Patterns**
```csharp
// Network/API errors
catch (HttpRequestException ex)
{
    throw new InvalidOperationException($"Network error: {ex.Message}", ex);
}

// Data format errors
catch (JsonException ex)
{
    throw new InvalidOperationException($"Invalid response format: {ex.Message}", ex);
}

// General errors
catch (Exception ex)
{
    throw new InvalidOperationException($"Unexpected error: {ex.Message}", ex);
}
```

#### 3. **Data Processing Patterns**
```csharp
// Simple collection
var data = GetAllData();
var resolvers = data.Select(entity => new EntityResolver<T>(...)).ToList();
chunkedSource.Add(resolvers);

// Chunked processing
const int chunkSize = 1000;
foreach (var chunk in data.Chunk(chunkSize))
{
    var resolvers = chunk.Select(entity => new EntityResolver<T>(...)).ToList();
    chunkedSource.Add(resolvers);
}

// Streaming processing
foreach (var item in GetDataStream())
{
    yield return new EntityResolver<T>(item, ...);
}
```

#### 4. **XML Metadata Patterns**
```csharp
// Simple static columns
/// <columns>
/// <column name="Id" type="string">Unique identifier</column>
/// <column name="Name" type="string">Display name</column>
/// </columns>

// Dynamic columns
/// <columns isDynamic="true"></columns>

// Complex types
/// <column name="Metadata" type="IDictionary&lt;string, object&gt;">Key-value metadata</column>
/// <column name="Items" type="ItemEntity[]">Array of related items</column>
```

### Creating Your Own Patterns

After studying existing plugins, you'll start to see patterns that apply to your specific use cases:

#### Custom API Integration Pattern
```csharp
internal class MyApiRowSource : RowSourceBase<MyEntity>
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public MyApiRowSource(RuntimeContext runtimeContext, params object[] parameters)
    {
        _httpClient = new HttpClient();
        _apiKey = runtimeContext.EnvironmentVariables["MY_API_KEY"];
        _baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("MY_BASE_URL", "https://api.myservice.com");
        
        // Configure based on parameters...
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        // Your API-specific logic here...
    }
}
```

#### Custom File Processing Pattern
```csharp
internal class MyFileRowSource : RowSourceBase<MyEntity>
{
    private readonly string _filePath;
    private readonly string _format;

    public MyFileRowSource(RuntimeContext runtimeContext, string filePath, string? format = null)
    {
        _filePath = filePath;
        _format = format ?? runtimeContext.EnvironmentVariables.GetValueOrDefault("FILE_FORMAT", "json");
        
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"File not found: {_filePath}");
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        // Your file-specific processing here...
    }
}
```

---

## Common Use Cases

Understanding common use cases helps you choose the right patterns and approaches for your plugin. Here are the most frequent scenarios and how to handle them:

### 🌐 Web API Integration

**When to use**: Querying REST APIs, GraphQL endpoints, or web services using SQL syntax.

**Key considerations:**
- Authentication (API keys, OAuth, JWT)
- Rate limiting and throttling
- Error handling for network issues
- Data pagination

**Example scenarios:**
```sql
-- Query GitHub repositories
SELECT name, stars, language FROM #github.repos() WHERE stars > 1000

-- Get weather data for multiple cities
SELECT city, temperature, humidity FROM #weather.current() WHERE city IN ('London', 'Paris', 'Tokyo')

-- Fetch social media posts
SELECT author, content, likes FROM #twitter.search('musoq') WHERE likes > 100
```

**Pattern to follow**: Study the OpenAI plugin for HTTP client patterns and authentication.

### 🗄️ Custom Database Connectors

**When to use**: Connecting to proprietary databases or data stores not supported by standard providers.

**Key considerations:**
- Connection string management
- SQL query generation
- Dynamic schema discovery
- Connection pooling and disposal

**Example scenarios:**
```sql
-- Query custom database tables
SELECT * FROM #mydb.users() WHERE created > '2024-01-01'

-- Cross-database joins
SELECT u.name, o.total FROM #db1.users() u JOIN #db2.orders() o ON u.id = o.user_id

-- Dynamic table access
SELECT * FROM #warehouse.table('sales_2024') WHERE region = 'Europe'
```

**Pattern to follow**: Study the Postgres plugin for database connection patterns.

### 📁 File System Operations

**When to use**: Querying files, directories, logs, or any file-based data sources.

**Key considerations:**
- File format parsing (CSV, JSON, XML, binary)
- Large file handling and streaming
- File watching and real-time updates
- Path resolution and security

**Example scenarios:**
```sql
-- Query log files
SELECT timestamp, level, message FROM #logs.file('/var/log/app.log') WHERE level = 'ERROR'

-- Directory listing
SELECT name, size, modified FROM #files.directory('/home/user/docs') WHERE extension = '.pdf'

-- CSV data analysis
SELECT region, SUM(sales) FROM #csv.file('sales.csv') GROUP BY region
```

**Pattern to follow**: Study the JSON or Archives plugin for file processing patterns.

### ☁️ Cloud Service Integration

**When to use**: Querying cloud services like AWS, Azure, GCP resources, or SaaS platforms.

**Key considerations:**
- Cloud authentication (IAM roles, service accounts)
- Service-specific APIs and SDKs
- Regional and zone handling
- Cost optimization for API calls

**Example scenarios:**
```sql
-- AWS EC2 instances
SELECT instance_id, instance_type, state FROM #aws.ec2_instances() WHERE state = 'running'

-- Azure storage blobs
SELECT name, size, last_modified FROM #azure.blobs('mycontainer') WHERE size > 1000000

-- Google Cloud resources
SELECT project_id, resource_type, location FROM #gcp.resources() WHERE location LIKE 'us-%'
```

**Pattern to follow**: Study the Docker or Kubernetes plugins for external service integration.

### 🔧 System Monitoring

**When to use**: Querying system metrics, performance counters, or monitoring data.

**Key considerations:**
- Real-time vs historical data
- Performance impact of monitoring
- Cross-platform compatibility
- Metric aggregation and filtering

**Example scenarios:**
```sql
-- System processes
SELECT name, cpu_percent, memory_mb FROM #system.processes() WHERE cpu_percent > 10

-- Performance counters
SELECT counter_name, value, timestamp FROM #perf.counters() WHERE category = 'Processor'

-- Network connections
SELECT local_port, remote_address, state FROM #network.connections() WHERE state = 'ESTABLISHED'
```

**Pattern to follow**: Study the Os or System plugins for system integration patterns.

### 📊 Data Processing Pipelines

**When to use**: Transforming and querying data from ETL processes or data pipelines.

**Key considerations:**
- Data streaming and real-time processing
- Schema evolution and compatibility
- Error handling and data quality
- Performance optimization for large datasets

**Example scenarios:**
```sql
-- Stream processing
SELECT event_type, COUNT(*) FROM #stream.events() WHERE timestamp > NOW() - INTERVAL '1 HOUR' GROUP BY event_type

-- Data validation
SELECT table_name, row_count, error_count FROM #etl.validation() WHERE error_count > 0

-- Pipeline monitoring
SELECT pipeline_id, status, duration FROM #pipeline.runs() WHERE status = 'FAILED'
```

**Pattern to follow**: Create custom patterns based on your specific data pipeline technology.

### 🤖 AI and Machine Learning Integration

**When to use**: Querying AI models, machine learning services, or data processing APIs.

**Key considerations:**
- Model versioning and deployment
- Input/output data transformation
- Performance and latency optimization
- Cost management for AI API calls

**Example scenarios:**
```sql
-- Text analysis
SELECT text, sentiment, confidence FROM #ai.sentiment_analysis('Analyze this text')

-- Image recognition
SELECT image_path, detected_objects, confidence FROM #vision.analyze('/path/to/images/')

-- Language translation
SELECT original_text, translated_text, source_lang, target_lang FROM #translate.text('Hello', 'es')
```

**Pattern to follow**: Study the OpenAI or Ollama plugins for AI service integration.

### Choosing the Right Approach

**For simple data sources**: Use single-table patterns with basic entities
**For complex APIs**: Use multi-table patterns with rich XML metadata
**For real-time data**: Implement streaming patterns with chunked processing
**For large datasets**: Use pagination and caching strategies
**For external services**: Implement robust error handling and retry logic

---

## Support and Community

### Getting Help

**GitHub Issues**: Report bugs or request features at [https://github.com/Puchaczov/Musoq.DataSources/issues](https://github.com/Puchaczov/Musoq.DataSources/issues)

**Discussions**: Join community discussions for questions and ideas at [https://github.com/Puchaczov/Musoq.DataSources/discussions](https://github.com/Puchaczov/Musoq.DataSources/discussions)

**Examples**: Browse existing plugins in this repository for patterns and inspiration

### Contributing

**Documentation**: Help improve these guides by submitting pull requests

**Plugin Examples**: Share your plugins as examples for others to learn from

**Testing**: Report issues or bugs you encounter while developing plugins

### Best Resources for Learning

1. **This Tutorial**: Complete guide from basics to advanced patterns
2. **Existing Plugins**: Real-world examples in this repository
3. **Musoq Engine**: Understanding the main engine at [https://github.com/Puchaczov/Musoq](https://github.com/Puchaczov/Musoq)
4. **Community Examples**: Plugins shared by other developers

---

## Summary

Congratulations! You've completed the comprehensive Musoq plugin development tutorial. You now understand:

✅ **Core Concepts**: The five essential components and how they work together  
✅ **Hands-On Development**: Building a complete plugin from scratch  
✅ **Essential XML Metadata**: Critical annotations that make your plugin discoverable  
✅ **Build Configuration**: Proper project setup for documentation generation  
✅ **Testing Strategies**: Comprehensive testing approaches for all components  
✅ **Advanced Patterns**: Complex scenarios like multi-table schemas and custom functions  
✅ **Best Practices**: Proven patterns from the community  
✅ **Real-World Examples**: Learning from existing successful plugins  

### Your Next Steps

1. **Build Your First Plugin**: Start with a simple data source you're familiar with
2. **Study Existing Plugins**: Pick one similar to your use case and study its patterns
3. **Test Thoroughly**: Write comprehensive tests for all components
4. **Share Your Work**: Contribute back to the community with examples and improvements

### Key Takeaways

- **XML metadata is not optional** - it's essential for plugin functionality
- **Start simple, then add complexity** - begin with basic patterns and evolve
- **Study existing plugins** - they contain proven, production-ready patterns
- **Test everything** - comprehensive testing prevents runtime issues
- **Follow conventions** - consistent patterns make maintenance easier

You're now equipped to create powerful, production-ready Musoq plugins that can query any data source using SQL syntax. Welcome to the Musoq plugin development community!

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.