#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Structured;

internal sealed record StructuredColumnBinding(
    string Name,
    int SourceOrdinal,
    int OutputOrdinal,
    StructuredTypeState TypeState,
    Type? CarrierType = null,
    Type? SourceReadType = null,
    EnumTypeDescriptor? EnumType = null,
    ColumnStability Stability = ColumnStability.Stable)
{
    public Type ClrType => CarrierType ?? TypeState.ToClrType();

    public Type EffectiveSourceReadType => SourceReadType ?? ClrType;

    public bool IsNullable => !ClrType.IsValueType || Nullable.GetUnderlyingType(ClrType) is not null;
}

internal sealed class StructuredExecutionLayout
{
    private readonly ImmutableArray<string> _requestedNames;

    private StructuredExecutionLayout(
        StructuredSchemaSnapshot snapshot,
        ImmutableArray<StructuredColumnBinding> bindings,
        ImmutableArray<string> requestedNames,
        bool includesCompleteSchema)
    {
        SnapshotIdentity = snapshot.Identity;
        Bindings = bindings;
        _requestedNames = requestedNames;
        IncludesCompleteSchema = includesCompleteSchema;
    }

    public StructuredFileIdentity SnapshotIdentity { get; }

    public ImmutableArray<StructuredColumnBinding> Bindings { get; }

    public bool IncludesCompleteSchema { get; }

    public int OutputColumnCount => Bindings.Length;

    public static StructuredExecutionLayout Bind(
        StructuredSchemaSnapshot snapshot,
        IEnumerable<string>? requestedColumns,
        bool includeCompleteSchema)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var names = includeCompleteSchema
            ? snapshot.Columns.Select(column => column.Name).ToImmutableArray()
            : DistinctNames(requestedColumns ?? []);
        var bindings = ImmutableArray.CreateBuilder<StructuredColumnBinding>(names.Length);

        for (var outputOrdinal = 0; outputOrdinal < names.Length; outputOrdinal++)
        {
            var name = names[outputOrdinal];
            if (!snapshot.TryGetColumn(name, out var column))
                throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);

            bindings.Add(new StructuredColumnBinding(
                column.Name,
                column.SourceOrdinal,
                outputOrdinal,
                column.TypeState,
                column.CarrierType,
                column.SourceReadType,
                column.EnumType,
                column.Stability));
        }

        return new StructuredExecutionLayout(snapshot, bindings.MoveToImmutable(), names, includeCompleteSchema);
    }

    public void EnsureCompatibleWith(StructuredSchemaSnapshot currentSnapshot)
    {
        StructuredExecutionLayout currentLayout;
        try
        {
            currentLayout = Bind(currentSnapshot, _requestedNames, IncludesCompleteSchema);
        }
        catch (StructuredUnknownColumnException exception)
        {
            throw new StructuredSchemaDriftException(
                currentSnapshot.Identity.CanonicalPath,
                $"bound column '{exception.ColumnName}' disappeared");
        }

        if (Bindings.Length != currentLayout.Bindings.Length)
        {
            throw new StructuredSchemaDriftException(
                currentSnapshot.Identity.CanonicalPath,
                $"column count changed from {Bindings.Length} to {currentLayout.Bindings.Length}");
        }

        for (var index = 0; index < Bindings.Length; index++)
        {
            var expected = Bindings[index];
            var actual = currentLayout.Bindings[index];
            if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
            {
                throw new StructuredSchemaDriftException(
                    currentSnapshot.Identity.CanonicalPath,
                    $"column {index} changed from '{expected.Name}' to '{actual.Name}'");
            }

            if (expected.TypeState != actual.TypeState)
            {
                throw new StructuredSchemaDriftException(
                    currentSnapshot.Identity.CanonicalPath,
                    $"column '{expected.Name}' changed from {expected.TypeState} to {actual.TypeState}");
            }

            if (expected.ClrType != actual.ClrType ||
                expected.EffectiveSourceReadType != actual.EffectiveSourceReadType ||
                !Equals(expected.EnumType, actual.EnumType) ||
                expected.Stability != actual.Stability)
            {
                throw new StructuredSchemaDriftException(
                    currentSnapshot.Identity.CanonicalPath,
                    $"column '{expected.Name}' changed its logical enum/source-read contract; recompile the query");
            }
        }
    }

    private static ImmutableArray<string> DistinctNames(IEnumerable<string> names)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (seen.Add(name))
                result.Add(name);
        }

        return result.ToImmutable();
    }
}
