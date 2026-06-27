using System;
using System.Collections.Generic;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.SeparatedValues;

/// <description>
///     Provides schema to work with separated values like .csv, .tsv, semicolon.
/// </description>
/// <short-description>
///     Provides schema to work with separated values like .csv, .tsv, semicolon.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class SeparatedValuesSchema : SchemaBase
{
    private const string SchemaName = "SeparatedValues";
    private const string CommaTable = "comma";
    private const string TabTable = "tab";
    private const string SemicolonTable = "semicolon";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to the given file</virtual-param>
    ///         <virtual-param>Does the file has header</virtual-param>
    ///         <virtual-param>How many lines should be skipped</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#separatedvalues.comma(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Gives the ability to process .CSV files</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to the given file</virtual-param>
    ///         <virtual-param>Does the file has header</virtual-param>
    ///         <virtual-param>How many lines should be skipped</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#separatedvalues.tab(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Gives the ability to process .TSV files</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to the given file</virtual-param>
    ///         <virtual-param>Does the file has header</virtual-param>
    ///         <virtual-param>How many lines should be skipped</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#separatedvalues.semicolon(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Gives the ability to process semicolon files</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public SeparatedValuesSchema()
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data Source name</param>
    /// <param name="metadataContext">Metadata context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object?[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            CommaTable => CreateTable(",", metadataContext, parameters),
            TabTable => CreateTable("\t", metadataContext, parameters),
            SemicolonTable => CreateTable(";", metadataContext, parameters),
            _ => base.GetTableByName(name, metadataContext, parameters)
        };
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="executionContext">Execution context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object?[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            CommaTable => CreateSource<T>(name, ",", executionContext, parameters),
            TabTable => CreateSource<T>(name, "\t", executionContext, parameters),
            SemicolonTable => CreateSource<T>(name, ";", executionContext, parameters),
            _ => base.GetRowSource<T>(name, executionContext, parameters)
        };
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object?[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object?[] parameters)
    {
        return [];
    }

    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters)
    {
        return SourcePlanResult.RejectAll(request);
    }

    /// <summary>
    ///     Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        return
        [
            CreateCommaMethodInfo(),
            CreateTabMethodInfo(),
            CreateSemicolonMethodInfo()
        ];
    }

    /// <summary>
    ///     Gets the constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">The name of the method to get constructor information for.</param>
    /// <param name="metadataContext">Metadata context.</param>
    /// <returns>An array of SchemaMethodInfo objects representing the method's constructors.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            CommaTable => [CreateCommaMethodInfo()],
            TabTable => [CreateTabMethodInfo()],
            SemicolonTable => [CreateSemicolonMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {CommaTable}, {TabTable}, {SemicolonTable}")
        };
    }

    /// <summary>
    ///     Gets constructor information for all data source methods.
    /// </summary>
    /// <param name="metadataContext">Metadata context.</param>
    /// <returns>An array of all SchemaMethodInfo objects.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return GetConstructors();
    }

    private static ISchemaTable CreateTable(
        string separator,
        SourceMetadataContext metadataContext,
        object?[] parameters)
    {
        if (metadataContext.AllColumns.Count > 0)
            return new InitiallyInferredTable(metadataContext.AllColumns);

        return new SeparatedValuesTable(
            (string)parameters[0]!,
            separator,
            (bool)parameters[1]!,
            (int)parameters[2]!)
        {
            InferredColumns = metadataContext.AllColumns
        };
    }

    private RowSource<T> CreateSource<T>(
        string name,
        string separator,
        SourceExecutionContext executionContext,
        object?[] parameters)
    {
        RowSource<object?[]> source = parameters[0] switch
        {
            IReadOnlyTable table => new SeparatedValuesFromFileRowsSource(table, separator, executionContext),
            Stream stream => new SeparatedValuesFromStreamRowsSource(
                stream,
                separator,
                (bool)parameters[1]!,
                (int)parameters[2]!,
                executionContext),
            string path => new SeparatedValuesFromFileRowsSource(
                path,
                separator,
                (bool)parameters[1]!,
                (int)parameters[2]!,
                executionContext),
            _ => throw new NotSupportedException($"Source parameter type '{parameters[0]?.GetType().Name}' is not supported.")
        };

        return EnsureSourceType<T, object?[]>(name, source);
    }

    private static SchemaMethodInfo CreateCommaMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("hasHeader", typeof(bool)),
                ("skipLines", typeof(int))
            ]);

        return new SchemaMethodInfo(CommaTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateTabMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("hasHeader", typeof(bool)),
                ("skipLines", typeof(int))
            ]);

        return new SchemaMethodInfo(TabTable, constructorInfo);
    }

    private static SchemaMethodInfo CreateSemicolonMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("hasHeader", typeof(bool)),
                ("skipLines", typeof(int))
            ]);

        return new SchemaMethodInfo(SemicolonTable, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new SeparatedValuesLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}
