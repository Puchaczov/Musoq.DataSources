#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Musoq.DataSources.Structured;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesQueryMetadata
{
    private SeparatedValuesQueryMetadata(ImmutableArray<SeparatedValuesQueryColumn> columns)
    {
        Columns = columns;
    }

    public ImmutableArray<SeparatedValuesQueryColumn> Columns { get; }

    public static bool TryValidateDeclaredColumns(
        IReadOnlyCollection<ISchemaColumn> columns,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnName))
            {
                reason = "source metadata contains an empty column name";
                return false;
            }

            if (!names.Add(column.ColumnName))
            {
                reason = $"source metadata contains duplicate column name '{column.ColumnName}'";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(column.IntendedTypeName))
            {
                reason = $"column '{column.ColumnName}' has unresolved intended type '{column.IntendedTypeName}'";
                return false;
            }

            if (!SeparatedValuesValueConverter.TryGetExactConversion(column.ColumnType, out _))
            {
                reason = $"column '{column.ColumnName}' has unsupported exact type '{column.ColumnType}'";
                return false;
            }

            if (column.ReadModifiers.Count != 0)
            {
                reason = $"column '{column.ColumnName}' has unsupported read modifiers";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    public static bool TryCreateForDescriptor(
        SeparatedValuesSourceContract contract,
        IReadOnlyCollection<ISchemaColumn> columns,
        out SeparatedValuesQueryMetadata? metadata,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(columns);

        var ordered = columns.OrderBy(static column => column.ColumnIndex).ToArray();
        var candidates = new Candidate[ordered.Length];
        for (var denseIndex = 0; denseIndex < ordered.Length; denseIndex++)
        {
            var column = ordered[denseIndex];
            if (column.ColumnIndex != denseIndex)
            {
                metadata = null;
                reason = $"column '{column.ColumnName}' has non-dense metadata ordinal {column.ColumnIndex}; expected {denseIndex}";
                return false;
            }

            if (!TryResolveSnapshotColumn(contract.Snapshot, column.ColumnName, out var snapshotColumn))
            {
                metadata = null;
                reason = $"column '{column.ColumnName}' is absent from the immutable source snapshot";
                return false;
            }

            candidates[denseIndex] = new Candidate(
                snapshotColumn.Name,
                denseIndex,
                snapshotColumn.SourceOrdinal,
                column.ColumnType,
                column.IntendedTypeName,
                column.ReadModifiers);
        }

        return TryCreate(candidates, out metadata, out reason);
    }

    public static bool TryCreateForExecution(
        SeparatedValuesSourceContract contract,
        StructuredExecutionLayout layout,
        IReadOnlyCollection<ISchemaColumn> plannedColumns,
        out SeparatedValuesQueryMetadata? metadata,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(plannedColumns);

        var ordered = plannedColumns
            .OrderBy(static column => column.ColumnIndex)
            .ToArray();
        var candidates = new Candidate[ordered.Length];
        for (var denseIndex = 0; denseIndex < ordered.Length; denseIndex++)
        {
            var column = ordered[denseIndex];
            if (column.ColumnIndex != denseIndex)
            {
                metadata = null;
                reason = $"planned column '{column.ColumnName}' has non-dense metadata ordinal " +
                         $"{column.ColumnIndex}; expected {denseIndex}";
                return false;
            }

            var matches = layout.Bindings
                .Where(binding => NamesMatch(column.ColumnName, binding.Name) ||
                                  NamesMatch(binding.Name, column.ColumnName))
                .ToArray();
            if (matches.Length != 1)
            {
                metadata = null;
                reason = matches.Length == 0
                    ? $"planned metadata column {denseIndex} ('{column.ColumnName}') is absent from the immutable execution layout"
                    : $"planned metadata column {denseIndex} ('{column.ColumnName}') is ambiguous in the immutable execution layout";
                return false;
            }

            var binding = matches[0];
            if ((uint)binding.SourceOrdinal >= (uint)contract.Snapshot.Columns.Length ||
                !string.Equals(
                    contract.Snapshot.Columns[binding.SourceOrdinal].Name,
                    binding.Name,
                    StringComparison.Ordinal))
            {
                metadata = null;
                reason = $"planned column '{binding.Name}' no longer matches physical source ordinal {binding.SourceOrdinal}";
                return false;
            }

            candidates[denseIndex] = new Candidate(
                binding.Name,
                denseIndex,
                binding.SourceOrdinal,
                column.ColumnType,
                column.IntendedTypeName,
                column.ReadModifiers);
        }

        return TryCreate(candidates, out metadata, out reason);
    }

    private static bool TryCreate(
        IReadOnlyList<Candidate> candidates,
        out SeparatedValuesQueryMetadata? metadata,
        out string reason)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceOrdinals = new HashSet<int>();
        var columns = ImmutableArray.CreateBuilder<SeparatedValuesQueryColumn>(candidates.Count);

        for (var denseIndex = 0; denseIndex < candidates.Count; denseIndex++)
        {
            var candidate = candidates[denseIndex];
            if (candidate.DenseSourceColumnIndex != denseIndex)
            {
                metadata = null;
                reason = $"column '{candidate.Name}' has non-dense metadata ordinal {candidate.DenseSourceColumnIndex}; expected {denseIndex}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                metadata = null;
                reason = "source metadata contains an empty column name";
                return false;
            }

            if (!names.Add(candidate.Name))
            {
                metadata = null;
                reason = $"source metadata contains duplicate column name '{candidate.Name}'";
                return false;
            }

            if (!sourceOrdinals.Add(candidate.PhysicalSourceOrdinal))
            {
                metadata = null;
                reason = $"source metadata contains duplicate physical ordinal {candidate.PhysicalSourceOrdinal}";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(candidate.IntendedTypeName))
            {
                metadata = null;
                reason = $"column '{candidate.Name}' has unresolved intended type '{candidate.IntendedTypeName}'";
                return false;
            }

            if (!SeparatedValuesValueConverter.TryGetExactConversion(candidate.FieldType, out var conversion))
            {
                metadata = null;
                reason = $"column '{candidate.Name}' has unsupported exact type '{candidate.FieldType}'";
                return false;
            }

            if (candidate.ReadModifiers.Count != 0)
            {
                metadata = null;
                reason = $"column '{candidate.Name}' has unsupported read modifiers";
                return false;
            }

            columns.Add(new SeparatedValuesQueryColumn(
                candidate.Name,
                denseIndex,
                candidate.PhysicalSourceOrdinal,
                candidate.FieldType,
                IsNullable(candidate.FieldType),
                conversion,
                candidate.ReadModifiers.ToImmutableDictionary(StringComparer.Ordinal)));
        }

        metadata = new SeparatedValuesQueryMetadata(columns.MoveToImmutable());
        reason = string.Empty;
        return true;
    }

    private static bool TryResolveSnapshotColumn(
        StructuredSchemaSnapshot snapshot,
        string name,
        out StructuredColumnSnapshot column)
    {
        if (snapshot.TryGetColumn(name, out column!))
            return true;

        StructuredColumnSnapshot? match = null;
        foreach (var candidate in snapshot.Columns)
        {
            if (!NamesMatch(name, candidate.Name))
                continue;
            if (match is not null)
            {
                column = null!;
                return false;
            }

            match = candidate;
        }

        column = match!;
        return match is not null;
    }

    internal static bool NamesMatch(string candidate, string sourceName)
    {
        if (string.Equals(candidate, sourceName, StringComparison.Ordinal))
            return true;

        return candidate.Length > sourceName.Length &&
               candidate[candidate.Length - sourceName.Length - 1] == '.' &&
               candidate.EndsWith(sourceName, StringComparison.Ordinal);
    }

    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    private readonly record struct Candidate(
        string Name,
        int DenseSourceColumnIndex,
        int PhysicalSourceOrdinal,
        Type FieldType,
        string? IntendedTypeName,
        IReadOnlyDictionary<string, string> ReadModifiers);
}

internal sealed class SeparatedValuesQueryShapeMapping
{
    private SeparatedValuesQueryShapeMapping(
        string fingerprint,
        ImmutableArray<SeparatedValuesQueryFieldMapping> fields)
    {
        Fingerprint = fingerprint;
        Fields = fields;
    }

    public string Fingerprint { get; }

    public ImmutableArray<SeparatedValuesQueryFieldMapping> Fields { get; }

    public static bool TryCreate(
        SeparatedValuesSourceContract contract,
        StructuredExecutionLayout layout,
        IReadOnlyCollection<ISchemaColumn> plannedColumns,
        QueryRowShape shape,
        out SeparatedValuesQueryShapeMapping? mapping,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!SeparatedValuesQueryMetadata.TryCreateForExecution(
                contract,
                layout,
                plannedColumns,
                out var metadata,
                out reason))
        {
            mapping = null;
            return false;
        }

        var fields = ImmutableArray.CreateBuilder<SeparatedValuesQueryFieldMapping>(shape.Fields.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceIndexes = new HashSet<int>();
        foreach (var field in shape.Fields)
        {
            if (!sourceIndexes.Add(field.SourceColumnIndex))
            {
                mapping = null;
                reason = $"shape contains duplicate source column {field.SourceColumnIndex}";
                return false;
            }

            if (!names.Add(field.Name))
            {
                mapping = null;
                reason = $"shape contains duplicate field name '{field.Name}'";
                return false;
            }

            if (!TryValidateDescriptorField(contract, field, out reason))
            {
                mapping = null;
                return false;
            }

            var matches = metadata!.Columns
                .Where(column => SeparatedValuesQueryMetadata.NamesMatch(field.Name, column.Name) ||
                                 SeparatedValuesQueryMetadata.NamesMatch(column.Name, field.Name))
                .ToArray();
            if (matches.Length != 1)
            {
                mapping = null;
                reason = matches.Length == 0
                    ? $"shape field '{field.Name}' is absent from planned dense metadata"
                    : $"shape field '{field.Name}' is ambiguous in planned dense metadata";
                return false;
            }

            var column = matches[0];

            if (field.FieldType != column.FieldType)
            {
                mapping = null;
                reason = $"shape field '{field.Name}' type '{field.FieldType}' does not match planned type '{column.FieldType}'";
                return false;
            }

            if (field.IsNullable != column.IsNullable)
            {
                mapping = null;
                reason = $"shape field '{field.Name}' nullability does not match planned nullability";
                return false;
            }

            if (!ModifiersEqual(field.ReadModifiers, column.ReadModifiers))
            {
                mapping = null;
                reason = $"shape field '{field.Name}' read modifiers do not match planned metadata";
                return false;
            }

            fields.Add(new SeparatedValuesQueryFieldMapping(
                field.Slot,
                column.DenseSourceColumnIndex,
                column.PhysicalSourceOrdinal,
                column.Name,
                column.FieldType,
                column.IsNullable,
                column.Conversion));
        }

        mapping = new SeparatedValuesQueryShapeMapping(shape.Fingerprint, fields.MoveToImmutable());
        reason = string.Empty;
        return true;
    }

    private static bool TryValidateDescriptorField(
        SeparatedValuesSourceContract contract,
        QueryRowField field,
        out string reason)
    {
        if (contract.DescriptorColumns.IsEmpty)
        {
            reason = string.Empty;
            return true;
        }

        if ((uint)field.SourceColumnIndex >= (uint)contract.DescriptorColumns.Length)
        {
            reason = $"shape field '{field.Name}' references unavailable descriptor source column {field.SourceColumnIndex}";
            return false;
        }

        var descriptor = contract.DescriptorColumns[field.SourceColumnIndex];
        if (!SeparatedValuesQueryMetadata.NamesMatch(field.Name, descriptor.Name) &&
            !SeparatedValuesQueryMetadata.NamesMatch(descriptor.Name, field.Name))
        {
            reason = $"shape field '{field.Name}' does not match descriptor source column " +
                     $"{field.SourceColumnIndex} ('{descriptor.Name}')";
            return false;
        }

        if (field.FieldType != descriptor.FieldType)
        {
            reason = $"shape field '{field.Name}' type '{field.FieldType}' does not match planned type " +
                     $"'{descriptor.FieldType}'";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.IntendedTypeName))
        {
            reason = $"descriptor column '{descriptor.Name}' has unresolved intended type '{descriptor.IntendedTypeName}'";
            return false;
        }

        if (!ModifiersEqual(field.ReadModifiers, descriptor.ReadModifiers))
        {
            reason = $"shape field '{field.Name}' read modifiers do not match descriptor metadata";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool ModifiersEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) ||
                !string.Equals(pair.Value, value, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

internal readonly record struct SeparatedValuesQueryColumn(
    string Name,
    int DenseSourceColumnIndex,
    int PhysicalSourceOrdinal,
    Type FieldType,
    bool IsNullable,
    SeparatedValuesConversion Conversion,
    IReadOnlyDictionary<string, string> ReadModifiers);

internal readonly record struct SeparatedValuesQueryFieldMapping(
    int Slot,
    int DenseSourceColumnIndex,
    int PhysicalSourceOrdinal,
    string Name,
    Type FieldType,
    bool IsNullable,
    SeparatedValuesConversion Conversion);
