using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Sqlite;

/// <description>
///     Provides ability to work with sqlite database
/// </description>
/// <short-description>
///     Provides schema to work with sqlite database
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class SqliteSchema : SchemaBase
{
    private const string SchemaName = "sqlite";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="SQLITE_CONNECTION_STRING" isRequired="true">Sqlite connection string</environmentVariable>
    ///                     </environmentVariables>
    ///                     #sqlite.tableName()
    ///                 </from>
    ///                 <description>Gives ability to process sqlite table</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public SqliteSchema()
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    ///     Retrieves an ISchemaTable instance for the specified table name.
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
    ///     Retrieves a RowSource instance for the specified table name.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    /// <param name="runtimeContext">The runtime context to be used.</param>
    /// <param name="parameters">Additional parameters to be passed.</param>
    /// <returns>A RowSource instance.</returns>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new SqliteRowSource(runtimeContext);
    }

    /// <summary>
    ///     Gets the constructor information for a specific data source method (dynamic table).
    /// </summary>
    /// <param name="methodName">The name of the table to get constructor information for.</param>
    /// <param name="runtimeContext">The runtime context.</param>
    /// <returns>An array of SchemaMethodInfo objects representing the table's constructors.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            []
        );

        return [new SchemaMethodInfo(methodName, constructorInfo)];
    }

    /// <summary>
    ///     Gets constructor information for all data source methods.
    ///     For Sqlite, tables are dynamic and discovered at runtime, so this returns an empty array.
    /// </summary>
    /// <param name="runtimeContext">The runtime context.</param>
    /// <returns>An empty array since table names are dynamic.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return [];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new SqliteLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}