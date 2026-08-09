#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Musoq.DataSources.Structured;

internal sealed record StructuredColumnSnapshot(
    string Name,
    int SourceOrdinal,
    StructuredTypeState TypeState,
    long PresentValueCount)
{
    public Type ClrType => TypeState.ToClrType();
}

internal readonly record struct StructuredPartition(
    long StartOffset,
    long EndOffset,
    long StartRow,
    long RowCount)
{
    public long Length => EndOffset - StartOffset;
}

internal sealed class StructuredSchemaSnapshot
{
    private readonly FrozenDictionary<string, StructuredColumnSnapshot> _columnsByName;

    public StructuredSchemaSnapshot(
        StructuredFileIdentity identity,
        IEnumerable<StructuredColumnSnapshot> columns,
        long rowCount,
        IEnumerable<StructuredPartition>? partitions = null,
        long? estimatedSizeBytes = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);

        Identity = identity;
        Columns = columns.ToImmutableArray();
        Partitions = partitions?.ToImmutableArray() ?? [];
        RowCount = rowCount;

        ValidateColumns(Columns, rowCount);
        ValidatePartitions(Partitions, identity.Length, rowCount);

        _columnsByName = Columns.ToFrozenDictionary(column => column.Name, StringComparer.Ordinal);
        StringPool = new StructuredStringPool(Columns.Length);
        EstimatedSizeBytes = estimatedSizeBytes ?? EstimateSize(Columns, Partitions);
    }

    public StructuredFileIdentity Identity { get; }

    public ImmutableArray<StructuredColumnSnapshot> Columns { get; }

    public ImmutableArray<StructuredPartition> Partitions { get; }

    public long RowCount { get; }

    public StructuredStringPool StringPool { get; }

    public long EstimatedSizeBytes { get; }

    public bool TryGetColumn(string name, out StructuredColumnSnapshot column)
    {
        return _columnsByName.TryGetValue(name, out column!);
    }

    private static void ValidateColumns(ImmutableArray<StructuredColumnSnapshot> columns, long rowCount)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < columns.Length; index++)
        {
            var column = columns[index];
            if (column.Name is null)
                throw new ArgumentException("Structured column names cannot be null.", nameof(columns));
            if (column.SourceOrdinal != index)
                throw new ArgumentException("Structured source ordinals must be dense and ordered.", nameof(columns));
            if (column.PresentValueCount < 0 || column.PresentValueCount > rowCount)
                throw new ArgumentOutOfRangeException(nameof(columns), "Column presence count is outside the row count.");
            if (!names.Add(column.Name))
                throw new ArgumentException($"Duplicate structured column '{column.Name}'.", nameof(columns));
        }
    }

    private static void ValidatePartitions(
        ImmutableArray<StructuredPartition> partitions,
        long fileLength,
        long rowCount)
    {
        long previousEnd = 0;
        long previousRowEnd = 0;

        foreach (var partition in partitions)
        {
            if (partition.StartOffset < previousEnd || partition.EndOffset < partition.StartOffset ||
                partition.EndOffset > fileLength)
                throw new ArgumentException("Structured partitions contain invalid or overlapping offsets.", nameof(partitions));
            if (partition.StartRow < previousRowEnd || partition.RowCount < 0 ||
                partition.StartRow + partition.RowCount > rowCount)
                throw new ArgumentException("Structured partitions contain invalid or overlapping row ranges.", nameof(partitions));

            previousEnd = partition.EndOffset;
            previousRowEnd = partition.StartRow + partition.RowCount;
        }
    }

    private static long EstimateSize(
        ImmutableArray<StructuredColumnSnapshot> columns,
        ImmutableArray<StructuredPartition> partitions)
    {
        long size = 512;
        foreach (var column in columns)
            size = checked(size + 96 + column.Name.Length * sizeof(char));
        var stringPoolBudget = columns.IsEmpty ? 0 : StructuredStringPool.MaximumRetainedBytes;
        return checked(size + partitions.Length * 48L + stringPoolBudget);
    }
}
