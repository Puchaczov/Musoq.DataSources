#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal enum SeparatedValuesSchemaResolutionMode : byte
{
    Declared,
    Sampled
}

/// <summary>
/// Immutable binding information shared by planning and execution.
/// </summary>
internal sealed record SeparatedValuesColumnContract(
    string Name,
    int SourceOrdinal,
    Type ClrType,
    StructuredTypeState TypeState,
    Type? SourceReadType = null,
    EnumTypeDescriptor? EnumType = null,
    ColumnStability Stability = ColumnStability.Stable,
    SeparatedValuesEnumPlan? EnumPlan = null)
{
    public Type EffectiveSourceReadType => SourceReadType ?? ClrType;

    public bool IsNullable => !ClrType.IsValueType || Nullable.GetUnderlyingType(ClrType) is not null;
}

internal sealed class SeparatedValuesSourceContract
{
    public const string PropertyName = "SeparatedValuesSourceContract";

    public SeparatedValuesSourceContract(
        StructuredSchemaSnapshot snapshot,
        SeparatedValuesSchemaResolutionMode mode,
        bool hasExactCardinality,
        long inspectedRows,
        long inspectedBytes,
        TimeSpan elapsed,
        long dataStartOffset = 0,
        IEnumerable<Type>? columnTypes = null,
        SeparatedValuesStructuralSummary? structuralSummary = null,
        SeparatedValuesDialect? dialect = null,
        IEnumerable<SeparatedValuesColumnContract>? columnContracts = null,
        IEnumerable<SeparatedValuesDescriptorColumn>? descriptorColumns = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(inspectedRows);
        ArgumentOutOfRangeException.ThrowIfNegative(inspectedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(dataStartOffset);

        Snapshot = snapshot;
        Mode = mode;
        HasExactCardinality = hasExactCardinality;
        InspectedRows = inspectedRows;
        InspectedBytes = inspectedBytes;
        Elapsed = elapsed;
        DataStartOffset = dataStartOffset;
        ColumnTypes = (columnTypes ?? snapshot.Columns.Select(column => column.ClrType)).ToImmutableArray();
        if (ColumnTypes.Length != snapshot.Columns.Length)
            throw new ArgumentException("Separated-values contract types must match its columns.", nameof(columnTypes));
        StructuralSummary = structuralSummary;
        Dialect = dialect ?? SeparatedValuesDialect.Strict((byte)',');
        ColumnContracts = (columnContracts ?? snapshot.Columns.Select((column, index) =>
                new SeparatedValuesColumnContract(
                    column.Name,
                    column.SourceOrdinal,
                    ColumnTypes[index],
                    column.TypeState,
                    column.EffectiveSourceReadType,
                    column.EnumType,
                    column.Stability,
                    column.EnumType is null
                        ? null
                        : SeparatedValuesEnumPlan.Create(column.SourceOrdinal, ColumnTypes[index], column.EnumType))))
            .ToImmutableArray();
        if (ColumnContracts.Length != snapshot.Columns.Length)
            throw new ArgumentException("Separated-values column contracts must match its columns.", nameof(columnContracts));
        for (var index = 0; index < ColumnContracts.Length; index++)
        {
            var column = ColumnContracts[index];
            if (column.SourceOrdinal != index ||
                !string.Equals(column.Name, snapshot.Columns[index].Name, StringComparison.Ordinal))
                throw new ArgumentException("Separated-values column contracts must follow snapshot order.", nameof(columnContracts));
        }
        DescriptorColumns = (descriptorColumns ?? []).ToImmutableArray();
        Diagnostics =
        [
            new OptimizationDiagnostic(
                OptimizationDiagnosticSeverity.Info,
                $"Separated-values schema resolution mode={mode}; inspectedRows={inspectedRows:N0}; " +
                $"inspectedBytes={inspectedBytes:N0}; elapsedMs={elapsed.TotalMilliseconds:F3}; " +
                $"exactCardinality={hasExactCardinality}; dialect={Dialect.Fingerprint}; " +
                $"columns={snapshot.Columns.Length:N0}.")
        ];
    }

    public StructuredSchemaSnapshot Snapshot { get; }

    public SeparatedValuesSchemaResolutionMode Mode { get; }

    public bool HasExactCardinality { get; }

    public long InspectedRows { get; }

    public long InspectedBytes { get; }

    public TimeSpan Elapsed { get; }

    public long DataStartOffset { get; }

    public ImmutableArray<Type> ColumnTypes { get; }

    public SeparatedValuesDialect Dialect { get; }

    public ImmutableArray<SeparatedValuesColumnContract> ColumnContracts { get; }

    public ImmutableArray<SeparatedValuesDescriptorColumn> DescriptorColumns { get; }

    public IReadOnlyList<OptimizationDiagnostic> Diagnostics { get; }

    public SeparatedValuesStructuralSummary? StructuralSummary { get; }

    public SeparatedValuesSourceContract WithStructuralSummary(SeparatedValuesStructuralSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!StructuredFileIdentityComparer.Instance.Equals(Snapshot.Identity, summary.Identity))
            throw new ArgumentException("Structural summary identity does not match the source contract.", nameof(summary));
        if (summary.DataStartOffset != DataStartOffset)
            throw new ArgumentException("Structural summary data offset does not match the source contract.", nameof(summary));

        var snapshot = new StructuredSchemaSnapshot(
            Snapshot.Identity,
            Snapshot.Columns,
            summary.TotalRows);
        return new SeparatedValuesSourceContract(
            snapshot,
            Mode,
            true,
            InspectedRows,
            InspectedBytes,
            Elapsed,
            DataStartOffset,
            ColumnTypes,
            summary,
            Dialect,
            ColumnContracts,
            DescriptorColumns);
    }

    public SeparatedValuesSourceContract WithDescriptorColumns(IReadOnlyCollection<ISchemaColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var ordered = columns.OrderBy(static column => column.ColumnIndex).ToArray();
        var descriptors = new SeparatedValuesDescriptorColumn[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            var column = ordered[index];
            if (column.ColumnIndex != index)
            {
                throw new ArgumentException(
                    $"Descriptor column '{column.ColumnName}' has ordinal {column.ColumnIndex}; expected {index}.",
                    nameof(columns));
            }

            descriptors[index] = new SeparatedValuesDescriptorColumn(
                column.ColumnName,
                column.ColumnIndex,
                column.ColumnType,
                column.IntendedTypeName,
                column.ReadModifiers.ToImmutableDictionary(StringComparer.Ordinal),
                column.EnumType is null ? column.SourceReadType : column.ColumnType,
                column.EnumType,
                column.Stability);
        }

        return new SeparatedValuesSourceContract(
            Snapshot,
            Mode,
            HasExactCardinality,
            InspectedRows,
            InspectedBytes,
            Elapsed,
            DataStartOffset,
            ColumnTypes,
            StructuralSummary,
            Dialect,
            ColumnContracts,
            descriptors);
    }

    public static SeparatedValuesSourceContract From(SourceExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Properties is not null &&
            plan.Properties.TryGetValue(PropertyName, out var value) &&
            value is SeparatedValuesSourceContract contract)
            return contract;

        throw new InvalidOperationException("The separated-values execution plan does not contain a source contract.");
    }
}

internal readonly record struct SeparatedValuesDescriptorColumn(
    string Name,
    int SourceColumnIndex,
    Type FieldType,
    string? IntendedTypeName,
    IReadOnlyDictionary<string, string> ReadModifiers,
    Type? SourceReadType = null,
    EnumTypeDescriptor? EnumType = null,
    ColumnStability Stability = ColumnStability.Stable)
{
    public Type EffectiveSourceReadType => SourceReadType ?? FieldType;
}

internal readonly record struct SeparatedValuesSchemaResolutionRequest(
    string Path,
    string Separator,
    bool HasHeader,
    int SkipLines,
    IReadOnlyCollection<ISchemaColumn> DeclaredColumns,
    IReadOnlyDictionary<string, string> RuntimeSettings,
    CancellationToken CancellationToken,
    SeparatedValuesDialect? Dialect = null);

internal interface ISeparatedValuesSchemaResolver
{
    SeparatedValuesSourceContract Resolve(SeparatedValuesSchemaResolutionRequest request);
}

internal readonly record struct SeparatedValuesScanRequest(
    string Path,
    string Separator,
    byte SeparatorByte,
    bool HasHeader,
    int SkipLines,
    SourceExecutionContext ExecutionContext,
    SeparatedValuesDialect? Dialect = null);

internal interface ISeparatedValuesQueryScanPipeline
{
    void Execute<TRow, TMaterializer>(
        SeparatedValuesScanRequest request,
        QueryRowShape shape,
        IChunkWriter<TRow> writer)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>;
}

internal interface ISeparatedValuesParallelQueryScanPipeline
{
    long Execute<TRow, TMaterializer>(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        SeparatedValuesQueryShapeMapping mapping,
        QueryRowShape shape,
        IChunkWriter<TRow> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        int workerCount,
        CancellationToken cancellationToken)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>;
}

internal interface ISeparatedValuesDialectResolver
{
    SeparatedValuesDialect Resolve(
        string separator,
        IReadOnlyDictionary<string, string> runtimeSettings);
}

internal interface ISeparatedValuesContractStore
{
    bool TryGet(SeparatedValuesContractKey key, out SeparatedValuesSourceContract contract);

    void Store(SeparatedValuesContractKey key, SeparatedValuesSourceContract contract);
}

internal readonly record struct SeparatedValuesContractKey(
    string Path,
    string DialectFingerprint,
    bool HasHeader,
    int SkipLines);

internal sealed class DefaultSeparatedValuesDialectResolver : ISeparatedValuesDialectResolver
{
    public SeparatedValuesDialect Resolve(
        string separator,
        IReadOnlyDictionary<string, string> runtimeSettings)
    {
        ArgumentNullException.ThrowIfNull(runtimeSettings);
        return SeparatedValuesDialect.FromRuntimeSettings(
            SeparatedValuesFormat.GetSeparatorByte(separator),
            runtimeSettings);
    }
}

internal sealed class InMemorySeparatedValuesContractStore : ISeparatedValuesContractStore
{
    private readonly object _gate = new();
    private readonly Dictionary<SeparatedValuesContractKey, SeparatedValuesSourceContract> _contracts = [];

    public bool TryGet(SeparatedValuesContractKey key, out SeparatedValuesSourceContract contract)
    {
        lock (_gate)
            return _contracts.TryGetValue(key, out contract!);
    }

    public void Store(SeparatedValuesContractKey key, SeparatedValuesSourceContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        lock (_gate)
            _contracts[key] = contract;
    }
}

internal sealed class SeparatedValuesPipelineModules
{
    public static SeparatedValuesPipelineModules Default { get; } = new(
        new BoundedSeparatedValuesSchemaResolver(),
        new SeparatedValuesScanPipeline(),
        new DefaultSeparatedValuesDialectResolver(),
        new InMemorySeparatedValuesContractStore());

    public SeparatedValuesPipelineModules(
        ISeparatedValuesSchemaResolver schemaResolver,
        ISeparatedValuesQueryScanPipeline scanPipeline,
        ISeparatedValuesDialectResolver? dialectResolver = null,
        ISeparatedValuesContractStore? contractStore = null)
    {
        SchemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        ScanPipeline = scanPipeline ?? throw new ArgumentNullException(nameof(scanPipeline));
        DialectResolver = dialectResolver ?? new DefaultSeparatedValuesDialectResolver();
        ContractStore = contractStore ?? new InMemorySeparatedValuesContractStore();
    }

    public ISeparatedValuesSchemaResolver SchemaResolver { get; }

    public ISeparatedValuesQueryScanPipeline ScanPipeline { get; }

    public ISeparatedValuesDialectResolver DialectResolver { get; }

    public ISeparatedValuesContractStore ContractStore { get; }
}
