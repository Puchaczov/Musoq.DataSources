# Musoq Plugin Quick Start Template

This template provides a minimal working example to get you started quickly with Musoq plugin development.

## Quick Setup (5 minutes)

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

## Next Steps

1. **Customize the Entity**: Modify `SimpleEntity` to match your data structure
2. **Implement Real Data Source**: Replace the sample data in `SimpleRowSource` with actual data retrieval
3. **Add More Tables**: Create additional table types following the same pattern
4. **Add Custom Functions**: Extend `SimpleLibrary` with domain-specific functions
5. **Handle Parameters**: Add support for connection strings, filters, etc.

## Common Modifications

### Reading from a File
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

### Making HTTP Requests
```csharp
protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
{
    using var client = new HttpClient();
    var json = client.GetStringAsync("https://api.example.com/data").Result;
    var entities = JsonSerializer.Deserialize<List<SimpleEntity>>(json) ?? new List<SimpleEntity>();

    // Convert to resolvers and add to collection...
}
```

### Database Connection
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

This template gets you up and running quickly. For more advanced features and detailed explanations, refer to the full [Plugin Development Guide](PLUGIN_DEVELOPMENT_GUIDE.md).