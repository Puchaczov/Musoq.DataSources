using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Sqlite;

/// <summary>
/// The SqliteSchema class extends SchemaBase and is responsible for managing SQLite schema operations.
/// It also creates and manages the SqliteTable, SqliteRowSource, and MethodsAggregator instances.
/// </summary>
public class SqliteSchema : SchemaBase
{
    private const string SchemaName = "sqlite";
    
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="SQLITE_CONNECTION_STRING" isRequired="true">Sqlite connection string</environmentVariable>
    /// </environmentVariables>
    /// #sqlite.tableName()
    /// </from>
    /// <description>Gives ability to process sqlite table</description>
    /// <columns isDynamic="true"></columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public SqliteSchema() 
        : base(SchemaName, CreateLibrary())
    {
    }
    
    /// <summary>
    /// Retrieves an ISchemaTable instance for the specified table name.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    /// <param name="runtimeContext">The runtime context to be used.</param>
    /// <param name="parameters">Additional parameters to be passed.</param>
    /// <returns>An ISchemaTable instance.</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new SqliteTable(runtimeContext);
    }

    /// <summary>
    /// Retrieves a RowSource instance for the specified table name.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    /// <param name="runtimeContext">The runtime context to be used.</param>
    /// <param name="parameters">Additional parameters to be passed.</param>
    /// <returns>A RowSource instance.</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new SqliteRowSource(runtimeContext);
    }
    
    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var propertiesManager = new PropertiesManager();

        var library = new SqliteLibrary();

        methodsManager.RegisterLibraries(library);
        propertiesManager.RegisterProperties(library);

        return new MethodsAggregator(methodsManager, propertiesManager);
    }
}