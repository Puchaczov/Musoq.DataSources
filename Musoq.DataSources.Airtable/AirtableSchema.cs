using Musoq.DataSources.Airtable.Sources.Base;
using Musoq.DataSources.Airtable.Sources.Bases;
using Musoq.DataSources.Airtable.Sources.Table;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Airtable;

/// <description>
/// Provides interface to work with Airtable API.
/// </description>
/// <short-description>
/// Provides interface to work with Airtable API.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class AirtableSchema : SchemaBase
{
    private const string SchemaName = "airtable";
    
    private readonly IAirtableApi? _api;
    
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="MUSOQ_AIRTABLE_API_KEY" isRequired="true">Airtable API key</environmentVariable>
    /// </environmentVariables>
    /// #airtable.bases()
    /// </from>
    /// <description>Enumerate bases from Airtable API</description>
    /// <columns>
    /// <column name="Id" type="string">Base id</column>
    /// <column name="Name" type="string">Base name</column>
    /// <column name="PermissionLevel" type="string">Base description</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="MUSOQ_AIRTABLE_API_KEY" isRequired="true">Airtable API key</environmentVariable>
    /// <environmentVariable name="MUSOQ_AIRTABLE_BASE_ID" isRequired="true">Airtable base id</environmentVariable>
    /// </environmentVariables>
    /// #airtable.base()
    /// </from>
    /// <description>Enumerate base tables for Airtable API</description>
    /// <columns>
    /// <column name="Id" type="string">Base id</column>
    /// <column name="Name" type="string">Base name</column>
    /// <column name="PrimaryFieldId" type="string">Base description</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// <virtual-constructor>
    /// <virtual-param>Table name</virtual-param>
    /// <examples>
    /// <example>
    /// <from>
    /// <environmentVariables>
    /// <environmentVariable name="MUSOQ_AIRTABLE_API_KEY" isRequired="true">Airtable API key</environmentVariable>
    /// <environmentVariable name="MUSOQ_AIRTABLE_BASE_ID" isRequired="true">Airtable base id</environmentVariable>
    /// </environmentVariables>
    /// #airtable.records(string tableName)
    /// </from>
    /// <description>Enumerate records for specific table</description>
    /// <columns isDynamic="true"></columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public AirtableSchema() 
        : base(SchemaName, CreateLibrary())
    {
        _api = null;
    }

    internal AirtableSchema(IAirtableApi api)
        : base(SchemaName, CreateLibrary())
    {
        _api = api;
    }
    
    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    /// <exception cref="NotSupportedException">Thrown when data source is not supported.</exception>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object?[] parameters)
    {
        return name switch
        {
            "bases" => new AirtableBasesSchemaTable(),
            "base" => new AirtableBaseSchemaTable(),
            "records" => new AirtableTableSchemaTable(
                _api ?? new AirtableApi(
                    runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_API_KEY"], 
                    runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_BASE_ID"],
                    Convert.ToString(parameters[0])), runtimeContext),
            _ => throw new NotSupportedException($"Table {name} is not supported.")
        };
    }

    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    /// <exception cref="NotSupportedException">Thrown when data source is not supported.</exception>
    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object?[] parameters)
    {
        return name switch
        {
            "bases" => new AirtableBasesRowSource(
                _api ?? new AirtableApi(runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_API_KEY"]), runtimeContext),
            "base" => new AirtableBaseRowSource(
                _api ?? new AirtableApi(runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_API_KEY"], runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_BASE_ID"]), runtimeContext),
            "records" => new AirtableTableRowSource(
                _api ?? new AirtableApi(
                    runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_API_KEY"], 
                    runtimeContext.EnvironmentVariables["MUSOQ_AIRTABLE_BASE_ID"],
                    Convert.ToString(parameters[0])), runtimeContext),
            _ => throw new NotSupportedException($"Table {name} is not supported.")
        };
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new AirtableLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}