#nullable enable

using System;
using System.Collections.Generic;
using System.Buffers.Text;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal sealed class JsonQueryRowProcessor<TRow, TMaterializer> : IJsonRecordProcessor
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    private readonly IChunkWriter<TRow> _writer;
    private readonly JsonPropertyLookup _lookup;
    private readonly JsonPredicateEvaluator _predicate;
    private readonly DataSourceProgressReporter? _progress;
    private readonly CancellationToken _cancellationToken;
    private readonly FieldSlice[] _fields;
    private readonly int[] _sourceOrdinals;
    private readonly StructuredStringPool _stringPool;
    private readonly bool[] _nullable;
    private readonly string[] _fieldNames;
    private readonly long? _take;
    private List<TRow>? _chunk;
    private long _skipRemaining;
    private long _emitted;

    public JsonQueryRowProcessor(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext context,
        QueryRowShape shape,
        IChunkWriter<TRow> writer,
        DataSourceProgressReporter? progress,
        CancellationToken cancellationToken)
    {
        _writer = writer;
        _progress = progress;
        _cancellationToken = cancellationToken;
        _skipRemaining = context.Plan.AcceptedSkip ?? 0;
        _take = context.Plan.AcceptedTake;
        _lookup = new JsonPropertyLookup(shape.Fields.Select(field => field.Name).ToArray());
        _fields = new FieldSlice[shape.Fields.Count];
        _sourceOrdinals = new int[shape.Fields.Count];
        _stringPool = snapshot.StringPool;
        _nullable = shape.Fields.Select(field => field.IsNullable).ToArray();
        _fieldNames = shape.Fields.Select(field => field.Name).ToArray();
        foreach (var field in shape.Fields)
        {
            if (!snapshot.TryGetColumn(field.Name, out var column))
                throw new StructuredUnknownColumnException(field.Name, snapshot.Identity.CanonicalPath);
            _sourceOrdinals[field.Slot] = column.SourceOrdinal;
            if (field.FieldType == typeof(object))
                throw new InvalidOperationException($"JSON column '{field.Name}' needs a concrete declared type.");
        }

        _predicate = JsonPredicateEvaluator.Create(snapshot, context.Plan.AcceptedPredicate,
            JsonRowLayout.Create(snapshot, context));
    }

    public long RowsRead { get; private set; }

    public bool Process(ReadOnlySpan<byte> record)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        RowsRead++;
        _progress?.RowRead();
        if (!_predicate.Matches(record, ReaderOptions))
            return true;
        if (_skipRemaining > 0)
        {
            _skipRemaining--;
            return true;
        }

        if (_take.HasValue && _emitted >= _take.Value)
            return false;

        _fields.AsSpan().Clear();
        var reader = new Utf8JsonReader(record, true, new JsonReaderState(ReaderOptions));
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A framed JSON row must be an object.");
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("A JSON object must contain property names and values.");
            var slot = _lookup.Find(ref reader);
            if (!reader.Read())
                throw new JsonException("A JSON property has no value.");
            if (slot < 0)
            {
                if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                    reader.Skip();
                continue;
            }
            var start = checked((int)reader.TokenStartIndex);
            var escaped = reader.ValueIsEscaped;
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                reader.Skip();
            _fields[slot] = new FieldSlice(start, checked((int)reader.BytesConsumed - start), escaped);
        }

        var fields = new FieldReader(record, this);
        var row = TMaterializer.Materialize(ref fields);
        _chunk ??= new List<TRow>(RowChunking.DefaultChunkSize);
        _chunk.Add(row);
        _emitted++;
        if (_chunk.Count == RowChunking.DefaultChunkSize)
            Complete();
        return !_take.HasValue || _emitted < _take.Value;
    }

    public void Complete()
    {
        if (_chunk is not { Count: > 0 })
            return;
        _writer.Write(_chunk);
        _chunk = null;
    }

    private readonly record struct FieldSlice(int Start, int Length, bool Escaped);

    private readonly ref struct FieldReader : IQuerySourceFieldReader
    {
        private readonly ReadOnlySpan<byte> _record;
        private readonly JsonQueryRowProcessor<TRow, TMaterializer> _owner;

        public FieldReader(ReadOnlySpan<byte> record, JsonQueryRowProcessor<TRow, TMaterializer> owner)
        {
            _record = record;
            _owner = owner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>(int slot)
        {
            ref readonly var field = ref _owner._fields[slot];
            if (field.Length == 0)
                return ReadNull<T>(slot);
            var json = _record.Slice(field.Start, field.Length);
            if (json.SequenceEqual("null"u8))
                return ReadNull<T>(slot);
            if (typeof(T) == typeof(string) && json[0] == (byte)'"' && !field.Escaped)
            {
                var value = _owner._stringPool.GetOrAddUtf8(_owner._sourceOrdinals[slot], json[1..^1]);
                return Unsafe.As<string?, T>(ref value);
            }
            return ReadConverted<T>(slot);
        }

        private T ReadConverted<T>(int slot)
        {
            try
            {
                return ReadValue<T>(slot);
            }
            catch (Exception exception) when (exception is JsonException or OverflowException or FormatException)
            {
                throw new FormatException($"JSON value for column '{_owner._fieldNames[slot]}' cannot be converted to {typeof(T)}.", exception);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue<T>(int slot)
        {
            ref readonly var field = ref _owner._fields[slot];
            if (field.Length == 0)
                return ReadNull<T>(slot);
            var json = _record.Slice(field.Start, field.Length);
            if (typeof(T) == typeof(string) && json[0] == (byte)'"')
            {
                var value = field.Escaped
                    ? JsonSerializer.Deserialize<string>(json)
                    : _owner._stringPool.GetOrAddUtf8(_owner._sourceOrdinals[slot], json[1..^1]);
                return Unsafe.As<string?, T>(ref value);
            }
            if (json.SequenceEqual("null"u8))
                return ReadNull<T>(slot);
            // Closed generic numeric reads are folded by the JIT and never box values.
            if (typeof(T) == typeof(long))
            {
                var value = ReadInt64(json);
                return Unsafe.As<long, T>(ref value);
            }
            if (typeof(T) == typeof(long?))
            {
                long? value = ReadInt64(json);
                return Unsafe.As<long?, T>(ref value);
            }
            if (typeof(T) == typeof(decimal))
            {
                var value = ReadDecimal(json);
                return Unsafe.As<decimal, T>(ref value);
            }
            if (typeof(T) == typeof(decimal?))
            {
                decimal? value = ReadDecimal(json);
                return Unsafe.As<decimal?, T>(ref value);
            }
            if (typeof(T) == typeof(double))
            {
                var value = ReadDouble(json);
                return Unsafe.As<double, T>(ref value);
            }
            if (typeof(T) == typeof(double?))
            {
                double? value = ReadDouble(json);
                return Unsafe.As<double?, T>(ref value);
            }
            if (typeof(T) == typeof(bool))
            {
                var value = ReadBoolean(json);
                return Unsafe.As<bool, T>(ref value);
            }
            if (typeof(T) == typeof(bool?))
            {
                bool? value = ReadBoolean(json);
                return Unsafe.As<bool?, T>(ref value);
            }
            return JsonSerializer.Deserialize<T>(json)!;
        }

        private static long ReadInt64(ReadOnlySpan<byte> json)
        {
            if (Utf8Parser.TryParse(json, out long value, out var consumed) && consumed == json.Length)
                return value;
            throw new JsonException("JSON integer cannot be represented by the declared Int64 field.");
        }

        private static decimal ReadDecimal(ReadOnlySpan<byte> json)
        {
            if (Utf8Parser.TryParse(json, out decimal value, out var consumed) && consumed == json.Length)
                return value;
            throw new JsonException("JSON number cannot be represented by the declared Decimal field.");
        }

        private static double ReadDouble(ReadOnlySpan<byte> json)
        {
            if (Utf8Parser.TryParse(json, out double value, out var consumed) && consumed == json.Length)
                return value;
            throw new JsonException("JSON number cannot be represented by the declared Double field.");
        }

        private static bool ReadBoolean(ReadOnlySpan<byte> json)
        {
            if (json.SequenceEqual("true"u8))
                return true;
            if (json.SequenceEqual("false"u8))
                return false;
            throw new JsonException("JSON value cannot be represented by the declared Boolean field.");
        }

        private T ReadNull<T>(int slot)
        {
            if (_owner._nullable[slot])
                return default!;
            throw new JsonException($"JSON source field at query slot {slot} returned null for a non-nullable declared type.");
        }
    }
}
