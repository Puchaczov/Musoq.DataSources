using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.SeparatedValues;

/// <description>
///     Streams strict UTF-8 comma-, tab-, and semicolon-separated files with bounded schema resolution.
/// </description>
/// <short-description>
///     Streams strict UTF-8 separated-values files with dynamic columns.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class SeparatedValuesSchema : SchemaBase, IQueryScopedRowSourceSchema
{
    private const string SchemaName = "SeparatedValues";
    private const string CommaTable = "comma";
    private const string TabTable = "tab";
    private const string SemicolonTable = "semicolon";
    private const string DelimitedTable = "delimited";
    private readonly SeparatedValuesPipelineModules _modules;
    private readonly AsyncLocal<PendingContract?> _pendingContract = new();

    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Path to a strict UTF-8 separated-values file; UTF-8 BOM is allowed</virtual-param>
    ///         <virtual-param>Whether the first logical record is a header</virtual-param>
    ///         <virtual-param>Number of physical preamble lines to skip</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>separatedvalues.comma(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Resolves a bounded CSV schema and streams requested columns</description>
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
    ///                 <from>separatedvalues.tab(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Resolves a bounded TSV schema and streams requested columns</description>
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
    ///                 <from>separatedvalues.semicolon(string path, bool hasHeader, int skipLines)</from>
    ///                 <description>Resolves a bounded semicolon-separated schema and streams requested columns</description>
    ///                 <columns isDynamic="true"></columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public SeparatedValuesSchema()
        : this(SeparatedValuesPipelineModules.Default)
    {
    }

    internal SeparatedValuesSchema(SeparatedValuesPipelineModules modules)
        : base(SchemaName.ToLowerInvariant(), CreateLibrary())
    {
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
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
        var sourceParameters = ParseParameters(name, parameters);

        return name.ToLowerInvariant() switch
        {
            CommaTable or TabTable or SemicolonTable or DelimitedTable => CreateTable(
                sourceParameters.Separator,
                metadataContext,
                sourceParameters),
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
        ArgumentNullException.ThrowIfNull(executionContext);

        return name.ToLowerInvariant() switch
        {
            CommaTable or TabTable or SemicolonTable or DelimitedTable => throw LegacyRowTransferUnsupported(
                executionContext.Plan.Identity,
                name,
                typeof(T)),
            _ => base.GetRowSource<T>(name, executionContext, parameters)
        };
    }

    /// <summary>
    ///     Gets a row source that materializes directly into the current query's private row carrier.
    /// </summary>
    public RowSource<TRow> GetQueryScopedRowSource<TRow, TMaterializer>(
        string name,
        QueryScopedRowSourceRequest request,
        params object?[] parameters)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        ArgumentNullException.ThrowIfNull(request);
        var identity = request.ExecutionContext.Plan.Identity;
        var sourceParameters = ParseParameters(name, parameters);
        return name.ToLowerInvariant() switch
        {
            CommaTable or TabTable or SemicolonTable or DelimitedTable =>
                new SeparatedValuesQueryRowSource<TRow, TMaterializer>(
                    sourceParameters.Path,
                    sourceParameters.Separator,
                    sourceParameters.HasHeader,
                    sourceParameters.SkipLines,
                    request,
                    _modules.ScanPipeline,
                    _modules.DialectResolver.Resolve(
                        sourceParameters.Separator,
                        request.ExecutionContext.SourceRuntimeSettings)),
            _ => throw QueryScopedRowsUnavailable(
                identity,
                request.Shape.Fingerprint,
                $"data source '{name}' is not supported")
        };
    }

    /// <summary>
    ///     Describes the declared or bounded-sample schema for a separated-values file.
    /// </summary>
    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object?[] parameters)
    {
        var sourceParameters = ParseParameters(name, parameters);
        if (!SeparatedValuesQueryMetadata.TryValidateDeclaredColumns(
                context.MetadataContext.AllColumns,
                out var declaredMetadataReason))
            throw MandatoryQueryRowsUnavailable(context.Identity, declaredMetadataReason);

        var separator = sourceParameters.Separator;
        var dialect = _modules.DialectResolver.Resolve(separator, context.MetadataContext.SourceRuntimeSettings);
        var contract = Resolve(
            sourceParameters,
            dialect,
            context.MetadataContext.AllColumns,
            context.MetadataContext.SourceRuntimeSettings,
            context.MetadataContext.EndWorkToken);
        var table = new SeparatedValuesTable(contract.Snapshot, context.MetadataContext);

        if (!SeparatedValuesQueryMetadata.TryCreateForDescriptor(
                contract,
                table.Columns,
                out _,
                out var eligibilityReason))
            throw MandatoryQueryRowsUnavailable(context.Identity, eligibilityReason);

        contract = contract.WithDescriptorColumns(table.Columns);

        _pendingContract.Value = new PendingContract(
            CreateContractKey(sourceParameters, dialect),
            contract);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = typeof(object[]),
            Diagnostics = contract.Diagnostics,
            ContractDiagnostics = [],
            TransferCapabilities = SourceTransferCapabilities.QueryScopedRows |
                                    SourceTransferCapabilities.LogicalScalarReads
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
        var requirements = new List<SourceRuntimeSettingRequirement>
        {
            new SourceRuntimeSettingRequirement(
                SeparatedValuesParallelScanOptions.MaximumParallelismSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Execution,
                "Maximum file-scan parallelism. Missing or 0 selects automatically; 1 forces sequential scanning."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesInferenceOptions.MaximumBytesSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning,
                $"Maximum bytes read while resolving a dynamic schema. Default: {SeparatedValuesInferenceOptions.DefaultMaximumBytes:N0}."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesInferenceOptions.MaximumRowsSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning,
                $"Maximum complete rows inspected while resolving a dynamic schema. Default: {SeparatedValuesInferenceOptions.DefaultMaximumRows:N0}."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning,
                $"Cooperative schema-resolution deadline in milliseconds. Default: {SeparatedValuesInferenceOptions.DefaultMaximumTimeMilliseconds:N0}.")
        };

        if (string.Equals(name, DelimitedTable, StringComparison.OrdinalIgnoreCase))
        {
            requirements.AddRange(
            [
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.QuoteSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Quote character, or 'none'. Default: double quote."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.EscapeSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Quote escape mode: double, backslash, or none."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.WhitespaceSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Whether unquoted field whitespace is preserved or trimmed."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.BlankRecordSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Whether blank records are skipped or emitted."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.CommentPrefixSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Optional UTF-8 prefix for comment records."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.NullTokensSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "JSON array of unquoted null tokens."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.CultureSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Culture used for bounded inference and materializing typed values. Default: invariant."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.RecordEndingsSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                "Record endings: lf_crlf (default) or any."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.MaximumRecordBytesSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                $"Maximum logical record bytes. Default: {SeparatedValuesDialect.DefaultMaximumRecordBytes:N0}."),
            new SourceRuntimeSettingRequirement(
                SeparatedValuesDialect.MaximumBufferedBytesSettingName,
                false,
                false,
                SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning | SourceRuntimeSettingPhase.Execution,
                $"Maximum buffered bytes. Default: {SeparatedValuesDialect.DefaultMaximumBufferedBytes:N0}.")
            ]);
        }

        return requirements;
    }

    /// <summary>
    ///     Plans projection, scalar predicate, and safe slicing pushdown.
    /// </summary>
    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object?[] parameters)
    {
        var sourceParameters = ParseParameters(name, parameters);
        var dialect = _modules.DialectResolver.Resolve(sourceParameters.Separator, request.SourceRuntimeSettings);
        var contract = ConsumePendingContract(sourceParameters, dialect) ??
                       Resolve(sourceParameters, dialect, [], request.SourceRuntimeSettings, default);

        return name.ToLowerInvariant() switch
        {
            CommaTable or TabTable or SemicolonTable or DelimitedTable => SeparatedValuesSourcePlanner.Plan(
                contract,
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
            DelimitedTable => [CreateDelimitedMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {CommaTable}, {TabTable}, {SemicolonTable}, {DelimitedTable}")
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

    private ISchemaTable CreateTable(
        string separator,
        SourceMetadataContext metadataContext,
        SourceParameters parameters)
    {
        return new SeparatedValuesTable(
            Resolve(
                parameters,
                _modules.DialectResolver.Resolve(separator, metadataContext.SourceRuntimeSettings),
                metadataContext.AllColumns,
                metadataContext.SourceRuntimeSettings,
                metadataContext.EndWorkToken).Snapshot,
            metadataContext);
    }

    private SeparatedValuesSourceContract Resolve(
        SourceParameters parameters,
        SeparatedValuesDialect dialect,
        IReadOnlyCollection<ISchemaColumn> declaredColumns,
        IReadOnlyDictionary<string, string> runtimeSettings,
        System.Threading.CancellationToken cancellationToken)
    {
        return _modules.SchemaResolver.Resolve(new SeparatedValuesSchemaResolutionRequest(
            parameters.Path,
            ((char)dialect.Separator).ToString(),
            parameters.HasHeader,
            parameters.SkipLines,
            declaredColumns,
            runtimeSettings,
            cancellationToken,
            dialect));
    }

    private SeparatedValuesSourceContract? ConsumePendingContract(
        SourceParameters parameters,
        SeparatedValuesDialect dialect)
    {
        var pending = _pendingContract.Value;
        _pendingContract.Value = null;
        return pending is not null && pending.Key == CreateContractKey(parameters, dialect)
            ? pending.Contract
            : null;
    }

    private static ContractKey CreateContractKey(SourceParameters parameters, SeparatedValuesDialect dialect)
    {
        return new ContractKey(
            Path.GetFullPath(parameters.Path),
            dialect.Fingerprint,
            parameters.HasHeader,
            parameters.SkipLines);
    }

    private static string GetSeparator(string name)
    {
        return name.ToLowerInvariant() switch
        {
            CommaTable => ",",
            TabTable => "\t",
            SemicolonTable => ";",
            _ => throw new NotSupportedException(
                $"Data source '{name}' is not supported by {SchemaName} schema. " +
                $"Available data sources: {CommaTable}, {TabTable}, {SemicolonTable}")
        };
    }

    private static InvalidOperationException QueryScopedRowsUnavailable(
        SourceIdentity identity,
        string fingerprint,
        string reason)
    {
        return new InvalidOperationException(
            $"Separated-values source '{identity.SchemaName}.{identity.MethodName}' " +
            $"(context '{identity.SourceContextId}', alias '{identity.Alias}') cannot materialize " +
            $"query shape '{fingerprint}': {reason}.");
    }

    private static InvalidOperationException MandatoryQueryRowsUnavailable(
        SourceIdentity identity,
        string reason)
    {
        return new InvalidOperationException(
            $"Separated-values source '{identity.SchemaName}.{identity.MethodName}' " +
            $"(context '{identity.SourceContextId}', alias '{identity.Alias}') cannot be described " +
            $"for mandatory query-scoped row transfer: {reason}.");
    }

    private static InvalidOperationException LegacyRowTransferUnsupported(
        SourceIdentity identity,
        string sourceName,
        Type requestedRowType)
    {
        var schemaName = string.IsNullOrWhiteSpace(identity.SchemaName)
            ? SchemaName.ToLowerInvariant()
            : identity.SchemaName;
        var methodName = string.IsNullOrWhiteSpace(identity.MethodName)
            ? sourceName
            : identity.MethodName;
        return new InvalidOperationException(
            $"Separated-values source '{schemaName}.{methodName}' " +
            $"(context '{identity.SourceContextId}', alias '{identity.Alias}') cannot create legacy row type " +
            $"'{requestedRowType}': the core planner selected unsupported legacy row transfer; " +
            "SeparatedValues requires query-scoped row transfer.");
    }

    private static SourceParameters ParseParameters(string name, object?[] parameters)
    {
        if (string.Equals(name, DelimitedTable, StringComparison.OrdinalIgnoreCase))
        {
            if (parameters is not [string delimitedPath, string separator, bool delimitedHasHeader, int delimitedSkipLines])
            {
                throw new ArgumentException(
                    "The delimited source requires exactly (string path, string delimiter, bool hasHeader, int skipLines).",
                    nameof(parameters));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(separator);
            _ = SeparatedValuesFormat.GetSeparatorByte(separator);
            ArgumentException.ThrowIfNullOrWhiteSpace(delimitedPath);
            ArgumentOutOfRangeException.ThrowIfNegative(delimitedSkipLines);
            return new SourceParameters(delimitedPath, separator, delimitedHasHeader, delimitedSkipLines);
        }

        if (parameters is not [string path, bool hasHeader, int skipLines])
        {
            throw new ArgumentException(
                "Separated-values sources require exactly (string path, bool hasHeader, int skipLines).",
                nameof(parameters));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        return new SourceParameters(path, GetSeparator(name), hasHeader, skipLines);
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

    private static SchemaMethodInfo CreateDelimitedMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("path", typeof(string)),
                ("delimiter", typeof(string)),
                ("hasHeader", typeof(bool)),
                ("skipLines", typeof(int))
            ]);

        return new SchemaMethodInfo(DelimitedTable, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new SeparatedValuesLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }

    private readonly record struct SourceParameters(string Path, string Separator, bool HasHeader, int SkipLines);

    private readonly record struct ContractKey(string Path, string DialectFingerprint, bool HasHeader, int SkipLines);

    private sealed record PendingContract(ContractKey Key, SeparatedValuesSourceContract Contract);
}
