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

## XML Metadata Annotations (Critical)

⚠️ **This is one of the most important aspects of plugin development.** The XML metadata annotations are placed directly in the constructor's XML comments and provide essential information about how to use your plugin.

### Schema Class Annotations

Every schema class must include these annotations above the constructor:

```csharp
/// <description>
/// Provides schema to work with [your data source description].
/// </description>
/// <short-description>
/// [Brief description of your plugin]
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class YourSchema : SchemaBase
{
    /// <virtual-constructors>
    /// <!-- Your virtual constructor definitions go here -->
    /// </virtual-constructors>
    /// <additional-tables>
    /// <!-- Your additional table definitions go here -->
    /// </additional-tables>
    public YourSchema() : base("yourschema", CreateLibrary())
    {
    }
}
```

### Virtual Constructor Annotations

Virtual constructors define how users call your plugin from SQL. Each table/method in your plugin needs a virtual constructor:

```csharp
/// <virtual-constructors>
/// <virtual-constructor>
/// <examples>
/// <example>
/// <from>
/// <environmentVariables>
/// <environmentVariable name="API_KEY" isRequired="true">Your API key</environmentVariable>
/// <environmentVariable name="BASE_URL" isRequired="false">Base URL override</environmentVariable>
/// </environmentVariables>
/// #yourschema.datasource(string connectionString, int maxResults)
/// </from>
/// <description>Retrieves data from your data source with the given parameters</description>
/// <columns>
/// <column name="Id" type="int">Unique identifier</column>
/// <column name="Name" type="string">Name of the entity</column>
/// <column name="CreatedDate" type="DateTime">Creation timestamp</column>
/// <column name="Tags" type="string[]">Array of tags</column>
/// <column name="Metadata" type="IDictionary&lt;string, object&gt;">Additional metadata</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
/// </virtual-constructors>
```

### Annotation Reference

#### Environment Variables
```xml
<environmentVariables>
<environmentVariable name="VARIABLE_NAME" isRequired="true|false">Description of the variable</environmentVariable>
</environmentVariables>
```

#### Virtual Parameter Documentation
Every parameter in your method signature should be documented with `<virtual-param>` tags:
```xml
<virtual-constructor>
<virtual-param>Model to use: gpt-4, gpt-3.5-turbo, etc.</virtual-param>
<virtual-param>Max tokens to generate</virtual-param>
<virtual-param>Temperature (0.0-2.0)</virtual-param>
<examples>
<example>
<from>#yourschema.method(string model, int maxTokens, decimal temperature)</from>
<description>Method with multiple documented parameters</description>
<columns>
<!-- column definitions -->
</columns>
</example>
</examples>
</virtual-constructor>
```

**Real example from OpenAI plugin:**
```xml
<virtual-constructor>
<virtual-param>Models to use: gpt-4, gpt-4-32k, gpt-4-vision-preview, gpt-4-turbo-preview, gpt-3.5-turbo, gpt-3.5-turbo-1106, gpt-3.5-turbo-16k, gpt-3.5-turbo-instruct, babbage-002, davinci-002</virtual-param>
<virtual-param>Max tokens to generate</virtual-param>
<virtual-param>Temperature</virtual-param>
<virtual-param>Frequency penalty</virtual-param>
<virtual-param>Presence penalty</virtual-param>
<examples>
<example>
<from>#openai.gpt(string model, int maxTokens, decimal temperature, decimal frequencyPenalty, decimal presencePenalty)</from>
<description>Gives the access to OpenAI api</description>
<columns isDynamic="true"></columns>
</example>
</examples>
</virtual-constructor>
```

#### Column Definitions

**Static Columns (Known at compile time):**
```xml
<columns>
<column name="ColumnName" type="datatype">Description of what this column contains</column>
<column name="Id" type="int">Unique identifier</column>
<column name="Name" type="string">Entity name</column>
<column name="Tags" type="string[]">Array of tags</column>
<column name="Metadata" type="IDictionary&lt;string, object&gt;">Key-value metadata</column>
<column name="Networks" type="IList&lt;string&gt;">List of network names</column>
</columns>
```

**Dynamic Columns (Determined at runtime):**
```xml
<columns isDynamic="true"></columns>
```

Use `isDynamic="true"` when:
- Column structure depends on runtime data
- API responses have varying schemas
- Database tables have unknown column structures
- Plugin discovers columns from external metadata

**Examples from existing plugins:**
- **OpenAI/Ollama**: `<columns isDynamic="true"></columns>` - AI responses vary
- **Postgres**: `<columns isDynamic="true"></columns>` - Database tables have varying schemas
- **CANBus**: Static columns for messages, but dynamic columns for separated values

### Additional Tables

When your plugin exposes complex entities with nested arrays or properties, document them:

```csharp
/// <additional-tables>
/// <additional-table>
/// <description>Represents a user profile within the system</description>
/// <columns type="UserEntity">
/// <column name="UserId" type="int">User's unique identifier</column>
/// <column name="Email" type="string">User's email address</column>
/// <column name="Permissions" type="PermissionEntity[]">Array of user permissions</column>
/// </columns>
/// </additional-table>
/// <additional-table>
/// <description>Represents a permission assigned to a user</description>
/// <columns type="PermissionEntity[]">
/// <column name="Name" type="string">Permission name</column>
/// <column name="Level" type="int">Permission level (1-10)</column>
/// </columns>
/// </additional-table>
/// </additional-tables>
```

### Complex Data Types Documentation

Document complex .NET types exactly as they appear in your entities:

#### Collections and Generics
```xml
<column name="Tags" type="string[]">Array of string tags</column>
<column name="Networks" type="IList&lt;string&gt;">List of network names</column>
<column name="Metadata" type="IDictionary&lt;string, string&gt;">Key-value metadata pairs</column>
<column name="Settings" type="IDictionary&lt;string, object&gt;">Dynamic settings dictionary</column>
```

#### Custom Entity Arrays
```xml
<column name="Signals" type="SignalEntity[]">Array of signal entities</column>
<column name="Permissions" type="PermissionEntity[]">User permissions array</column>
```

#### Complex Object Properties
```xml
<column name="NetworkSettings" type="SummaryNetworkSettings">Network configuration object</column>
<column name="UsageData" type="VolumeUsageData">Storage usage information</column>
```

**Real examples from existing plugins:**

**Docker plugin complex types:**
```xml
<column name="Labels" type="IDictionary&lt;string, string&gt;">Assigned labels to specific container</column>
<column name="Ports" type="IList&lt;string&gt;">Mapped ports</column>
<column name="Mounts" type="IList&lt;MountPoint&gt;">Mounted points</column>
<column name="NetworkSettings" type="SummaryNetworkSettings">Network settings</column>
```

**CANBus plugin entity arrays:**
```xml
<column name="Signals" type="SignalEntity[]">Signals of the message</column>
<column name="Receiver" type="string[]">Receiver for the signal entity</column>
```

### Multiple Examples and Overloads

When your plugin supports method overloads, document each one separately with multiple examples:

```csharp
/// <virtual-constructors>
/// <virtual-constructor>
/// <examples>
/// <example>
/// <from>#system.range(long max)</from>
/// <description>Generates range from 0 to max</description>
/// <columns>
/// <column name="Value" type="long">Enumerated value</column>
/// </columns>
/// </example>
/// <example>
/// <from>#system.range(long min, long max)</from>
/// <description>Generates range from min to max</description>
/// <columns>
/// <column name="Value" type="long">Enumerated value</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
/// </virtual-constructors>
```

**Real example from System plugin showing overloads:**
```csharp
/// <virtual-constructor>
/// <virtual-param>Minimal value</virtual-param>
/// <virtual-param>Maximal value</virtual-param>
/// <examples>
/// <example>
/// <from>#system.range(long min, long max)</from>
/// <description>Gives the ability to generate ranged values</description>
/// <columns>
/// <column name="Value" type="long">Enumerated value</column>
/// </columns>
/// </example>
/// <example>
/// <from>#system.range(int min, int max)</from>
/// <description>Gives the ability to generate ranged values</description>
/// <columns>
/// <column name="Value" type="long">Enumerated value</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
```

Here's how the CANBus plugin documents its interface:

```csharp
/// <description>
/// Provides schema to work with CAN bus data.
/// </description>
/// <short-description>
/// Provides schema to work with CAN bus data.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class CANBusSchema : SchemaBase
{
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// </environmentVariables>
    /// #can.messages(string dbc)
    /// </from>
    /// <description>Parses dbc file and returns all messages defined within it.</description>
    /// <columns>
    /// <column name="Id" type="uint">ID of the message entity</column>
    /// <column name="Name" type="string">Name of the message entity</column>
    /// <column name="Signals" type="SignalEntity[]">Signals of the message</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    /// <additional-tables>
    /// <additional-table>
    /// <description>Represent possible values of a signal</description>
    /// <columns type="ValueMapEntity[]">
    /// <column name="Value" type="int">Value of signal</column>
    /// <column name="Name" type="string">Name of the value</column>
    /// </columns>
    /// </additional-table>
    /// </additional-tables>
    public CANBusSchema() : base("can", CreateLibrary())
    {
    }
}
```

### Study Reference Examples

Study these excellent examples in the repository:
- **`Musoq.DataSources.CANBus/CANBusSchema.cs`** - Complex schema with multiple virtual constructors and additional tables
- **`Musoq.DataSources.OpenAI/OpenAiSchema.cs`** - API integration with environment variables and dynamic columns
- **`Musoq.DataSources.Docker/DockerSchema.cs`** - Multiple tables with different column sets
- **`Musoq.DataSources.System/SystemSchema.cs`** - Simple schema with parameter documentation

### Environment Variable Usage Patterns

After documenting environment variables in your XML annotations, use them in your RowSource constructor:

#### Required Environment Variables
```csharp
public class MyRowSource : RowSourceBase<MyEntity>
{
    private readonly string _apiKey;
    private readonly string _endpoint;

    public MyRowSource(RuntimeContext runtimeContext, string? customEndpoint = null)
    {
        // Required environment variable - will throw if missing
        _apiKey = runtimeContext.EnvironmentVariables["API_KEY"];
        
        // Optional with default
        _endpoint = customEndpoint ?? 
                   runtimeContext.EnvironmentVariables.GetValueOrDefault("API_ENDPOINT", "https://default.api.com");
    }
}
```

#### Common Patterns from Existing Plugins

**OpenAI pattern:**
```csharp
// Required API key, no default
var apiKey = runtimeContext.EnvironmentVariables["OPENAI_API_KEY"];
```

**Ollama pattern:**
```csharp
// Optional with sensible default
var baseUrl = runtimeContext.EnvironmentVariables.GetValueOrDefault("OLLAMA_BASE_URL", "http://localhost:11434");
```

**Postgres pattern:**
```csharp
// Required connection string
var connectionString = runtimeContext.EnvironmentVariables["NPGSQL_CONNECTION_STRING"];
```

---

## Documentation Generation

🔥 **Critical**: Your plugin must generate XML documentation files for the metadata to be processed correctly.

### Project Configuration (Mandatory)

**This exact configuration is required in your `.csproj` file:**

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <!-- Other properties -->
</PropertyGroup>

<!-- This target is CRITICAL - it ensures XML files are included in the package -->
<Target Name="_ResolveCopyLocalNuGetPackageXmls" AfterTargets="ResolveReferences">
    <ItemGroup>
        <ReferenceCopyLocalPaths Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')" 
                                Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)' != '' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
    </ItemGroup>
</Target>
```

⚠️ **Without the `_ResolveCopyLocalNuGetPackageXmls` target, your XML metadata will not be available to Musoq at runtime!**

### Complete .csproj Template

Copy this exact template for new plugins:

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
        <None Include="../LICENSE" Pack="true" Visible="false" PackagePath=""/>
    </ItemGroup>

    <!-- CRITICAL: This target includes XML documentation files -->
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
        <!-- Add your specific dependencies here -->
    </ItemGroup>
</Project>
```

**This configuration is used by ALL working plugins in the repository.**

### What Gets Generated

When you build your plugin:
1. XML documentation files are created from your annotations
2. Musoq uses these files to provide:
   - IntelliSense support
   - Schema discovery
   - Parameter validation
   - Dynamic column information
   - Help system content

### Validation

Build your plugin and verify:
```bash
dotnet build
# Look for YourPlugin.xml in the output directory
ls bin/Debug/net8.0/YourPlugin.xml
```

The generated XML file should contain your metadata annotations properly formatted.

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

### 1. XML Metadata Documentation (Critical)

**Always provide comprehensive XML metadata annotations:**

```csharp
/// <description>
/// Clear, concise description of what your plugin does
/// </description>
/// <virtual-constructors>
/// <virtual-constructor>
/// <examples>
/// <example>
/// <from>
/// <environmentVariables>
/// <environmentVariable name="REQUIRED_VAR" isRequired="true">Description</environmentVariable>
/// </environmentVariables>
/// #yourschema.method(string param1, int param2)
/// </from>
/// <description>Detailed description of this method's functionality</description>
/// <columns>
/// <column name="ColumnName" type="type">What this column represents</column>
/// </columns>
/// </example>
/// </examples>
/// </virtual-constructor>
/// </virtual-constructors>
```

**Key points:**
- Document ALL parameters and their purposes
- Include environment variable requirements
- Describe each column clearly
- Provide meaningful examples
- Keep descriptions user-friendly

### 2. Documentation Generation

**Always enable XML documentation generation in your .csproj:**

```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

### 3. Error Handling

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

### 4. Resource Management

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

### 5. Performance Considerations

- Use chunking for large datasets
- Implement pagination when possible
- Consider caching for frequently accessed data
- Use async/await for I/O operations

### 6. Documentation

- Add XML documentation to all public members
- Include usage examples in schema comments
- Document environment variables and configuration

### 7. Versioning

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

## Reference Plugins

Instead of a separate example project, study these existing plugins in the repository for real-world implementations:

### Simple Plugin - System Plugin
**Location**: `Musoq.DataSources.System/`

A minimal plugin demonstrating basic concepts:
- **SystemSchema.cs** - Simple schema with dual and range tables
- **DualEntity.cs** - Basic entity with just a Value property
- **RangeItemEntity.cs** - Simple numeric entity
- **EmptyLibrary.cs** - Minimal library implementation

Perfect starting point for understanding the basic plugin structure.

### API Integration - OpenAI Plugin  
**Location**: `Musoq.DataSources.OpenAI/`

Advanced plugin showing HTTP API integration:
- **OpenAiSchema.cs** - Schema with comprehensive documentation
- **OpenAiApi.cs** - HTTP client implementation
- **OpenAiLibrary.cs** - Custom functions for AI operations
- **OpenAiEntity.cs** - Complex entity with multiple properties

Great example for building plugins that integrate with REST APIs.

### Complex Plugin - Docker Plugin
**Location**: `Musoq.DataSources.Docker/`

Comprehensive plugin with multiple related tables:
- Multiple entity types (Container, Image, Network, Volume)
- Complex schema with many table variants
- Rich metadata and helper functions
- External service integration patterns

### Database Plugin - Postgres Plugin
**Location**: `Musoq.DataSources.Postgres/`

Example of database connectivity:
- Connection string handling
- Database-specific data types
- Query optimization patterns
- Error handling for database operations

### Recommended Study Path

1. **Start with System Plugin** - Understand basic structure
2. **Review OpenAI Plugin** - Learn API integration patterns  
3. **Examine Docker Plugin** - See complex multi-table schemas
4. **Study Postgres Plugin** - Understand database connectivity

Each plugin follows the same architectural patterns but demonstrates different complexity levels and use cases.

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

Study these existing plugins for real-world examples and implementation patterns:

### 🤖 AI & Language Models
- **OpenAI** (`Musoq.DataSources.OpenAI/`) - GPT API integration with custom AI functions
- **Ollama** (`Musoq.DataSources.Ollama/`) - Local LLM integration patterns

### 🐳 Infrastructure & DevOps  
- **Docker** (`Musoq.DataSources.Docker/`) - Container, image, network, and volume management
- **Kubernetes** (`Musoq.DataSources.Kubernetes/`) - k8s resource queries and monitoring

### 🗄️ Databases
- **Postgres** (`Musoq.DataSources.Postgres/`) - PostgreSQL database connectivity patterns
- **Sqlite** (`Musoq.DataSources.Sqlite/`) - SQLite database integration

### 📁 Files & Data
- **Archives** (`Musoq.DataSources.Archives/`) - ZIP, RAR, and archive file processing
- **FlatFile** (`Musoq.DataSources.FlatFile/`) - Fixed-width file parsing
- **SeparatedValues** (`Musoq.DataSources.SeparatedValues/`) - CSV and delimiter-separated files
- **Json** (`Musoq.DataSources.Json/`) - JSON file and API response processing

### ⚙️ System & Utilities
- **System** (`Musoq.DataSources.System/`) - System utilities and range generators
- **Os** (`Musoq.DataSources.Os/`) - Operating system information and processes
- **Time** (`Musoq.DataSources.Time/`) - Date/time manipulation utilities
- **Git** (`Musoq.DataSources.Git/`) - Version control system integration

### 🌐 Web & APIs
- **Airtable** (`Musoq.DataSources.Airtable/`) - Airtable API integration

Each plugin demonstrates different architectural patterns and complexity levels, making them excellent references for your own implementations.

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