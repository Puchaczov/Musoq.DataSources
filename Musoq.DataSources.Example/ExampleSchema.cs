using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using Musoq.DataSources.Example.Sources;
using Musoq.DataSources.Example.Tables;

namespace Musoq.DataSources.Example;

/// <description>
/// Provides example data source for demonstrating Musoq plugin development
/// </description>
/// <short-description>
/// Example data source for plugin development demonstration
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class ExampleSchema : SchemaBase
{
    private const string SchemaName = "example";
    private const string TableName = "data";

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>#example.data()</from>
    /// <description>Gets example data with default settings (10 records)</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Display name</column>
    /// <column name="CreatedDate" type="DateTime">Creation timestamp</column>
    /// <column name="Value" type="int">Numeric value</column>
    /// <column name="IsActive" type="bool">Active status</column>
    /// <column name="Category" type="string">Category classification</column>
    /// <column name="Description" type="string">Optional description</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Number of records to generate</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#example.data(int count)</from>
    /// <description>Gets example data with specified number of records</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Display name</column>
    /// <column name="CreatedDate" type="DateTime">Creation timestamp</column>
    /// <column name="Value" type="int">Numeric value</column>
    /// <column name="IsActive" type="bool">Active status</column>
    /// <column name="Category" type="string">Category classification</column>
    /// <column name="Description" type="string">Optional description</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Number of records to generate</virtual-param>
    /// <virtual-param>Filter string for names/categories</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#example.data(int count, string filter)</from>
    /// <description>Gets filtered example data with specified number of records</description>
    /// <columns>
    /// <column name="Id" type="string">Unique identifier</column>
    /// <column name="Name" type="string">Display name</column>
    /// <column name="CreatedDate" type="DateTime">Creation timestamp</column>
    /// <column name="Value" type="int">Numeric value</column>
    /// <column name="IsActive" type="bool">Active status</column>
    /// <column name="Category" type="string">Category classification</column>
    /// <column name="Description" type="string">Optional description</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public ExampleSchema() 
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    /// Gets the table metadata for the specified table name
    /// </summary>
    /// <param name="name">Table name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters passed to the table</param>
    /// <returns>Table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            TableName => new ExampleTable(),
            _ => throw new NotSupportedException($"Table '{name}' is not supported by the Example schema.")
        };
    }

    /// <summary>
    /// Gets the row source for the specified table name
    /// </summary>
    /// <param name="name">Table name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters passed to the row source</param>
    /// <returns>Row source instance</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            TableName => new ExampleRowSource(
                runtimeContext,
                parameters.Length > 0 ? Convert.ToInt32(parameters[0]) : 10,
                parameters.Length > 1 ? parameters[1]?.ToString() : null
            ),
            _ => throw new NotSupportedException($"Table '{name}' is not supported by the Example schema.")
        };
    }

    /// <summary>
    /// Gets the available constructors for this schema
    /// </summary>
    /// <returns>Array of schema method information</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();
        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<ExampleRowSource>(TableName));
        return constructors.ToArray();
    }

    /// <summary>
    /// Creates the library aggregator with custom functions
    /// </summary>
    /// <returns>Methods aggregator</returns>
    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new ExampleLibrary();
        methodsManager.RegisterLibraries(library);
        return new MethodsAggregator(methodsManager);
    }
}