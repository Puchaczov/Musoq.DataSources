using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Json;

/// <description>
///     Provides schema to work with json files
/// </description>
/// <short-description>
///     Provides schema to work with json files
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class JsonSchema : SchemaBase
{
    private const string FileTable = "file";
    private const string SchemaName = "json";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to the json file</virtual-param>
    ///         <virtual-param>Path to the json schema file</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#json.file(string jsonFilePath, string jsonSchemaFilePath)</from>
    ///                 <description>Gives the ability to process json files</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public JsonSchema()
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new JsonTable((string)parameters[1]);
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="interCommunicator">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext interCommunicator, params object[] parameters)
    {
        return new JsonSource((string)parameters[0], interCommunicator);
    }

    /// <summary>
    ///     Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();

        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<JsonSource>(FileTable));

        return constructors.ToArray();
    }

    /// <summary>
    ///     Gets raw constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">Name of the data source method</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of constructor information for the specified method</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            FileTable => [CreateFileMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {FileTable}")
        };
    }

    /// <summary>
    ///     Gets raw constructor information for all data source methods in the schema.
    /// </summary>
    /// <param name="runtimeContext">Runtime context</param>
    /// <returns>Array of constructor information for all methods</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return [CreateFileMethodInfo()];
    }

    private static SchemaMethodInfo CreateFileMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("jsonFilePath", typeof(string)),
                ("jsonSchemaFilePath", typeof(string))
            ]);

        return new SchemaMethodInfo(FileTable, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new JsonLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}