using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal sealed class JsonRowProcessor
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowMultipleValues = false,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 0
    };

    private readonly CancellationToken _cancellationToken;
    private List<object[]> _chunk;
    private readonly IChunkWriter<object[]> _writer;
    private readonly JsonRowLayout _layout;
    private readonly JsonPredicateEvaluator _predicate;
    private readonly DataSourceProgressReporter _progress;
    private readonly long? _take;
    private long _emittedRows;
    private long _skipRemaining;
    private int _zeroColumnRows;

    public JsonRowProcessor(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext executionContext,
        IChunkWriter<object[]> writer,
        DataSourceProgressReporter progress,
        CancellationToken cancellationToken)
    {
        _writer = writer;
        _progress = progress;
        _cancellationToken = cancellationToken;
        _skipRemaining = executionContext.Plan.AcceptedSkip ?? 0;
        _take = executionContext.Plan.AcceptedTake;
        _layout = JsonRowLayout.Create(snapshot, executionContext);
        _predicate = JsonPredicateEvaluator.Create(
            snapshot,
            executionContext.Plan.AcceptedPredicate,
            _layout);
    }

    public long RowsRead { get; private set; }

    public long RowsEmitted => _emittedRows;

    public bool HasWork => _take is null || _take.Value > 0;

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

        if (_take is not null && _emittedRows >= _take.Value)
            return false;

        if (_layout.HasOutputColumns)
        {
            _chunk ??= new List<object[]>(RowChunking.DefaultChunkSize);
            _chunk.Add(_layout.Materialize(record, ReaderOptions));
        }
        else
        {
            _zeroColumnRows++;
        }

        _emittedRows++;

        if (_zeroColumnRows == RowChunking.DefaultChunkSize ||
            _chunk?.Count == RowChunking.DefaultChunkSize)
            Flush();

        return _take is null || _emittedRows < _take.Value;
    }

    public void Complete()
    {
        Flush();
    }

    private void Flush()
    {
        if (_zeroColumnRows > 0)
        {
            _writer.Write(new RepeatedValueChunk<object[]>(Array.Empty<object>(), _zeroColumnRows));
            _zeroColumnRows = 0;
        }

        if (_chunk is null || _chunk.Count == 0)
            return;

        _writer.Write(_chunk);
        _chunk = null;
    }
}

internal sealed class JsonRowLayout
{
    private readonly BoundColumn[] _columns;
    private readonly int _outputCount;
    private readonly JsonPropertyLookup _propertyLookup;
    private readonly Dictionary<string, int> _slotsByName;
    private readonly StructuredStringPool _stringPool;

    private JsonRowLayout(
        BoundColumn[] columns,
        int outputCount,
        StructuredStringPool stringPool)
    {
        _columns = columns;
        _outputCount = outputCount;
        _stringPool = stringPool;
        _propertyLookup = new JsonPropertyLookup(columns.Select(column => column.Name).ToArray());
        _slotsByName = new Dictionary<string, int>(columns.Length, StringComparer.Ordinal);
        for (var index = 0; index < columns.Length; index++)
            _slotsByName.Add(columns[index].Name, index);
    }

    public static JsonRowLayout Create(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext executionContext)
    {
        var columns = new List<BoundColumn>();
        var outputCount = 0;
        var projectionAccepted = JsonSourcePlanner.IsProjectionAccepted(executionContext.Plan) ||
                                 executionContext.Plan.AcceptedColumns.Count > 0;

        if (projectionAccepted)
        {
            var denseFallback = 0;
            foreach (var accepted in executionContext.Plan.AcceptedColumns)
            {
                var name = ResolveName(snapshot, accepted.Name);
                var schemaColumn = FindSchemaColumn(executionContext.AllColumns, name);
                var outputIndex = schemaColumn?.ColumnIndex ?? denseFallback;
                var outputType = schemaColumn?.ColumnType ?? GetSnapshotColumn(snapshot, name).ClrType;
                AddOrUpdate(columns, snapshot, name, outputIndex, outputType);
                outputCount = Math.Max(outputCount, outputIndex + 1);
                denseFallback++;
            }
        }
        else if (executionContext.AllColumns.Count > 0)
        {
            foreach (var schemaColumn in executionContext.AllColumns.OrderBy(column => column.ColumnIndex))
            {
                var name = ResolveName(snapshot, schemaColumn.ColumnName);
                AddOrUpdate(columns, snapshot, name, schemaColumn.ColumnIndex, schemaColumn.ColumnType);
                outputCount = Math.Max(outputCount, schemaColumn.ColumnIndex + 1);
            }
        }
        else
        {
            foreach (var column in snapshot.Columns)
            {
                AddOrUpdate(columns, snapshot, column.Name, column.SourceOrdinal, column.ClrType);
                outputCount++;
            }
        }

        AddPredicateColumns(columns, snapshot, executionContext.Plan.AcceptedPredicate);
        return new JsonRowLayout(columns.ToArray(), outputCount, snapshot.StringPool);
    }

    public int GetSlot(string name)
    {
        return _slotsByName[name];
    }

    public bool HasOutputColumns => _outputCount > 0;

    public object[] Materialize(ReadOnlySpan<byte> record, JsonReaderOptions options)
    {
        if (_outputCount == 0)
            return Array.Empty<object>();

        var output = new object[_outputCount];
        var reader = new Utf8JsonReader(record, true, new JsonReaderState(options));
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A framed JSON row must be an object.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return output;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("A JSON object must contain property names and values.");

            var slot = _propertyLookup.Find(ref reader);
            if (!reader.Read())
                throw new JsonException("A JSON property has no value.");

            if (slot < 0 || _columns[slot].OutputIndex < 0)
            {
                SkipValue(ref reader);
                continue;
            }

            var binding = _columns[slot];
            var value = reader.TokenType == JsonTokenType.String && !reader.ValueIsEscaped
                ? _stringPool.GetOrAddUtf8(binding.SourceOrdinal, reader.ValueSpan)
                : MaterializeValue(ref reader, binding.TypeState.Kind);
            output[binding.OutputIndex] = ConvertValue(value, binding.OutputType, binding.Name);
        }

        throw new JsonException("A framed JSON row is incomplete.");
    }

    private static object MaterializeValue(ref Utf8JsonReader reader, StructuredValueKind expectedKind)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var values = new List<object>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                values.Add(MaterializeValue(ref reader, StructuredValueKind.Object));
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("JSON array is incomplete.");
            return values;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("A nested JSON object must contain property names and values.");
                var name = reader.GetString()
                           ?? throw new JsonException("A JSON property name cannot be null.");
                if (!reader.Read())
                    throw new JsonException("A nested JSON property has no value.");
                if (!values.TryAdd(name, MaterializeValue(ref reader, StructuredValueKind.Object)))
                    throw new JsonException($"Duplicate JSON property '{name}'.");
            }

            if (reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("Nested JSON object is incomplete.");
            return values;
        }

        return expectedKind switch
        {
            StructuredValueKind.Boolean => reader.GetBoolean(),
            StructuredValueKind.Long => reader.GetInt64(),
            StructuredValueKind.Decimal => reader.GetDecimal(),
            StructuredValueKind.Double => reader.GetDouble(),
            StructuredValueKind.String => reader.GetString(),
            StructuredValueKind.Object => MaterializeNaturalScalar(ref reader),
            _ => MaterializeNaturalScalar(ref reader)
        };
    }

    private static object MaterializeNaturalScalar(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => MaterializeNaturalNumber(ref reader),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unsupported JSON scalar token '{reader.TokenType}'.")
        };
    }

    private static object MaterializeNaturalNumber(ref Utf8JsonReader reader)
    {
        var value = reader.ValueSpan;
        if (value.IndexOf((byte)'e') >= 0 || value.IndexOf((byte)'E') >= 0)
            return reader.GetDouble();
        if (value.IndexOf((byte)'.') < 0)
            return reader.GetInt64();
        return reader.TryGetDecimal(out var decimalValue) ? decimalValue : reader.GetDouble();
    }

    private static object ConvertValue(object value, Type outputType, string columnName)
    {
        if (value is null || outputType == typeof(object) || outputType.IsInstanceOfType(value))
            return value;

        var targetType = Nullable.GetUnderlyingType(outputType) ?? outputType;
        try
        {
            if (targetType == typeof(string))
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool))
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw new FormatException(
                $"JSON value for column '{columnName}' cannot be converted to {outputType}.",
                exception);
        }

        throw new FormatException($"JSON value for column '{columnName}' cannot be converted to {outputType}.");
    }

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            reader.Skip();
    }

    private static void AddPredicateColumns(
        List<BoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                AddPredicateColumns(columns, snapshot, logical.Left);
                AddPredicateColumns(columns, snapshot, logical.Right);
                return;
            case SourcePredicateComparison comparison when
                JsonSourcePlanner.TryGetComparisonParts(comparison, out var name, out _, out _):
                name = ResolveName(snapshot, name);
                AddOrUpdate(columns, snapshot, name, -1, GetSnapshotColumn(snapshot, name).ClrType);
                return;
        }
    }

    private static void AddOrUpdate(
        List<BoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        string name,
        int outputIndex,
        Type outputType)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (!string.Equals(columns[index].Name, name, StringComparison.Ordinal))
                continue;

            if (outputIndex >= 0)
                columns[index] = columns[index] with { OutputIndex = outputIndex, OutputType = outputType };
            return;
        }

        var column = GetSnapshotColumn(snapshot, name);
        columns.Add(new BoundColumn(
            name,
            column.SourceOrdinal,
            outputIndex,
            outputType,
            column.TypeState));
    }

    private static StructuredColumnSnapshot GetSnapshotColumn(
        StructuredSchemaSnapshot snapshot,
        string name)
    {
        if (snapshot.TryGetColumn(name, out var column))
            return column;
        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
    }

    private static string ResolveName(StructuredSchemaSnapshot snapshot, string name)
    {
        if (snapshot.TryGetColumn(name, out _))
            return name;

        var dotIndex = name.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            var unqualified = name[(dotIndex + 1)..];
            if (snapshot.TryGetColumn(unqualified, out _))
                return unqualified;
        }

        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
    }

    private static ISchemaColumn FindSchemaColumn(
        IReadOnlyCollection<ISchemaColumn> columns,
        string name)
    {
        foreach (var column in columns)
            if (string.Equals(column.ColumnName, name, StringComparison.Ordinal) ||
                column.ColumnName.EndsWith('.' + name, StringComparison.Ordinal))
                return column;
        return null;
    }

    private sealed record BoundColumn(
        string Name,
        int SourceOrdinal,
        int OutputIndex,
        Type OutputType,
        StructuredTypeState TypeState);
}

internal sealed class JsonPredicateEvaluator
{
    private static readonly JsonPredicateEvaluator Empty = new([]);
    private readonly JsonPredicateTerm[] _terms;

    private JsonPredicateEvaluator(JsonPredicateTerm[] terms)
    {
        _terms = terms;
    }

    public static JsonPredicateEvaluator Create(
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate,
        JsonRowLayout layout)
    {
        if (predicate is null)
            return Empty;

        var terms = new List<JsonPredicateTerm>();
        AddTerms(snapshot, predicate, layout, terms);
        return terms.Count == 0 ? Empty : new JsonPredicateEvaluator(terms.ToArray());
    }

    public bool Matches(ReadOnlySpan<byte> record, JsonReaderOptions options)
    {
        if (_terms.Length == 0)
            return true;

        Span<bool> seen = _terms.Length <= 64
            ? stackalloc bool[_terms.Length]
            : new bool[_terms.Length];
        Span<bool> matched = _terms.Length <= 64
            ? stackalloc bool[_terms.Length]
            : new bool[_terms.Length];
        Span<bool> propertyMatches = _terms.Length <= 64
            ? stackalloc bool[_terms.Length]
            : new bool[_terms.Length];
        var reader = new Utf8JsonReader(record, true, new JsonReaderState(options));
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A framed JSON row must be an object.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("A JSON object must contain property names and values.");

            var nameHash = reader.ValueIsEscaped ? 0 : JsonPropertyLookup.Hash(reader.ValueSpan);
            propertyMatches.Clear();
            for (var index = 0; index < _terms.Length; index++)
                propertyMatches[index] = _terms[index].NameMatches(nameHash, ref reader);

            if (!reader.Read())
                throw new JsonException("A JSON property has no value.");

            for (var index = 0; index < _terms.Length; index++)
            {
                ref readonly var term = ref _terms[index];
                if (!propertyMatches[index])
                    continue;
                seen[index] = true;
                matched[index] = term.Evaluate(ref reader);
            }

            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                reader.Skip();
        }

        for (var index = 0; index < _terms.Length; index++)
            if (!seen[index] || !matched[index])
                return false;
        return true;
    }

    private static void AddTerms(
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate,
        JsonRowLayout layout,
        List<JsonPredicateTerm> terms)
    {
        switch (predicate)
        {
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                AddTerms(snapshot, logical.Left, layout, terms);
                AddTerms(snapshot, logical.Right, layout, terms);
                return;
            case SourcePredicateComparison comparison when
                JsonSourcePlanner.TryGetComparisonParts(comparison, out var name, out var literal, out var op):
            {
                if (!snapshot.TryGetColumn(name, out var column))
                {
                    var dotIndex = name.LastIndexOf('.');
                    if (dotIndex < 0 || !snapshot.TryGetColumn(name[(dotIndex + 1)..], out column))
                        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
                    name = column.Name;
                }

                _ = layout.GetSlot(name);
                terms.Add(JsonPredicateTerm.Create(name, column.TypeState.Kind, literal.Value, op));
                return;
            }
            default:
                throw new InvalidOperationException("JSON execution received a predicate it did not accept.");
        }
    }
}

internal sealed class JsonPredicateTerm
{
    private readonly bool _boolean;
    private readonly decimal _decimal;
    private readonly double _double;
    private readonly ulong _nameHash;
    private readonly byte[] _nameUtf8;
    private readonly long _long;
    private readonly SourcePredicateComparisonOperator _operator;
    private readonly byte[] _stringUtf8;
    private readonly StructuredValueKind _type;

    private JsonPredicateTerm(
        string name,
        StructuredValueKind type,
        SourcePredicateComparisonOperator op,
        long longValue,
        decimal decimalValue,
        double doubleValue,
        bool booleanValue,
        byte[] stringUtf8)
    {
        _nameUtf8 = Encoding.UTF8.GetBytes(name);
        _nameHash = JsonPropertyLookup.Hash(_nameUtf8);
        _type = type;
        _operator = op;
        _long = longValue;
        _decimal = decimalValue;
        _double = doubleValue;
        _boolean = booleanValue;
        _stringUtf8 = stringUtf8;
    }

    public static JsonPredicateTerm Create(
        string name,
        StructuredValueKind type,
        object literal,
        SourcePredicateComparisonOperator op)
    {
        return type switch
        {
            StructuredValueKind.Long => new JsonPredicateTerm(
                name, type, op, Convert.ToInt64(literal, CultureInfo.InvariantCulture), 0, 0, false, null),
            StructuredValueKind.Decimal => new JsonPredicateTerm(
                name, type, op, 0, Convert.ToDecimal(literal, CultureInfo.InvariantCulture), 0, false, null),
            StructuredValueKind.Double => new JsonPredicateTerm(
                name, type, op, 0, 0, Convert.ToDouble(literal, CultureInfo.InvariantCulture), false, null),
            StructuredValueKind.Boolean => new JsonPredicateTerm(
                name, type, op, 0, 0, 0, (bool)literal, null),
            StructuredValueKind.String => new JsonPredicateTerm(
                name, type, op, 0, 0, 0, false, Encoding.UTF8.GetBytes((string)literal)),
            _ => throw new InvalidOperationException($"JSON predicate type '{type}' is not supported.")
        };
    }

    public bool NameMatches(ulong unescapedHash, ref Utf8JsonReader propertyReader)
    {
        return propertyReader.ValueIsEscaped
            ? propertyReader.ValueTextEquals(_nameUtf8)
            : unescapedHash == _nameHash && propertyReader.ValueSpan.SequenceEqual(_nameUtf8);
    }

    public bool Evaluate(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return false;

        return _type switch
        {
            StructuredValueKind.Long when reader.TokenType == JsonTokenType.Number =>
                Compare(reader.GetInt64(), _long, _operator),
            StructuredValueKind.Decimal when reader.TokenType == JsonTokenType.Number =>
                Compare(reader.GetDecimal(), _decimal, _operator),
            StructuredValueKind.Double when reader.TokenType == JsonTokenType.Number =>
                Compare(reader.GetDouble(), _double, _operator),
            StructuredValueKind.Boolean when reader.TokenType is JsonTokenType.True or JsonTokenType.False =>
                Compare(reader.GetBoolean(), _boolean, _operator),
            StructuredValueKind.String when reader.TokenType == JsonTokenType.String =>
                _operator == SourcePredicateComparisonOperator.Equal
                    ? reader.ValueTextEquals(_stringUtf8)
                    : !reader.ValueTextEquals(_stringUtf8),
            _ => false
        };
    }

    private static bool Compare<T>(T left, T right, SourcePredicateComparisonOperator op)
        where T : IComparable<T>
    {
        var comparison = left.CompareTo(right);
        return op switch
        {
            SourcePredicateComparisonOperator.Equal => comparison == 0,
            SourcePredicateComparisonOperator.NotEqual => comparison != 0,
            SourcePredicateComparisonOperator.GreaterThan => comparison > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => comparison >= 0,
            SourcePredicateComparisonOperator.LessThan => comparison < 0,
            SourcePredicateComparisonOperator.LessOrEqual => comparison <= 0,
            _ => false
        };
    }
}
