#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
    StructuredTypeState TypeState);

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
        IEnumerable<SeparatedValuesColumnContract>? columnContracts = null)
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
        ColumnContracts = (columnContracts ?? snapshot.Columns.Select(column =>
                new SeparatedValuesColumnContract(
                    column.Name,
                    column.SourceOrdinal,
                    column.ClrType,
                    column.TypeState)))
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
            ColumnContracts);
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

internal interface ISeparatedValuesScanPipeline
{
    void Execute(SeparatedValuesScanRequest request, IChunkWriter<object?[]> writer);
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
        ISeparatedValuesScanPipeline scanPipeline,
        ISeparatedValuesDialectResolver? dialectResolver = null,
        ISeparatedValuesContractStore? contractStore = null)
    {
        SchemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        ScanPipeline = scanPipeline ?? throw new ArgumentNullException(nameof(scanPipeline));
        DialectResolver = dialectResolver ?? new DefaultSeparatedValuesDialectResolver();
        ContractStore = contractStore ?? new InMemorySeparatedValuesContractStore();
    }

    public ISeparatedValuesSchemaResolver SchemaResolver { get; }

    public ISeparatedValuesScanPipeline ScanPipeline { get; }

    public ISeparatedValuesDialectResolver DialectResolver { get; }

    public ISeparatedValuesContractStore ContractStore { get; }
}
