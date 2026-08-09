#nullable enable

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesSchemaDiscovery
{
    private const int MaximumPartitionCount = 64;
    private static readonly StructuredSnapshotCache Cache = new();

    public static StructuredSchemaSnapshot GetSnapshot(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        CancellationToken cancellationToken = default)
    {
        return GetSnapshotWithAccess(path, separator, hasHeader, skipLines, cancellationToken).Snapshot;
    }

    public static StructuredSnapshotCacheResult GetSnapshotWithAccess(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        CancellationToken cancellationToken = default)
    {
        var separatorByte = GetSeparatorByte(separator);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        var parserOptions = FormattableString.Invariant(
            $"separated-values:strict-utf8:v1;separator={separatorByte};header={hasHeader};skip-lines={skipLines}");
        var identity = StructuredFileIdentity.Capture(path, parserOptions, cancellationToken);

        return Cache.GetOrCreate(
            identity,
            token => Discover(identity, separatorByte, hasHeader, skipLines, token),
            cancellationToken);
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }

    private static StructuredSchemaSnapshot Discover(
        StructuredFileIdentity identity,
        byte separator,
        bool hasHeader,
        int skipLines,
        CancellationToken cancellationToken)
    {
        using var reader = new SeparatedValuesUtf8Reader(
            identity.CanonicalPath,
            separator,
            skipLines,
            cancellationToken);
        var builder = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToString);
        var partitions = new PartitionAccumulator(identity.Length);
        var names = hasHeader
            ? ReadHeader(reader, builder)
            : new List<string>();

        while (reader.TryRead(out var record))
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.BeginRow();
            partitions.BeginRow(record.StartOffset, builder.RowCount - 1);
            var fieldIndex = 0;

            foreach (var field in record)
            {
                if (hasHeader && fieldIndex >= names.Count)
                {
                    throw new InvalidDataException(
                        $"Separated-values row {builder.RowCount:N0} has more fields than its {names.Count:N0}-column header.");
                }

                if (!hasHeader && fieldIndex == names.Count)
                    names.Add(string.Format(CultureInfo.InvariantCulture, SeparatedValuesHelper.AutoColumnName, fieldIndex + 1));

                builder.Observe(names[fieldIndex], Infer(field));
                fieldIndex++;
            }

            partitions.EndRow(record.EndOffset);
        }

        var snapshot = builder.Build(identity, partitions.Complete());
        return NormalizeUnresolvedColumns(snapshot);
    }

    private static List<string> ReadHeader(
        SeparatedValuesUtf8Reader reader,
        StructuredSchemaBuilder builder)
    {
        if (!reader.TryRead(out var header))
            throw new InvalidDataException("A headered separated-values source must contain a non-empty header record.");

        var names = new List<string>();
        var uniqueNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in header)
        {
            var name = field.Decode();
            if (name.Length == 0)
                throw new InvalidDataException("Separated-values headers cannot be empty.");
            if (!uniqueNames.Add(name))
                throw new InvalidDataException($"Separated-values header '{name}' occurs more than once.");

            names.Add(name);
            builder.DeclareColumn(name);
        }

        if (names.Count == 0)
            throw new InvalidDataException("A headered separated-values source must contain at least one column.");

        return names;
    }

    private static StructuredValueKind Infer(SeparatedValuesUtf8Field field)
    {
        var value = field.EncodedValue;
        if (value.IsEmpty)
            return field.WasQuoted ? StructuredValueKind.String : StructuredValueKind.Null;

        if (field.NeedsUnescaping)
            return StructuredValueKind.String;

        if (IsBoolean(value))
            return StructuredValueKind.Boolean;

        if (value.IndexOfAny((byte)'e', (byte)'E') >= 0)
        {
            return Utf8Parser.TryParse(value, out double exponent, out var consumed) &&
                   consumed == value.Length &&
                   double.IsFinite(exponent)
                ? StructuredValueKind.Double
                : StructuredValueKind.String;
        }

        if (value.IndexOf((byte)'.') >= 0)
        {
            if (SeparatedValuesDecimalParser.TryParse(value, out _))
                return StructuredValueKind.Decimal;

            return Utf8Parser.TryParse(value, out double fraction, out var doubleConsumed) &&
                   doubleConsumed == value.Length &&
                   double.IsFinite(fraction)
                ? StructuredValueKind.Double
                : StructuredValueKind.String;
        }

        return Utf8Parser.TryParse(value, out long _, out var integerConsumed) && integerConsumed == value.Length
            ? StructuredValueKind.Long
            : StructuredValueKind.String;
    }

    private static bool IsBoolean(ReadOnlySpan<byte> value)
    {
        return value.Length switch
        {
            4 => ToLowerAscii(value[0]) == (byte)'t' &&
                 ToLowerAscii(value[1]) == (byte)'r' &&
                 ToLowerAscii(value[2]) == (byte)'u' &&
                 ToLowerAscii(value[3]) == (byte)'e',
            5 => ToLowerAscii(value[0]) == (byte)'f' &&
                 ToLowerAscii(value[1]) == (byte)'a' &&
                 ToLowerAscii(value[2]) == (byte)'l' &&
                 ToLowerAscii(value[3]) == (byte)'s' &&
                 ToLowerAscii(value[4]) == (byte)'e',
            _ => false
        };
    }

    private static byte ToLowerAscii(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + ((byte)'a' - (byte)'A'))
            : value;
    }

    private static StructuredSchemaSnapshot NormalizeUnresolvedColumns(StructuredSchemaSnapshot snapshot)
    {
        if (snapshot.Columns.All(column => column.TypeState.Kind != StructuredValueKind.Unknown))
            return snapshot;

        return new StructuredSchemaSnapshot(
            snapshot.Identity,
            snapshot.Columns.Select(column => column.TypeState.Kind == StructuredValueKind.Unknown
                ? column with
                {
                    TypeState = new StructuredTypeState(StructuredValueKind.String, column.TypeState.IsNullable)
                }
                : column),
            snapshot.RowCount,
            snapshot.Partitions);
    }

    private static byte GetSeparatorByte(string separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        if (separator.Length != 1 || separator[0] > 0x7f)
            throw new ArgumentException("The separated-values delimiter must be one ASCII character.", nameof(separator));

        var value = checked((byte)separator[0]);
        if (value is (byte)'"' or (byte)'\r' or (byte)'\n')
            throw new ArgumentException("The configured separated-values delimiter is not supported.", nameof(separator));
        return value;
    }

    private sealed class PartitionAccumulator
    {
        private readonly List<StructuredPartition> _partitions = [];
        private readonly long _targetBytes;
        private long _currentEnd;
        private long _currentRowCount;
        private long _currentStart;
        private long _currentStartRow;
        private long _nextTarget;

        public PartitionAccumulator(long fileLength)
        {
            var desiredCount = Math.Clamp(Environment.ProcessorCount * 2, 1, MaximumPartitionCount);
            _targetBytes = Math.Max(1, fileLength / desiredCount);
            _nextTarget = _targetBytes;
        }

        public void BeginRow(long startOffset, long rowIndex)
        {
            if (_currentRowCount > 0 &&
                startOffset >= _nextTarget &&
                _partitions.Count < MaximumPartitionCount - 1)
            {
                CompleteCurrent();
                _nextTarget = checked((_partitions.Count + 1L) * _targetBytes);
            }

            if (_currentRowCount != 0)
                return;

            _currentStart = startOffset;
            _currentStartRow = rowIndex;
        }

        public void EndRow(long endOffset)
        {
            _currentEnd = endOffset;
            _currentRowCount++;
        }

        public IReadOnlyList<StructuredPartition> Complete()
        {
            CompleteCurrent();
            return _partitions;
        }

        private void CompleteCurrent()
        {
            if (_currentRowCount == 0)
                return;

            _partitions.Add(new StructuredPartition(
                _currentStart,
                _currentEnd,
                _currentStartRow,
                _currentRowCount));
            _currentRowCount = 0;
        }
    }
}
