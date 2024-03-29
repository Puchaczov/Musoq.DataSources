﻿using Musoq.Schema.DataSources;
using Musoq.Schema.Exceptions;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using System.Collections.Generic;
using Musoq.Schema;

namespace Musoq.DataSources.FlatFile;

/// <description>
/// Provides schema to work with flat files
/// </description>
/// <short-description>
/// Provides schema to work with flat files
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class FlatFileSchema : SchemaBase
{
    private const string SchemaName = "Flat";

    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <virtual-param>Path of the given file</virtual-param>
    /// <examples>
    /// <example>
    /// <from>#flat.file(string path)</from>
    /// <description>Gives ability to process flat files</description>
    /// <columns>
    /// <column name="LineNumber" type="int">Line number of a given file</column>
    /// <column name="Line" type="string">Line of a given file</column>
    /// </columns>
    /// </example>
    /// </examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public FlatFileSchema()
        : base(SchemaName, CreateLibrary())
    {
    }

    /// <summary>
    /// Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="runtimeContext">Runtime context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "file" => new FlatFileTable(),
            _ => throw new TableNotFoundException(nameof(name))
        };
    }

    /// <summary>
    /// Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="interCommunicator">Runtime context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource GetRowSource(string name, RuntimeContext interCommunicator, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            "file" => new FlatFileSource((string) parameters[0], interCommunicator),
            _ => throw new SourceNotFoundException(nameof(name))
        };
    }

    /// <summary>
    /// Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();

        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<FlatFileSource>("file"));

        return constructors.ToArray();
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new FlatFileLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}