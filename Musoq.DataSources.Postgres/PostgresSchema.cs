using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Postgres;

/// <description>
///     Provides schema to work with postgres database
/// </description>
/// <short-description>
///     Provides schema to work with postgres database
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class PostgresSchema : SchemaBase
{
    private const string SchemaName = "postgres";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Schema the table belongs to</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>
    ///                     <environmentVariables>
    ///                         <environmentVariable name="NPGSQL_CONNECTION_STRING" isRequired="true">
    ///                             Postgres connections
    ///                             string
    ///                         </environmentVariable>
    ///                     </environmentVariables>
    ///                     #postgres.tableName('schemaName')
    ///                 </from>
    ///                 <description>Gives ability to process sqlite table</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public PostgresSchema()
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
        return new PostgresTable(runtimeContext, (string)parameters[0]);
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
        return new PostgresRowSource(runtimeContext, (string)parameters[0]);
    }

    /// <summary>
    ///     Gets raw information's about specific method in the schema.
    /// </summary>
    /// <param name="methodName">Method name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("schemaName", typeof(string))
            ]);

        return
        [
            new SchemaMethodInfo(methodName, constructorInfo)
        ];
    }

    /// <summary>
    ///     Gets raw information's about all tables in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return [];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new PostgresLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}