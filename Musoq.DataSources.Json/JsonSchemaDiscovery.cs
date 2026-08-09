#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Unicode;
using System.Text.Json;
using System.Threading;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json;

internal static class JsonSchemaDiscovery
{
    private const int InitialBufferSize = 64 * 1024;
    private const int MaximumPartitionCount = 64;
    private const string ParserOptions = "json:strict-utf8:v1";
    private static readonly StructuredSnapshotCache Cache = new();

    public static StructuredSchemaSnapshot GetSnapshot(
        string path,
        CancellationToken cancellationToken = default)
    {
        return GetSnapshotWithAccess(path, cancellationToken).Snapshot;
    }

    public static StructuredSnapshotCacheResult GetSnapshotWithAccess(
        string path,
        CancellationToken cancellationToken = default)
    {
        var identity = StructuredFileIdentity.Capture(path, ParserOptions, cancellationToken);
        return Cache.GetOrCreate(
            identity,
            token => Discover(identity, token),
            cancellationToken);
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }

    private static StructuredSchemaSnapshot Discover(
        StructuredFileIdentity identity,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            identity.CanonicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            InitialBufferSize,
            FileOptions.SequentialScan);

        var scanner = new DiscoveryScanner(identity, cancellationToken);
        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var buffered = 0;
        var bufferStartOffset = 0L;
        var endOfFile = false;
        var bomChecked = false;
        var readerState = new JsonReaderState(new JsonReaderOptions
        {
            AllowMultipleValues = false,
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 0
        });

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!endOfFile && buffered < buffer.Length)
                {
                    var read = stream.Read(buffer, buffered, buffer.Length - buffered);
                    if (read == 0)
                        endOfFile = true;
                    else
                        buffered += read;
                }

                if (!bomChecked && (buffered >= 3 || endOfFile))
                {
                    bomChecked = true;
                    if (buffered >= 3 &&
                        buffer[0] == 0xef &&
                        buffer[1] == 0xbb &&
                        buffer[2] == 0xbf)
                    {
                        Buffer.BlockCopy(buffer, 3, buffer, 0, buffered - 3);
                        buffered -= 3;
                        bufferStartOffset = 3;
                    }
                }

                if (!bomChecked)
                    continue;

                var reader = new Utf8JsonReader(buffer.AsSpan(0, buffered), endOfFile, readerState);
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scanner.ProcessToken(ref reader, bufferStartOffset);
                }

                var consumed = checked((int)reader.BytesConsumed);
                readerState = reader.CurrentState;
                if (consumed > 0)
                {
                    buffered -= consumed;
                    bufferStartOffset += consumed;
                    if (buffered > 0)
                        Buffer.BlockCopy(buffer, consumed, buffer, 0, buffered);
                }

                if (endOfFile)
                {
                    if (buffered != 0)
                        throw new JsonException("JSON input contains an incomplete token.");

                    return scanner.Complete();
                }

                if (consumed != 0 || buffered != buffer.Length)
                    continue;

                var expanded = ArrayPool<byte>.Shared.Rent(checked(buffer.Length * 2));
                Buffer.BlockCopy(buffer, 0, expanded, 0, buffered);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = expanded;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed class DiscoveryScanner
    {
        private readonly StructuredSchemaBuilder _schemaBuilder =
            new(StructuredTypeConflictBehavior.WidenToObject);
        private readonly StructuredFileIdentity _identity;
        private readonly CancellationToken _cancellationToken;
        private readonly Stack<HashSet<string>?> _objectProperties = [];
        private readonly PropertyNameTable _propertyNames = new();
        private readonly PartitionAccumulator _partitions;
        private RootShape _rootShape;
        private string? _pendingColumn;
        private bool _rootComplete;
        private bool _rowActive;
        private int _rowDepth;

        public DiscoveryScanner(
            StructuredFileIdentity identity,
            CancellationToken cancellationToken)
        {
            _identity = identity;
            _cancellationToken = cancellationToken;
            _partitions = new PartitionAccumulator(identity.Length);
        }

        public void ProcessToken(ref Utf8JsonReader reader, long bufferStartOffset)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (_rootComplete)
                throw new JsonException("JSON input contains more than one root document.");

            if (_rootShape == RootShape.Unknown)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        _rootShape = RootShape.Object;
                        BeginRow(reader.CurrentDepth, bufferStartOffset + reader.TokenStartIndex);
                        PushObject(false);
                        return;
                    case JsonTokenType.StartArray:
                        _rootShape = RootShape.Array;
                        return;
                    default:
                        throw new JsonException("The JSON root must be an object or an array of objects.");
                }
            }

            if (_rootShape == RootShape.Array && !_rowActive && reader.CurrentDepth == 1)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("A JSON root array may contain objects only.");

                BeginRow(reader.CurrentDepth, bufferStartOffset + reader.TokenStartIndex);
                PushObject(false);
                return;
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    ObservePendingColumn(StructuredValueKind.Object);
                    PushObject(true);
                    break;
                case JsonTokenType.EndObject:
                {
                    var completesRow = _rowActive && reader.CurrentDepth == _rowDepth;
                    PopObject();
                    if (completesRow)
                    {
                        var endOffset = bufferStartOffset + reader.BytesConsumed;
                        _partitions.EndRow(endOffset);
                        _rowActive = false;
                        _pendingColumn = null;

                        if (_rootShape == RootShape.Object)
                            _rootComplete = true;
                    }

                    break;
                }
                case JsonTokenType.StartArray:
                    ObservePendingColumn(StructuredValueKind.Object);
                    break;
                case JsonTokenType.EndArray:
                    if (_rootShape == RootShape.Array && reader.CurrentDepth == 0)
                        _rootComplete = true;
                    break;
                case JsonTokenType.PropertyName:
                    ProcessPropertyName(ref reader);
                    break;
                case JsonTokenType.String:
                    if (!Utf8.IsValid(reader.ValueSpan))
                        throw new JsonException("JSON string contains invalid UTF-8.");
                    ObservePendingColumn(StructuredValueKind.String);
                    break;
                case JsonTokenType.Number:
                    ObservePendingColumn(GetNumberKind(ref reader));
                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    ObservePendingColumn(StructuredValueKind.Boolean);
                    break;
                case JsonTokenType.Null:
                    ObservePendingColumn(StructuredValueKind.Null);
                    break;
                case JsonTokenType.Comment:
                case JsonTokenType.None:
                    throw new JsonException($"Unsupported JSON token '{reader.TokenType}'.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(reader.TokenType), reader.TokenType, null);
            }
        }

        public StructuredSchemaSnapshot Complete()
        {
            if (_rootShape == RootShape.Unknown || !_rootComplete || _rowActive || _objectProperties.Count != 0)
                throw new JsonException("JSON input does not contain one complete root document.");

            return _schemaBuilder.Build(_identity, _partitions.Complete());
        }

        private void BeginRow(int depth, long startOffset)
        {
            _schemaBuilder.BeginRow();
            _rowDepth = depth;
            _rowActive = true;
            _pendingColumn = null;
            _partitions.BeginRow(startOffset, _schemaBuilder.RowCount - 1);
        }

        private void ProcessPropertyName(ref Utf8JsonReader reader)
        {
            if (_objectProperties.Count == 0)
                throw new JsonException("A JSON property occurred outside an object.");

            var name = _propertyNames.Resolve(ref reader);
            var seenProperties = _objectProperties.Peek();
            if (seenProperties is not null && !seenProperties.Add(name))
                throw new StructuredDuplicateFieldException(name, _schemaBuilder.RowCount - 1);

            if (_rowActive && reader.CurrentDepth == _rowDepth + 1)
                _pendingColumn = name;
        }

        private void ObservePendingColumn(StructuredValueKind kind)
        {
            if (_pendingColumn is null)
                return;

            _schemaBuilder.Observe(_pendingColumn, kind);
            _pendingColumn = null;
        }

        private void PushObject(bool trackProperties)
        {
            _objectProperties.Push(trackProperties
                ? new HashSet<string>(StringComparer.Ordinal)
                : null);
        }

        private void PopObject()
        {
            if (_objectProperties.Count == 0)
                throw new JsonException("A JSON object ended without a matching start token.");

            _objectProperties.Pop();
        }

        private static StructuredValueKind GetNumberKind(ref Utf8JsonReader reader)
        {
            var value = reader.ValueSpan;
            if (value.IndexOf((byte)'e') >= 0 || value.IndexOf((byte)'E') >= 0)
            {
                if (!reader.TryGetDouble(out var exponentValue) || !double.IsFinite(exponentValue))
                    throw new JsonException("JSON number cannot be represented as a finite double.");
                return StructuredValueKind.Double;
            }

            if (value.IndexOf((byte)'.') < 0)
            {
                if (!reader.TryGetInt64(out _))
                    throw new JsonException("JSON integer is outside the Int64 range.");
                return StructuredValueKind.Long;
            }

            if (reader.TryGetDecimal(out _))
                return StructuredValueKind.Decimal;

            if (!reader.TryGetDouble(out var floatingValue) || !double.IsFinite(floatingValue))
                throw new JsonException("JSON number cannot be represented as a finite double.");
            return StructuredValueKind.Double;
        }
    }

    private sealed class PropertyNameTable
    {
        private readonly Dictionary<string, string> _canonicalNames = new(StringComparer.Ordinal);
        private readonly Dictionary<ulong, List<NameEntry>> _unescapedNames = [];

        public string Resolve(ref Utf8JsonReader reader)
        {
            if (reader.ValueIsEscaped)
            {
                var decoded = reader.GetString()
                              ?? throw new JsonException("A JSON property name cannot be null.");
                return GetCanonical(decoded);
            }

            var bytes = reader.ValueSpan;
            if (!Utf8.IsValid(bytes))
                throw new JsonException("JSON property name contains invalid UTF-8.");

            var hash = Hash(bytes);
            if (_unescapedNames.TryGetValue(hash, out var entries))
            {
                foreach (var entry in entries)
                    if (bytes.SequenceEqual(entry.Utf8Bytes))
                        return entry.Name;
            }
            else
            {
                entries = [];
                _unescapedNames.Add(hash, entries);
            }

            var name = GetCanonical(Encoding.UTF8.GetString(bytes));
            entries.Add(new NameEntry(bytes.ToArray(), name));
            return name;
        }

        private string GetCanonical(string name)
        {
            if (_canonicalNames.TryGetValue(name, out var canonical))
                return canonical;

            _canonicalNames.Add(name, name);
            return name;
        }

        private static ulong Hash(ReadOnlySpan<byte> value)
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

        private sealed record NameEntry(byte[] Utf8Bytes, string Name);
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

    private enum RootShape : byte
    {
        Unknown,
        Object,
        Array
    }
}
