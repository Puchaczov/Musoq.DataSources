using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Musoq.Schema;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesRowParser
{
    private readonly Dictionary<string, ColumnBinding> _bindingsByName;
    private readonly OutputBinding[] _outputBindings;
    private readonly PredicateBinding? _acceptedPredicate;
    private readonly bool _projectionAccepted;
    private readonly int _outputLength;

    public SeparatedValuesRowParser(
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> allColumns,
        IReadOnlyCollection<ISchemaColumn> outputColumns,
        bool projectionAccepted,
        SourcePredicateExpression? acceptedPredicate)
    {
        _projectionAccepted = projectionAccepted;
        _bindingsByName = CreateBindings(indexToNameMap, allColumns);
        _outputBindings = CreateOutputBindings(indexToNameMap, outputColumns, projectionAccepted);
        _outputLength = projectionAccepted
            ? outputColumns.Count == 0 ? 0 : outputColumns.Max(column => column.ColumnIndex) + 1
            : -1;
        _acceptedPredicate = CreatePredicateBinding(acceptedPredicate);
    }

    public bool MatchesAcceptedPredicate(ISeparatedValuesFieldReader row)
    {
        return Matches(_acceptedPredicate, row);
    }

    public object?[] Parse(ISeparatedValuesFieldReader row)
    {
        if (_projectionAccepted && _outputLength == 0)
            return [];

        var parsedRecords = new object?[_projectionAccepted ? _outputLength : row.FieldCount];

        foreach (var binding in _outputBindings)
        {
            if (binding.SourceIndex >= row.FieldCount || binding.OutputIndex >= parsedRecords.Length)
                continue;

            parsedRecords[binding.OutputIndex] = binding.Convert(row.GetField(binding.SourceIndex));
        }

        return parsedRecords;
    }

    private bool Matches(PredicateBinding? predicate, ISeparatedValuesFieldReader row)
    {
        if (predicate is null)
            return true;

        if (predicate.IsLogical)
            return Matches(predicate.Left, row) && Matches(predicate.Right, row);

        return EvaluateComparison(predicate, row);
    }

    private bool EvaluateComparison(PredicateBinding predicate, ISeparatedValuesFieldReader row)
    {
        if (!predicate.LiteralCanCompare || predicate.SourceIndex >= row.FieldCount)
            return false;

        if (!TryConvertValue(row.GetField(predicate.SourceIndex), predicate.Column, predicate.ComparisonType, out var left) ||
            left is null ||
            predicate.LiteralValue is null)
            return false;

        var compare = Compare(left, predicate.LiteralValue, predicate.ComparisonType);

        return predicate.Operator switch
        {
            SourcePredicateComparisonOperator.Equal => compare == 0,
            SourcePredicateComparisonOperator.NotEqual => compare != 0,
            SourcePredicateComparisonOperator.GreaterThan => compare > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => compare >= 0,
            SourcePredicateComparisonOperator.LessThan => compare < 0,
            SourcePredicateComparisonOperator.LessOrEqual => compare <= 0,
            _ => false
        };
    }

    private static ValueConverter CreateValueConverter(ISchemaColumn? column, Type? type)
    {
        if (type is null)
            return value => value;

        return value => TryConvertValue(value, column, type, out var converted) ? converted : null;
    }

    private static Dictionary<string, ColumnBinding> CreateBindings(
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> allColumns)
    {
        var columns = allColumns
            .GroupBy(column => column.ColumnName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var bindings = new Dictionary<string, ColumnBinding>(StringComparer.Ordinal);

        foreach (var pair in indexToNameMap)
        {
            columns.TryGetValue(pair.Value, out var column);
            bindings.TryAdd(pair.Value, new ColumnBinding(
                pair.Key,
                column?.ColumnType.GetUnderlyingNullable(),
                column));
        }

        return bindings;
    }

    private OutputBinding[] CreateOutputBindings(
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> outputColumns,
        bool projectionAccepted)
    {
        if (projectionAccepted && outputColumns.Count == 0)
            return [];

        var selectedNames = projectionAccepted
            ? outputColumns.Select(column => column.ColumnName).ToHashSet(StringComparer.Ordinal)
            : null;
        var bindings = new List<OutputBinding>();

        foreach (var pair in indexToNameMap.OrderBy(pair => pair.Key))
        {
            if (selectedNames is not null && !selectedNames.Contains(pair.Value))
                continue;

            _bindingsByName.TryGetValue(pair.Value, out var binding);
            bindings.Add(new OutputBinding(pair.Key, pair.Key, CreateValueConverter(binding.Column, binding.Type)));
        }

        return bindings.ToArray();
    }

    private static Type GetPredicateComparisonType(Type? type)
    {
        if (type is null || type == typeof(object))
            return typeof(string);

        return type;
    }

    private PredicateBinding? CreatePredicateBinding(SourcePredicateExpression? predicate)
    {
        return predicate switch
        {
            null => null,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                PredicateBinding.CreateLogical(
                    CreatePredicateBinding(logical.Left),
                    CreatePredicateBinding(logical.Right)),
            SourcePredicateComparison comparison => CreateComparisonPredicateBinding(comparison),
            _ => null
        };
    }

    private PredicateBinding? CreateComparisonPredicateBinding(SourcePredicateComparison comparison)
    {
        if (!SeparatedValuesSourcePlanner.TryGetComparisonParts(
                comparison,
                out var columnName,
                out var literal,
                out var op))
            return null;

        if (!_bindingsByName.TryGetValue(columnName, out var binding))
            throw new InvalidOperationException(
                $"Accepted predicate references column '{columnName}' that is not available in separated values source.");

        var comparisonType = GetPredicateComparisonType(binding.Type);
        var literalCanCompare = TryConvertLiteral(literal.Value, binding.Column, comparisonType, out var literalValue) &&
                                literalValue is not null;

        return PredicateBinding.CreateComparison(
            binding.SourceIndex,
            binding.Column,
            comparisonType,
            op,
            literalValue,
            literalCanCompare);
    }

    private static bool TryConvertLiteral(object? value, ISchemaColumn? column, Type type, out object? converted)
    {
        if (value is null)
        {
            converted = null;
            return false;
        }

        if (type.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        return TryConvertTextToType(ApplyTextModifiers(value.ToString(), column), type, column?.ReadModifiers, out converted);
    }

    private static bool TryConvertValue(string? value, ISchemaColumn? column, Type type, out object? converted)
    {
        if (value is null)
        {
            converted = null;
            return true;
        }

        var text = value;
        var modifiers = column?.ReadModifiers;

        if (modifiers is not null &&
            modifiers.TryGetValue(SeparatedValuesReadModifiers.SourceCodec, out var codec))
        {
            if (!SeparatedValuesReadModifiers.TryDecodeSourceCodec(text, codec, out var bytes))
            {
                converted = null;
                return true;
            }

            try
            {
                text = SeparatedValuesReadModifiers.ResolveColumnEncodingOrThrow(column!).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                converted = null;
                return true;
            }
        }

        return TryConvertTextToType(ApplyTextModifiers(text, column), type, modifiers, out converted);
    }

    private static string? ApplyTextModifiers(string? value, ISchemaColumn? column)
    {
        if (value is null)
            return null;

        return column?.ReadModifiers.ContainsKey(ColumnReadModifiers.Trim) == true
            ? value.Trim()
            : value;
    }

    private static bool TryConvertTextToType(
        string? value,
        Type type,
        IReadOnlyDictionary<string, string>? modifiers,
        out object? converted)
    {
        if (type == typeof(DateTimeOffset))
            return TryParseDateTimeOffset(value, modifiers, out converted);

        if (type == typeof(TimeSpan))
            return TryParseTimeSpan(value, modifiers, out converted);

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                return TryParse<bool>(value, bool.TryParse, out converted);
            case TypeCode.Byte:
                return TryParseNumber<byte>(value, byte.TryParse, modifiers, out converted);
            case TypeCode.Char:
                return TryParse<char>(value, char.TryParse, out converted);
            case TypeCode.DateTime:
                return TryParseDateTime(value, modifiers, out converted);
            case TypeCode.DBNull:
                throw new NotSupportedException($"Type {TypeCode.DBNull} is not supported.");
            case TypeCode.Decimal:
                return TryParseNumber<decimal>(value, decimal.TryParse, modifiers, out converted);
            case TypeCode.Double:
                return TryParseNumber<double>(value, double.TryParse, modifiers, out converted);
            case TypeCode.Empty:
                throw new NotSupportedException($"Type {TypeCode.Empty} is not supported.");
            case TypeCode.Int16:
                return TryParseNumber<short>(value, short.TryParse, modifiers, out converted);
            case TypeCode.Int32:
                return TryParseNumber<int>(value, int.TryParse, modifiers, out converted);
            case TypeCode.Int64:
                return TryParseNumber<long>(value, long.TryParse, modifiers, out converted);
            case TypeCode.Object:
                if (type == typeof(object))
                {
                    converted = string.IsNullOrEmpty(value) ? null : value;
                    return true;
                }

                throw new NotSupportedException($"Type {type} is not supported.");
            case TypeCode.SByte:
                return TryParseNumber<sbyte>(value, sbyte.TryParse, modifiers, out converted);
            case TypeCode.Single:
                return TryParseNumber<float>(value, float.TryParse, modifiers, out converted);
            case TypeCode.String:
                converted = string.IsNullOrEmpty(value) ? null : value;
                return true;
            case TypeCode.UInt16:
                return TryParseNumber<ushort>(value, ushort.TryParse, modifiers, out converted);
            case TypeCode.UInt32:
                return TryParseNumber<uint>(value, uint.TryParse, modifiers, out converted);
            case TypeCode.UInt64:
                return TryParseNumber<ulong>(value, ulong.TryParse, modifiers, out converted);
            default:
                throw new NotSupportedException($"Type {type} is not supported.");
        }
    }

    private static int Compare(object left, object right, Type type)
    {
        if (type == typeof(string))
            return string.Compare((string)left, (string)right, StringComparison.Ordinal);

        return ((IComparable)left).CompareTo(right);
    }

    private static bool TryParse<T>(
        string? value,
        TryParseHandler<T> parser,
        out object? converted)
    {
        if (parser(value, out var parsed))
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private static bool TryParseNumber<T>(
        string? value,
        NumberTryParseHandler<T> parser,
        IReadOnlyDictionary<string, string>? modifiers,
        out object? converted)
    {
        if (parser(value, NumberStyles.Any, ResolveCulture(modifiers), out var parsed))
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private static bool TryParseDateTime(
        string? value,
        IReadOnlyDictionary<string, string>? modifiers,
        out object? converted)
    {
        var culture = ResolveCulture(modifiers);
        var parsedSuccessfully = modifiers is not null &&
                                 modifiers.TryGetValue(ColumnReadModifiers.Format, out var format)
            ? DateTime.TryParseExact(value, format, culture, DateTimeStyles.None, out var parsed)
            : DateTime.TryParse(value, culture, DateTimeStyles.None, out parsed);

        if (parsedSuccessfully)
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private static bool TryParseDateTimeOffset(
        string? value,
        IReadOnlyDictionary<string, string>? modifiers,
        out object? converted)
    {
        var culture = ResolveCulture(modifiers);
        var parsedSuccessfully = modifiers is not null &&
                                 modifiers.TryGetValue(ColumnReadModifiers.Format, out var format)
            ? DateTimeOffset.TryParseExact(value, format, culture, DateTimeStyles.None, out var parsed)
            : DateTimeOffset.TryParse(value, culture, DateTimeStyles.None, out parsed);

        if (parsedSuccessfully)
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private static bool TryParseTimeSpan(
        string? value,
        IReadOnlyDictionary<string, string>? modifiers,
        out object? converted)
    {
        var culture = ResolveCulture(modifiers);
        var parsedSuccessfully = modifiers is not null &&
                                 modifiers.TryGetValue(ColumnReadModifiers.Format, out var format)
            ? TimeSpan.TryParseExact(value, format, culture, out var parsed)
            : TimeSpan.TryParse(value, culture, out parsed);

        if (parsedSuccessfully)
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private static CultureInfo ResolveCulture(IReadOnlyDictionary<string, string>? modifiers)
    {
        return modifiers is null
            ? CultureInfo.CurrentCulture
            : SeparatedValuesReadModifiers.ResolveCulture(modifiers);
    }

    private delegate bool TryParseHandler<T>(string? value, out T result);

    private delegate bool NumberTryParseHandler<T>(
        string? value,
        NumberStyles numberStyles,
        IFormatProvider formatProvider,
        out T result);

    private readonly record struct ColumnBinding(int SourceIndex, Type? Type, ISchemaColumn? Column);

    private readonly record struct OutputBinding(int SourceIndex, int OutputIndex, ValueConverter Convert);

    private delegate object? ValueConverter(string? value);

    private sealed class PredicateBinding
    {
        private PredicateBinding(PredicateBinding? left, PredicateBinding? right)
        {
            Left = left;
            Right = right;
            IsLogical = true;
            ComparisonType = typeof(string);
        }

        private PredicateBinding(
            int sourceIndex,
            ISchemaColumn? column,
            Type comparisonType,
            SourcePredicateComparisonOperator op,
            object? literalValue,
            bool literalCanCompare)
        {
            SourceIndex = sourceIndex;
            Column = column;
            ComparisonType = comparisonType;
            Operator = op;
            LiteralValue = literalValue;
            LiteralCanCompare = literalCanCompare;
        }

        public bool IsLogical { get; }

        public PredicateBinding? Left { get; }

        public PredicateBinding? Right { get; }

        public int SourceIndex { get; }

        public ISchemaColumn? Column { get; }

        public Type ComparisonType { get; }

        public SourcePredicateComparisonOperator Operator { get; }

        public object? LiteralValue { get; }

        public bool LiteralCanCompare { get; }

        public static PredicateBinding? CreateLogical(PredicateBinding? left, PredicateBinding? right)
        {
            return (left, right) switch
            {
                (null, null) => null,
                (not null, null) => left,
                (null, not null) => right,
                _ => new PredicateBinding(left, right)
            };
        }

        public static PredicateBinding CreateComparison(
            int sourceIndex,
            ISchemaColumn? column,
            Type comparisonType,
            SourcePredicateComparisonOperator op,
            object? literalValue,
            bool literalCanCompare)
        {
            return new PredicateBinding(sourceIndex, column, comparisonType, op, literalValue, literalCanCompare);
        }
    }
}
