using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.SeparatedValues;

/// <description>
///     Streams strict UTF-8 comma-, tab-, and semicolon-separated files after exact schema discovery.
/// </description>
/// <short-description>
///     Streams strict UTF-8 separated-values files with dynamic columns.
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
    ///         <virtual-param>Path to a strict UTF-8 separated-values file; UTF-8 BOM is allowed</virtual-param>
    ///         <virtual-param>Whether the first logical record is a header</virtual-param>
    ///         <virtual-param>Number of physical preamble lines to skip</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#separatedvalues.comma(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Discovers the complete CSV schema and streams requested columns</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to a strict UTF-8 separated-values file; UTF-8 BOM is allowed</virtual-param>
    ///         <virtual-param>Whether the first logical record is a header</virtual-param>
    ///         <virtual-param>Number of physical preamble lines to skip</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#separatedvalues.tab(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Discovers the complete TSV schema and streams requested columns</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to a strict UTF-8 separated-values file; UTF-8 BOM is allowed</virtual-param>
    ///         <virtual-param>Whether the first logical record is a header</virtual-param>
    ///         <virtual-param>Number of physical preamble lines to skip</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#separatedvalues.semicolon(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Discovers the complete semicolon-separated schema and streams requested columns</description>
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
        var sourceParameters = ParseParameters(parameters);

        return name.ToLowerInvariant() switch
        {
            CommaTable => CreateTable(",", metadataContext, sourceParameters),
            TabTable => CreateTable("\t", metadataContext, sourceParameters),
            SemicolonTable => CreateTable(";", metadataContext, sourceParameters),
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
        var sourceParameters = ParseParameters(parameters);

        return name.ToLowerInvariant() switch
        {
            CommaTable => CreateSource<T>(name, ",", executionContext, sourceParameters),
            TabTable => CreateSource<T>(name, "\t", executionContext, sourceParameters),
            SemicolonTable => CreateSource<T>(name, ";", executionContext, sourceParameters),
            _ => base.GetRowSource<T>(name, executionContext, parameters)
        };
    }

    /// <summary>
    ///     Describes the exact schema discovered for a separated-values file.
    /// </summary>
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

    /// <summary>
    ///     Describes optional runtime settings for separated-values sources.
    /// </summary>
    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object?[] parameters)
    {
        return
        [
            new SourceRuntimeSettingRequirement(
                SeparatedValuesParallelScanOptions.MaximumParallelismSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Execution,
                "Maximum file-scan parallelism. Missing or 0 selects automatically; 1 forces sequential scanning.")
        ];
    }

    /// <summary>
    ///     Plans projection, scalar predicate, and safe slicing pushdown.
    /// </summary>
    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters)
    {
        var sourceParameters = ParseParameters(parameters);

        return name.ToLowerInvariant() switch
        {
            CommaTable => SeparatedValuesSourcePlanner.Plan(
                SeparatedValuesSchemaDiscovery.GetSnapshot(
                    sourceParameters.Path,
                    ",",
                    sourceParameters.HasHeader,
                    sourceParameters.SkipLines),
                request),
            TabTable => SeparatedValuesSourcePlanner.Plan(
                SeparatedValuesSchemaDiscovery.GetSnapshot(
                    sourceParameters.Path,
                    "\t",
                    sourceParameters.HasHeader,
                    sourceParameters.SkipLines),
                request),
            SemicolonTable => SeparatedValuesSourcePlanner.Plan(
                SeparatedValuesSchemaDiscovery.GetSnapshot(
                    sourceParameters.Path,
                    ";",
                    sourceParameters.HasHeader,
                    sourceParameters.SkipLines),
                request),
            _ => SourcePlanResult.RejectAll(request)
        };
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
        SourceParameters parameters)
    {
        return new SeparatedValuesTable(
            SeparatedValuesSchemaDiscovery.GetSnapshot(
                parameters.Path,
                separator,
                parameters.HasHeader,
                parameters.SkipLines,
                metadataContext.EndWorkToken),
            metadataContext);
    }

    private static RowSource<T> CreateSource<T>(
        string name,
        string separator,
        SourceExecutionContext executionContext,
        SourceParameters parameters)
    {
        RowSource<object?[]> source = new SeparatedValuesFromFileRowsSource(
            parameters.Path,
            separator,
            parameters.HasHeader,
            parameters.SkipLines,
            executionContext);

        return EnsureSourceType<T, object?[]>(name, source);
    }

    private static SourceParameters ParseParameters(object?[] parameters)
    {
        if (parameters is not [string path, bool hasHeader, int skipLines])
        {
            throw new ArgumentException(
                "Separated-values sources require exactly (string path, bool hasHeader, int skipLines).",
                nameof(parameters));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        return new SourceParameters(path, hasHeader, skipLines);
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

    private readonly record struct SourceParameters(string Path, bool HasHeader, int SkipLines);
}
