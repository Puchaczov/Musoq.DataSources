#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues;

internal readonly record struct SeparatedValuesStructuralBlock(
    long StartRow,
    long RowCount,
    long FirstRecordOffset,
    long LastRecordEndOffset)
{
    public long EndRow => checked(StartRow + RowCount);
}

internal sealed class SeparatedValuesStructuralSummary
{
    public SeparatedValuesStructuralSummary(
        StructuredFileIdentity identity,
        long dataStartOffset,
        long totalRows,
        IEnumerable<SeparatedValuesStructuralBlock> blocks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dataStartOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(totalRows);
        Identity = identity;
        DataStartOffset = dataStartOffset;
        TotalRows = totalRows;
        Blocks = blocks.ToImmutableArray();
        EstimatedSizeBytes = checked(512L + Blocks.Length * 64L);
        Validate();
    }

    public StructuredFileIdentity Identity { get; }

    public long DataStartOffset { get; }

    public long TotalRows { get; }

    public ImmutableArray<SeparatedValuesStructuralBlock> Blocks { get; }

    public long EstimatedSizeBytes { get; }

    public bool TryFindRow(long rowIndex, out SeparatedValuesStructuralBlock block)
    {
        if (rowIndex < 0 || rowIndex >= TotalRows)
        {
            block = default;
            return false;
        }

        var low = 0;
        var high = Blocks.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var candidate = Blocks[middle];
            if (rowIndex < candidate.StartRow)
            {
                high = middle - 1;
            }
            else if (rowIndex >= candidate.EndRow)
            {
                low = middle + 1;
            }
            else
            {
                block = candidate;
                return true;
            }
        }

        block = default;
        return false;
    }

    private void Validate()
    {
        var nextRow = 0L;
        var previousEndOffset = DataStartOffset;
        foreach (var block in Blocks)
        {
            if (block.RowCount <= 0 || block.StartRow != nextRow ||
                block.FirstRecordOffset < DataStartOffset ||
                block.FirstRecordOffset < previousEndOffset ||
                block.LastRecordEndOffset <= block.FirstRecordOffset ||
                block.LastRecordEndOffset > Identity.Length)
            {
                throw new ArgumentException("Separated-values structural blocks are not ordered and contiguous.");
            }

            nextRow = block.EndRow;
            previousEndOffset = block.LastRecordEndOffset;
        }

        if (nextRow != TotalRows)
            throw new ArgumentException("Separated-values structural blocks do not cover the exact row count.");
    }
}

internal sealed class SeparatedValuesStructuralSummaryBuilder
{
    public const int MaximumBlocksPerSummary = 16 * 1024;
    private readonly long _dataStartOffset;
    private readonly StructuredFileIdentity _identity;
    private readonly List<SeparatedValuesStructuralBlock> _blocks = [];
    private readonly long _targetBlockSize;
    private long _currentFirstOffset;
    private long _currentLastEndOffset;
    private long _currentRowCount;
    private long _currentStartRow;
    private long _currentTargetEnd;

    public SeparatedValuesStructuralSummaryBuilder(
        StructuredFileIdentity identity,
        long dataStartOffset,
        int targetBlockSize = 4 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dataStartOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetBlockSize);
        _identity = identity;
        _dataStartOffset = dataStartOffset;
        var dataLength = Math.Max(0, identity.Length - dataStartOffset);
        var boundedTarget = dataLength / MaximumBlocksPerSummary +
                            (dataLength % MaximumBlocksPerSummary == 0 ? 0 : 1);
        _targetBlockSize = Math.Max(targetBlockSize, boundedTarget);
        _currentTargetEnd = AddSaturating(dataStartOffset, _targetBlockSize);
    }

    public long TotalRows { get; private set; }

    public void ObserveRecord(long startOffset, long endOffset)
    {
        if (_currentRowCount > 0 && endOffset > _currentTargetEnd)
            CompleteCurrent();

        if (_currentRowCount == 0)
        {
            _currentStartRow = TotalRows;
            _currentFirstOffset = startOffset;
            _currentTargetEnd = AlignTarget(endOffset);
        }

        _currentLastEndOffset = endOffset;
        _currentRowCount++;
        TotalRows++;
    }

    public void ObserveRange(
        long startRow,
        long rowCount,
        long firstRecordOffset,
        long lastRecordEndOffset)
    {
        if (rowCount <= 0 || startRow != TotalRows)
            throw new ArgumentException("Separated-values structural range is not contiguous.");
        if (_currentRowCount > 0 && lastRecordEndOffset > _currentTargetEnd)
            CompleteCurrent();
        if (_currentRowCount == 0)
        {
            _currentStartRow = startRow;
            _currentFirstOffset = firstRecordOffset;
            _currentTargetEnd = AlignTarget(lastRecordEndOffset);
        }

        _currentLastEndOffset = lastRecordEndOffset;
        _currentRowCount = checked(_currentRowCount + rowCount);
        TotalRows = checked(TotalRows + rowCount);
    }

    public SeparatedValuesStructuralSummary Build()
    {
        CompleteCurrent();
        return new SeparatedValuesStructuralSummary(
            _identity,
            _dataStartOffset,
            TotalRows,
            _blocks);
    }

    private long AlignTarget(long offset)
    {
        var relative = Math.Max(0, offset - _dataStartOffset);
        var block = relative / _targetBlockSize + 1;
        return block > (long.MaxValue - _dataStartOffset) / _targetBlockSize
            ? long.MaxValue
            : _dataStartOffset + block * _targetBlockSize;
    }

    private void CompleteCurrent()
    {
        if (_currentRowCount == 0)
            return;
        _blocks.Add(new SeparatedValuesStructuralBlock(
            _currentStartRow,
            _currentRowCount,
            _currentFirstOffset,
            _currentLastEndOffset));
        _currentRowCount = 0;
    }

    private static long AddSaturating(long left, long right)
    {
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}

internal static class SeparatedValuesStructuralSummaryCache
{
    public const int MaximumEntries = 64;
    public const long MaximumRetainedBytes = 64L * 1024L * 1024L;

    private static readonly Dictionary<StructuredFileIdentity, LinkedListNode<Entry>> Entries =
        new(StructuredFileIdentityComparer.Instance);
    private static readonly LinkedList<Entry> Recency = new();
    private static readonly object Sync = new();
    private static long _retainedBytes;

    public static bool TryGet(
        StructuredFileIdentity identity,
        out SeparatedValuesStructuralSummary summary)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(identity, out var node))
            {
                summary = null!;
                return false;
            }

            Recency.Remove(node);
            Recency.AddFirst(node);
            summary = node.Value.Summary;
            return true;
        }
    }

    public static void Store(SeparatedValuesStructuralSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.EstimatedSizeBytes > MaximumRetainedBytes)
            return;

        lock (Sync)
        {
            if (Entries.Remove(summary.Identity, out var existing))
            {
                Recency.Remove(existing);
                _retainedBytes -= existing.Value.Summary.EstimatedSizeBytes;
            }

            var node = Recency.AddFirst(new Entry(summary.Identity, summary));
            Entries.Add(summary.Identity, node);
            _retainedBytes += summary.EstimatedSizeBytes;

            while (Entries.Count > MaximumEntries || _retainedBytes > MaximumRetainedBytes)
            {
                var oldest = Recency.Last!;
                Recency.RemoveLast();
                Entries.Remove(oldest.Value.Identity);
                _retainedBytes -= oldest.Value.Summary.EstimatedSizeBytes;
            }
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Entries.Clear();
            Recency.Clear();
            _retainedBytes = 0;
        }
    }

    private sealed record Entry(
        StructuredFileIdentity Identity,
        SeparatedValuesStructuralSummary Summary);
}
