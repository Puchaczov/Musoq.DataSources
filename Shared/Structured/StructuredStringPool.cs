#nullable enable

using System;
using System.Text;
using System.Threading;

namespace Musoq.DataSources.Structured;

internal sealed class StructuredStringPool
{
    public const int MaximumValuesPerColumn = 4096;
    public const long MaximumRetainedBytes = 8L * 1024 * 1024;

    private readonly ColumnPool?[] _columns;
    private readonly object _sync = new();
    private int _disabled;
    private int _retainedValueCount;
    private long _retainedBytes;

    public StructuredStringPool(int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);
        _columns = new ColumnPool[columnCount];
    }

    public bool IsDisabled => Volatile.Read(ref _disabled) != 0;

    public long RetainedBytes
    {
        get
        {
            lock (_sync)
                return _retainedBytes;
        }
    }

    public int RetainedValueCount
    {
        get
        {
            lock (_sync)
                return _retainedValueCount;
        }
    }

    public void Disable()
    {
        lock (_sync)
        {
            if (_disabled == 0)
                DisableAndDiscard();
        }
    }

    public string GetOrAddUtf8(int sourceOrdinal, ReadOnlySpan<byte> utf8)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOrdinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sourceOrdinal, _columns.Length);

        if (IsDisabled)
            return Encoding.UTF8.GetString(utf8);

        var hash = StructuredUtf8Hash.Hash(utf8);
        var existingColumn = Volatile.Read(ref _columns[sourceOrdinal]);
        if (existingColumn?.TryGet(hash, utf8, out var existing) == true)
            return existing;

        var decoded = Encoding.UTF8.GetString(utf8);
        var retainedSize = EstimateRetainedSize(utf8.Length, decoded.Length);

        lock (_sync)
        {
            if (_disabled != 0)
                return decoded;

            var column = _columns[sourceOrdinal];
            if (column is null)
            {
                if (ColumnPool.EstimatedRetainedBytes > MaximumRetainedBytes - _retainedBytes)
                {
                    DisableAndDiscard();
                    return decoded;
                }

                column = new ColumnPool();
                Volatile.Write(ref _columns[sourceOrdinal], column);
                _retainedBytes += ColumnPool.EstimatedRetainedBytes;
            }

            if (column.TryGet(hash, utf8, out var cached))
                return cached;

            if (column.Count >= MaximumValuesPerColumn ||
                retainedSize > MaximumRetainedBytes - _retainedBytes)
            {
                DisableAndDiscard();
                return decoded;
            }

            column.Add(hash, utf8.ToArray(), decoded);
            _retainedBytes += retainedSize;
            _retainedValueCount++;
            return decoded;
        }
    }

    private static long EstimateRetainedSize(int utf8Length, int characterCount)
    {
        const int entryAndContainerOverhead = 96;
        return checked(entryAndContainerOverhead + utf8Length + characterCount * sizeof(char));
    }

    private void DisableAndDiscard()
    {
        Array.Clear(_columns);
        _retainedBytes = 0;
        _retainedValueCount = 0;
        Volatile.Write(ref _disabled, 1);
    }

    private sealed class ColumnPool
    {
        private const int Capacity = MaximumValuesPerColumn * 2;
        private readonly Entry?[] _entries = new Entry[Capacity];

        public const long EstimatedRetainedBytes = Capacity * sizeof(long) + 32L;

        public int Count { get; private set; }

        public bool TryGet(ulong hash, ReadOnlySpan<byte> utf8, out string value)
        {
            var slot = (int)hash & (Capacity - 1);
            while (true)
            {
                var entry = Volatile.Read(ref _entries[slot]);
                if (entry is null)
                    break;
                if (entry.Hash == hash && utf8.SequenceEqual(entry.Utf8))
                {
                    value = entry.Value;
                    return true;
                }

                slot = (slot + 1) & (Capacity - 1);
            }

            value = null!;
            return false;
        }

        public void Add(ulong hash, byte[] utf8, string value)
        {
            var slot = (int)hash & (Capacity - 1);
            while (Volatile.Read(ref _entries[slot]) is not null)
                slot = (slot + 1) & (Capacity - 1);

            Volatile.Write(ref _entries[slot], new Entry(hash, utf8, value));
            Count++;
        }
    }

    private sealed record Entry(ulong Hash, byte[] Utf8, string Value);
}

internal static class StructuredUtf8Hash
{
    public static ulong Hash(ReadOnlySpan<byte> value)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;

        foreach (var item in value)
        {
            hash ^= item;
            hash *= prime;
        }

        return hash;
    }
}
