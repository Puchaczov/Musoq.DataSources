using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Json;

/// <description>
///     Streams strict UTF-8 JSON files after exact source-driven schema discovery.
/// </description>
/// <short-description>
///     Streams strict UTF-8 JSON files with dynamic top-level columns.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class JsonSchema : SchemaBase, IQueryScopedRowSourceSchema
{
    private const string FileTable = "file";
    private const string SchemaName = "json";

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param name="jsonFilePath" kind="path" relative-to="invocation-directory">Path to a strict UTF-8 JSON file; UTF-8 BOM is allowed</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>json.file(string jsonFilePath)</from>
    ///                 <description>Discovers the complete top-level schema and streams the requested columns</description>
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
    /// <param name="metadataContext">Metadata context</param>
    /// <param name="parameters">Parameters to pass to data source</param>
    /// <returns>Requested table metadata</returns>
    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        if (parameters is not [string path])
            throw new ArgumentException("json.file requires exactly one string path parameter.", nameof(parameters));

        if (!string.Equals(name, FileTable, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Data source '{name}' is not supported by {SchemaName} schema.");

        return new JsonTable(
            JsonSchemaDiscovery.GetSnapshot(path, metadataContext.EndWorkToken),
            metadataContext);
    }

    /// <summary>
    ///     Gets the data source based on the given data source and parameters.
    /// </summary>
    /// <param name="name">Data source name</param>
    /// <param name="executionContext">Execution context</param>
    /// <param name="parameters">Parameters to pass data to data source</param>
    /// <returns>Data source</returns>
    public override RowSource<T> GetRowSource<T, TArguments>(
        string name,
        SourceExecutionContext executionContext,
        TArguments parameters)
    {
        if (!string.Equals(name, FileTable, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Data source '{name}' is not supported by {SchemaName} schema.");
        _ = ReadPath(parameters);
        throw new NotSupportedException("json.file requires query-scoped concrete rows and a typed materializer. Use GetQueryScopedRowSource.");
    }

    /// <summary>
    ///     Describes the exact schema discovered for a JSON file.
    /// </summary>
    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = [],
            TransferCapabilities = SourceTransferCapabilities.QueryScopedRows |
                                   SourceTransferCapabilities.LogicalScalarReads
        };
    }

    /// <summary>
    /// Streams directly into the concrete row carrier selected by query compilation.
    /// </summary>
    public RowSource<TRow> GetQueryScopedRowSource<TRow, TMaterializer, TArguments>(
        string name,
        QueryScopedRowSourceRequest request,
        TArguments parameters)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
        where TArguments : struct, ISourceArguments
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(name, FileTable, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Data source '{name}' is not supported by {SchemaName} schema.");
        if (parameters.Count != 1)
            throw new ArgumentException("json.file requires exactly one string path parameter.", nameof(parameters));
        return new JsonQueryRowSource<TRow, TMaterializer>(ReadPath(parameters), request);
    }

    private static string ReadPath<TArguments>(TArguments parameters)
        where TArguments : struct, ISourceArguments
    {
        if (parameters.Count != 1)
            throw new ArgumentException("json.file requires exactly one string path parameter.", nameof(parameters));
        try
        {
            return parameters.GetValue<string>(0) ??
                throw new ArgumentException("json.file requires exactly one string path parameter.", nameof(parameters));
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException("json.file requires exactly one string path parameter.", nameof(parameters), exception);
        }
    }

    /// <summary>
    ///     Describes optional runtime settings for the JSON source.
    /// </summary>
    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object[] parameters)
    {
        return
        [
            new SourceRuntimeSettingRequirement(
                JsonParallelScanOptions.MaximumParallelismSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Execution,
                "Maximum file-scan parallelism. Missing or 0 selects automatically; 1 forces sequential scanning.")
        ];
    }

    /// <summary>
    ///     Plans JSON projection, scalar predicate, and safe slicing pushdown.
    /// </summary>
    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            FileTable when parameters is [string path] => JsonSourcePlanner.Plan(
                JsonSchemaDiscovery.GetSnapshot(path),
                request),
            FileTable => throw new ArgumentException(
                "json.file requires exactly one string path parameter.",
                nameof(parameters)),
            _ => SourcePlanResult.RejectAll(request)
        };
    }

    /// <summary>
    ///     Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        return [CreateFileMethodInfo()];
    }

    /// <summary>
    ///     Gets raw constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">Name of the data source method</param>
    /// <param name="metadataContext">Metadata context</param>
    /// <returns>Array of constructor information for the specified method</returns>
    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
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
    /// <param name="metadataContext">Metadata context</param>
    /// <returns>Array of constructor information for all methods</returns>
    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return [CreateFileMethodInfo()];
    }

    private static SchemaMethodInfo CreateFileMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [("jsonFilePath", typeof(string))]);

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
