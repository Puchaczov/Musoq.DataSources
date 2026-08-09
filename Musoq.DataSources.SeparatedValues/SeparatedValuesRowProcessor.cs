#nullable enable

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesRowProcessor
{
    private readonly CancellationToken _cancellationToken;
    private readonly int _chunkSize;
    private readonly SeparatedValuesRowLayout _layout;
    private readonly SeparatedValuesPredicateEvaluator _predicate;
    private readonly DataSourceProgressReporter? _progress;
    private readonly long _rowNumberOffset;
    private readonly long? _take;
    private readonly IChunkWriter<object?[]> _writer;
    private List<object?[]>? _chunk;
    private long _emittedRows;
    private long _skipRemaining;
    private int _zeroColumnRows;

    public SeparatedValuesRowProcessor(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext executionContext,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter? progress,
        int chunkSize,
        CancellationToken cancellationToken,
        long rowNumberOffset = 0)
    {
        _writer = writer;
        _progress = progress;
        _chunkSize = chunkSize;
        _cancellationToken = cancellationToken;
        _rowNumberOffset = rowNumberOffset;
        _skipRemaining = executionContext.Plan.AcceptedSkip ?? 0;
        _take = executionContext.Plan.AcceptedTake;
        _layout = SeparatedValuesRowLayout.Create(snapshot, executionContext);
        _predicate = SeparatedValuesPredicateEvaluator.Create(
            snapshot,
            executionContext.Plan.AcceptedPredicate);
    }

    public long RowsRead { get; private set; }

    public long RowsEmitted => _emittedRows;

    public bool HasWork => _take is null || _take.Value > 0;

    public bool Process(SeparatedValuesUtf8Record record)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        RowsRead++;
        _progress?.RowRead();

        if (!_predicate.Matches(record))
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
            _chunk ??= new List<object?[]>(_chunkSize);
            _chunk.Add(_layout.Materialize(record, _rowNumberOffset + RowsRead));
        }
        else
        {
            _zeroColumnRows++;
        }

        _emittedRows++;

        if (_zeroColumnRows == _chunkSize || _chunk?.Count == _chunkSize)
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
            _writer.Write(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), _zeroColumnRows));
            _zeroColumnRows = 0;
        }

        if (_chunk is null || _chunk.Count == 0)
            return;

        _writer.Write(_chunk);
        _chunk = null;
    }
}

internal sealed class SeparatedValuesRowLayout
{
    private readonly BoundColumn[] _columns;
    private readonly int _outputCount;
    private readonly StructuredStringPool _stringPool;

    private SeparatedValuesRowLayout(
        BoundColumn[] columns,
        int outputCount,
        StructuredStringPool stringPool)
    {
        _columns = columns;
        _outputCount = outputCount;
        _stringPool = stringPool;
    }

    public static SeparatedValuesRowLayout Create(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext executionContext)
    {
        var columns = new List<BoundColumn>();
        var outputCount = 0;
        var readPlan = SeparatedValuesReadPlan.From(executionContext.Plan);
        var projectionAccepted = readPlan.ProjectionAccepted || executionContext.Plan.AcceptedColumns.Count > 0;

        if (projectionAccepted)
        {
            var denseFallback = 0;
            foreach (var accepted in executionContext.Plan.AcceptedColumns)
            {
                var name = ResolveName(snapshot, accepted.Name);
                var schemaColumn = FindSchemaColumn(executionContext.AllColumns, name);
                var outputIndex = schemaColumn?.ColumnIndex ?? denseFallback;
                var outputType = schemaColumn?.ColumnType ?? GetSnapshotColumn(snapshot, name).ClrType;
                AddColumn(columns, snapshot, name, outputIndex, outputType);
                outputCount = Math.Max(outputCount, outputIndex + 1);
                denseFallback++;
            }
        }
        else if (executionContext.AllColumns.Count > 0)
        {
            foreach (var schemaColumn in executionContext.AllColumns.OrderBy(column => column.ColumnIndex))
            {
                var name = ResolveName(snapshot, schemaColumn.ColumnName);
                AddColumn(columns, snapshot, name, schemaColumn.ColumnIndex, schemaColumn.ColumnType);
                outputCount = Math.Max(outputCount, schemaColumn.ColumnIndex + 1);
            }
        }
        else
        {
            foreach (var column in snapshot.Columns)
            {
                AddColumn(columns, snapshot, column.Name, column.SourceOrdinal, column.ClrType);
                outputCount++;
            }
        }

        return new SeparatedValuesRowLayout(
            columns.OrderBy(column => column.SourceOrdinal).ToArray(),
            outputCount,
            snapshot.StringPool);
    }

    public bool HasOutputColumns => _outputCount > 0;

    public object?[] Materialize(SeparatedValuesUtf8Record record, long rowNumber)
    {
        if (_outputCount == 0)
            return Array.Empty<object?>();

        var output = new object?[_outputCount];
        var bindingIndex = 0;
        var fieldIndex = 0;

        foreach (var field in record)
        {
            while (bindingIndex < _columns.Length && _columns[bindingIndex].SourceOrdinal < fieldIndex)
                bindingIndex++;

            if (bindingIndex == _columns.Length)
                break;

            ref readonly var binding = ref _columns[bindingIndex];
            if (binding.SourceOrdinal == fieldIndex)
            {
                output[binding.OutputOrdinal] = binding.Conversion == SeparatedValuesConversion.String &&
                                                 !SeparatedValuesValueConverter.IsNull(field) &&
                                                 !field.NeedsUnescaping
                    ? _stringPool.GetOrAddUtf8(binding.SourceOrdinal, field.EncodedValue)
                    : SeparatedValuesValueConverter.Convert(
                        field,
                        binding.Conversion,
                        binding.Name,
                        rowNumber);
                bindingIndex++;
            }

            fieldIndex++;
        }

        return output;
    }

    private static void AddColumn(
        List<BoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        string name,
        int outputOrdinal,
        Type outputType)
    {
        var snapshotColumn = GetSnapshotColumn(snapshot, name);
        columns.Add(new BoundColumn(
            name,
            snapshotColumn.SourceOrdinal,
            outputOrdinal,
            SeparatedValuesValueConverter.GetConversion(outputType, snapshotColumn.TypeState)));
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

    private static ISchemaColumn? FindSchemaColumn(
        IReadOnlyCollection<ISchemaColumn> columns,
        string name)
    {
        foreach (var column in columns)
        {
            if (string.Equals(column.ColumnName, name, StringComparison.Ordinal) ||
                column.ColumnName.EndsWith('.' + name, StringComparison.Ordinal))
                return column;
        }

        return null;
    }

    private readonly record struct BoundColumn(
        string Name,
        int SourceOrdinal,
        int OutputOrdinal,
        SeparatedValuesConversion Conversion);
}

internal sealed class SeparatedValuesPredicateEvaluator
{
    private static readonly SeparatedValuesPredicateEvaluator Empty = new([]);
    private readonly PredicateTerm[] _terms;

    private SeparatedValuesPredicateEvaluator(PredicateTerm[] terms)
    {
        _terms = terms;
    }

    public static SeparatedValuesPredicateEvaluator Create(
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression? predicate)
    {
        if (predicate is null)
            return Empty;

        var terms = new List<PredicateTerm>();
        AddTerms(snapshot, predicate, terms);
        terms.Sort((left, right) => left.SourceOrdinal.CompareTo(right.SourceOrdinal));
        return new SeparatedValuesPredicateEvaluator(terms.ToArray());
    }

    public bool Matches(SeparatedValuesUtf8Record record)
    {
        if (_terms.Length == 0)
            return true;

        var termIndex = 0;
        var fieldIndex = 0;

        foreach (var field in record)
        {
            while (termIndex < _terms.Length && _terms[termIndex].SourceOrdinal < fieldIndex)
                return false;

            if (termIndex == _terms.Length)
                return true;

            while (termIndex < _terms.Length && _terms[termIndex].SourceOrdinal == fieldIndex)
            {
                if (!_terms[termIndex].Evaluate(field))
                    return false;
                termIndex++;
            }

            fieldIndex++;
        }

        return termIndex == _terms.Length;
    }

    private static void AddTerms(
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate,
        List<PredicateTerm> terms)
    {
        switch (predicate)
        {
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                AddTerms(snapshot, logical.Left, terms);
                AddTerms(snapshot, logical.Right, terms);
                return;
            case SourcePredicateComparison comparison when
                SeparatedValuesSourcePlanner.TryGetComparisonParts(
                    comparison,
                    out var name,
                    out var literal,
                    out var op):
            {
                if (!snapshot.TryGetColumn(name, out var column))
                    throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);

                terms.Add(PredicateTerm.Create(column, literal.Value!, op));
                return;
            }
            default:
                throw new InvalidOperationException("Separated-values execution received a predicate it did not accept.");
        }
    }

    private sealed class PredicateTerm
    {
        private readonly bool _boolean;
        private readonly decimal _decimal;
        private readonly double _double;
        private readonly long _long;
        private readonly SourcePredicateComparisonOperator _operator;
        private readonly byte[]? _stringUtf8;
        private readonly StructuredValueKind _type;

        private PredicateTerm(
            int sourceOrdinal,
            StructuredValueKind type,
            SourcePredicateComparisonOperator op,
            long longValue,
            decimal decimalValue,
            double doubleValue,
            bool booleanValue,
            byte[]? stringUtf8)
        {
            SourceOrdinal = sourceOrdinal;
            _type = type;
            _operator = op;
            _long = longValue;
            _decimal = decimalValue;
            _double = doubleValue;
            _boolean = booleanValue;
            _stringUtf8 = stringUtf8;
        }

        public int SourceOrdinal { get; }

        public static PredicateTerm Create(
            StructuredColumnSnapshot column,
            object literal,
            SourcePredicateComparisonOperator op)
        {
            return column.TypeState.Kind switch
            {
                StructuredValueKind.Long => new PredicateTerm(
                    column.SourceOrdinal,
                    column.TypeState.Kind,
                    op,
                    Convert.ToInt64(literal, CultureInfo.InvariantCulture),
                    0,
                    0,
                    false,
                    null),
                StructuredValueKind.Decimal => new PredicateTerm(
                    column.SourceOrdinal,
                    column.TypeState.Kind,
                    op,
                    0,
                    Convert.ToDecimal(literal, CultureInfo.InvariantCulture),
                    0,
                    false,
                    null),
                StructuredValueKind.Double => new PredicateTerm(
                    column.SourceOrdinal,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    Convert.ToDouble(literal, CultureInfo.InvariantCulture),
                    false,
                    null),
                StructuredValueKind.Boolean => new PredicateTerm(
                    column.SourceOrdinal,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    0,
                    (bool)literal,
                    null),
                StructuredValueKind.String => new PredicateTerm(
                    column.SourceOrdinal,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    0,
                    false,
                    Encoding.UTF8.GetBytes((string)literal)),
                _ => throw new InvalidOperationException(
                    $"Separated-values predicate type '{column.TypeState.Kind}' is not supported.")
            };
        }

        public bool Evaluate(SeparatedValuesUtf8Field field)
        {
            if (SeparatedValuesValueConverter.IsNull(field))
                return false;

            return _type switch
            {
                StructuredValueKind.Long => EvaluateLong(field),
                StructuredValueKind.Decimal => EvaluateDecimal(field),
                StructuredValueKind.Double => EvaluateDouble(field),
                StructuredValueKind.Boolean => EvaluateBoolean(field),
                StructuredValueKind.String => _operator == SourcePredicateComparisonOperator.Equal
                    ? field.ValueEquals(_stringUtf8!)
                    : !field.ValueEquals(_stringUtf8!),
                _ => throw new InvalidOperationException(
                    $"Separated-values predicate type '{_type}' is not supported.")
            };
        }

        private bool EvaluateLong(SeparatedValuesUtf8Field field)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out long value))
                throw InvalidPredicateValue();
            return Matches(value.CompareTo(_long), _operator);
        }

        private bool EvaluateDecimal(SeparatedValuesUtf8Field field)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out decimal value))
                throw InvalidPredicateValue();
            return Matches(decimal.Compare(value, _decimal), _operator);
        }

        private bool EvaluateDouble(SeparatedValuesUtf8Field field)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out double value))
                throw InvalidPredicateValue();
            return Matches(value.CompareTo(_double), _operator);
        }

        private bool EvaluateBoolean(SeparatedValuesUtf8Field field)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out bool value))
                throw InvalidPredicateValue();
            return _operator == SourcePredicateComparisonOperator.Equal ? value == _boolean : value != _boolean;
        }

        private FormatException InvalidPredicateValue()
        {
            return new FormatException(
                $"A separated-values predicate field cannot be converted as {_type}.");
        }

        private static bool Matches(int comparison, SourcePredicateComparisonOperator op)
        {
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
}

internal enum SeparatedValuesConversion : byte
{
    String,
    Boolean,
    Byte,
    Character,
    DateTime,
    DateTimeOffset,
    Decimal,
    Double,
    Int16,
    Int32,
    Int64,
    SByte,
    Single,
    TimeSpan,
    UInt16,
    UInt32,
    UInt64,
    Guid,
    DateOnly,
    TimeOnly
}

internal static class SeparatedValuesValueConverter
{
    public static SeparatedValuesConversion GetConversion(Type outputType, StructuredTypeState inferredType)
    {
        var type = outputType.GetUnderlyingNullable();
        if (type == typeof(object))
        {
            type = inferredType.Kind switch
            {
                StructuredValueKind.Boolean => typeof(bool),
                StructuredValueKind.Long => typeof(long),
                StructuredValueKind.Decimal => typeof(decimal),
                StructuredValueKind.Double => typeof(double),
                _ => typeof(string)
            };
        }

        if (type == typeof(string)) return SeparatedValuesConversion.String;
        if (type == typeof(bool)) return SeparatedValuesConversion.Boolean;
        if (type == typeof(byte)) return SeparatedValuesConversion.Byte;
        if (type == typeof(char)) return SeparatedValuesConversion.Character;
        if (type == typeof(DateTime)) return SeparatedValuesConversion.DateTime;
        if (type == typeof(DateTimeOffset)) return SeparatedValuesConversion.DateTimeOffset;
        if (type == typeof(decimal)) return SeparatedValuesConversion.Decimal;
        if (type == typeof(double)) return SeparatedValuesConversion.Double;
        if (type == typeof(short)) return SeparatedValuesConversion.Int16;
        if (type == typeof(int)) return SeparatedValuesConversion.Int32;
        if (type == typeof(long)) return SeparatedValuesConversion.Int64;
        if (type == typeof(sbyte)) return SeparatedValuesConversion.SByte;
        if (type == typeof(float)) return SeparatedValuesConversion.Single;
        if (type == typeof(TimeSpan)) return SeparatedValuesConversion.TimeSpan;
        if (type == typeof(ushort)) return SeparatedValuesConversion.UInt16;
        if (type == typeof(uint)) return SeparatedValuesConversion.UInt32;
        if (type == typeof(ulong)) return SeparatedValuesConversion.UInt64;
        if (type == typeof(Guid)) return SeparatedValuesConversion.Guid;
        if (type == typeof(DateOnly)) return SeparatedValuesConversion.DateOnly;
        if (type == typeof(TimeOnly)) return SeparatedValuesConversion.TimeOnly;

        throw new NotSupportedException($"Explicit separated-values type '{outputType}' is not supported.");
    }

    public static object? Convert(
        SeparatedValuesUtf8Field field,
        SeparatedValuesConversion conversion,
        string columnName,
        long rowNumber)
    {
        if (IsNull(field))
            return null;

        try
        {
            return conversion switch
            {
                SeparatedValuesConversion.String => field.Decode(),
                SeparatedValuesConversion.Boolean when TryParse(field, out bool value) => value,
                SeparatedValuesConversion.Byte when TryParse(field, out byte value) => value,
                SeparatedValuesConversion.Character => ParseCharacter(field),
                SeparatedValuesConversion.DateTime => ParseDateTime(field),
                SeparatedValuesConversion.DateTimeOffset => ParseDateTimeOffset(field),
                SeparatedValuesConversion.Decimal when TryParse(field, out decimal value) => value,
                SeparatedValuesConversion.Double when TryParse(field, out double value) => value,
                SeparatedValuesConversion.Int16 when TryParse(field, out short value) => value,
                SeparatedValuesConversion.Int32 when TryParse(field, out int value) => value,
                SeparatedValuesConversion.Int64 when TryParse(field, out long value) => value,
                SeparatedValuesConversion.SByte when TryParse(field, out sbyte value) => value,
                SeparatedValuesConversion.Single when TryParse(field, out float value) => value,
                SeparatedValuesConversion.TimeSpan => ParseTimeSpan(field),
                SeparatedValuesConversion.UInt16 when TryParse(field, out ushort value) => value,
                SeparatedValuesConversion.UInt32 when TryParse(field, out uint value) => value,
                SeparatedValuesConversion.UInt64 when TryParse(field, out ulong value) => value,
                SeparatedValuesConversion.Guid => ParseGuid(field),
                SeparatedValuesConversion.DateOnly => ParseDateOnly(field),
                SeparatedValuesConversion.TimeOnly => ParseTimeOnly(field),
                _ => throw new FormatException()
            };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new FormatException(
                $"Separated-values row {rowNumber:N0} column '{columnName}' cannot be converted as {conversion}.",
                exception);
        }
    }

    public static bool IsNull(SeparatedValuesUtf8Field field)
    {
        return !field.WasQuoted && field.EncodedValue.IsEmpty;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out bool value)
    {
        var bytes = field.EncodedValue;
        if (field.NeedsUnescaping)
        {
            value = false;
            return false;
        }

        if (bytes.Length == 4 &&
            ToLowerAscii(bytes[0]) == (byte)'t' &&
            ToLowerAscii(bytes[1]) == (byte)'r' &&
            ToLowerAscii(bytes[2]) == (byte)'u' &&
            ToLowerAscii(bytes[3]) == (byte)'e')
        {
            value = true;
            return true;
        }

        if (bytes.Length == 5 &&
            ToLowerAscii(bytes[0]) == (byte)'f' &&
            ToLowerAscii(bytes[1]) == (byte)'a' &&
            ToLowerAscii(bytes[2]) == (byte)'l' &&
            ToLowerAscii(bytes[3]) == (byte)'s' &&
            ToLowerAscii(bytes[4]) == (byte)'e')
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out byte value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out sbyte value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out short value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out ushort value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out int value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out uint value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out long value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out ulong value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out decimal value)
    {
        if (field.NeedsUnescaping)
        {
            value = default;
            return false;
        }

        return SeparatedValuesDecimalParser.TryParse(field.EncodedValue, out value);
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out float value)
    {
        var result = TryParseCore(field, Utf8Parser.TryParse, out value);
        return result && float.IsFinite(value);
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out double value)
    {
        var result = TryParseCore(field, Utf8Parser.TryParse, out value);
        return result && double.IsFinite(value);
    }

    private static bool TryParseCore<T>(
        SeparatedValuesUtf8Field field,
        Utf8TryParse<T> parser,
        out T value)
    {
        if (field.NeedsUnescaping ||
            !parser(field.EncodedValue, out value, out var consumed) ||
            consumed != field.EncodedValue.Length)
        {
            value = default!;
            return false;
        }

        return true;
    }

    private static char ParseCharacter(SeparatedValuesUtf8Field field)
    {
        var value = field.Decode();
        return value.Length == 1 ? value[0] : throw new FormatException();
    }

    private static DateTime ParseDateTime(SeparatedValuesUtf8Field field)
    {
        return DateTime.TryParse(field.Decode(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static DateTimeOffset ParseDateTimeOffset(SeparatedValuesUtf8Field field)
    {
        return DateTimeOffset.TryParse(field.Decode(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static TimeSpan ParseTimeSpan(SeparatedValuesUtf8Field field)
    {
        return TimeSpan.TryParse(field.Decode(), CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException();
    }

    private static Guid ParseGuid(SeparatedValuesUtf8Field field)
    {
        return Guid.TryParse(field.Decode(), out var value) ? value : throw new FormatException();
    }

    private static DateOnly ParseDateOnly(SeparatedValuesUtf8Field field)
    {
        return DateOnly.TryParse(field.Decode(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static TimeOnly ParseTimeOnly(SeparatedValuesUtf8Field field)
    {
        return TimeOnly.TryParse(field.Decode(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static byte ToLowerAscii(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + ((byte)'a' - (byte)'A'))
            : value;
    }

    private delegate bool Utf8TryParse<T>(ReadOnlySpan<byte> source, out T value, out int bytesConsumed, char format = default);
}
