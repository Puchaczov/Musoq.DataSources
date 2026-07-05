using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesRowParser
{
    private readonly IReadOnlyDictionary<int, string> _indexToNameMap;
    private readonly Dictionary<string, ColumnBinding> _bindingsByName;
    private readonly OutputBinding[] _outputBindings;
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly bool _projectionAccepted;
    private readonly int _outputLength;

    public SeparatedValuesRowParser(
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> allColumns,
        IReadOnlyCollection<ISchemaColumn> outputColumns,
        bool projectionAccepted,
        SourcePredicateExpression? acceptedPredicate)
    {
        _indexToNameMap = indexToNameMap;
        _acceptedPredicate = acceptedPredicate;
        _projectionAccepted = projectionAccepted;
        _bindingsByName = CreateBindings(indexToNameMap, allColumns);
        _outputBindings = CreateOutputBindings(indexToNameMap, outputColumns, projectionAccepted);
        _outputLength = projectionAccepted
            ? outputColumns.Count == 0 ? 0 : outputColumns.Max(column => column.ColumnIndex) + 1
            : -1;
        ValidatePredicateColumns(_acceptedPredicate);
    }

    public bool MatchesAcceptedPredicate(string?[] rawRow)
    {
        return Matches(_acceptedPredicate, rawRow);
    }

    public object?[] Parse(string?[] rawRow)
    {
        if (_projectionAccepted && _outputLength == 0)
            return [];

        var parsedRecords = new object?[_projectionAccepted ? _outputLength : rawRow.Length];

        foreach (var binding in _outputBindings)
        {
            if (binding.SourceIndex >= rawRow.Length || binding.OutputIndex >= parsedRecords.Length)
                continue;

            parsedRecords[binding.OutputIndex] = ConvertOutputValue(rawRow[binding.SourceIndex], binding.Type);
        }

        return parsedRecords;
    }

    private bool Matches(SourcePredicateExpression? predicate, string?[] rawRow)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, rawRow) && Matches(logical.Right, rawRow),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, rawRow),
            _ => true
        };
    }

    private bool EvaluateComparison(SourcePredicateComparison comparison, string?[] rawRow)
    {
        if (!SeparatedValuesSourcePlanner.TryGetComparisonParts(
                comparison,
                out var columnName,
                out var literal,
                out var op))
            return true;

        if (!_bindingsByName.TryGetValue(columnName, out var binding))
            throw new InvalidOperationException(
                $"Accepted predicate references column '{columnName}' that is not available in separated values source.");

        if (binding.SourceIndex >= rawRow.Length)
            return false;

        var comparisonType = GetPredicateComparisonType(binding.Type);
        if (!TryConvertValue(rawRow[binding.SourceIndex], comparisonType, out var left) ||
            left is null ||
            !TryConvertLiteral(literal.Value, comparisonType, out var right) ||
            right is null)
            return false;

        var compare = Compare(left, right, comparisonType);

        return op switch
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

    private object? ConvertOutputValue(string? value, Type? type)
    {
        if (type is null)
            return value;

        if (!TryConvertValue(value, type, out var converted))
            return null;

        return converted;
    }

    private static Dictionary<string, ColumnBinding> CreateBindings(
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> allColumns)
    {
        var types = allColumns
            .GroupBy(column => column.ColumnName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().ColumnType.GetUnderlyingNullable(),
                StringComparer.Ordinal);
        var bindings = new Dictionary<string, ColumnBinding>(StringComparer.Ordinal);

        foreach (var pair in indexToNameMap)
        {
            types.TryGetValue(pair.Value, out var type);
            bindings.TryAdd(pair.Value, new ColumnBinding(pair.Key, type));
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
            bindings.Add(new OutputBinding(pair.Key, pair.Key, binding.Type));
        }

        return bindings.ToArray();
    }

    private static Type GetPredicateComparisonType(Type? type)
    {
        if (type is null || type == typeof(object))
            return typeof(string);

        return type;
    }

    private void ValidatePredicateColumns(SourcePredicateExpression? predicate)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                ValidatePredicateColumns(logical.Left);
                ValidatePredicateColumns(logical.Right);
                return;
            case SourcePredicateComparison comparison:
                if (SeparatedValuesSourcePlanner.TryGetComparisonParts(
                        comparison,
                        out var columnName,
                        out _,
                        out _) &&
                    !_bindingsByName.ContainsKey(columnName))
                    throw new InvalidOperationException(
                        $"Accepted predicate references column '{columnName}' that is not available in separated values source.");
                return;
        }
    }

    private static bool TryConvertLiteral(object? value, Type type, out object? converted)
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

        return TryConvertValue(value.ToString(), type, out converted);
    }

    private static bool TryConvertValue(string? value, Type type, out object? converted)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                return TryParse<bool>(value, bool.TryParse, out converted);
            case TypeCode.Byte:
                return TryParseNumber<byte>(value, byte.TryParse, out converted);
            case TypeCode.Char:
                return TryParse<char>(value, char.TryParse, out converted);
            case TypeCode.DateTime:
                return TryParseDateTime(value, out converted);
            case TypeCode.DBNull:
                throw new NotSupportedException($"Type {TypeCode.DBNull} is not supported.");
            case TypeCode.Decimal:
                return TryParseNumber<decimal>(value, decimal.TryParse, out converted);
            case TypeCode.Double:
                return TryParseNumber<double>(value, double.TryParse, out converted);
            case TypeCode.Empty:
                throw new NotSupportedException($"Type {TypeCode.Empty} is not supported.");
            case TypeCode.Int16:
                return TryParseNumber<short>(value, short.TryParse, out converted);
            case TypeCode.Int32:
                return TryParseNumber<int>(value, int.TryParse, out converted);
            case TypeCode.Int64:
                return TryParseNumber<long>(value, long.TryParse, out converted);
            case TypeCode.Object:
                throw new NotSupportedException($"Type {TypeCode.Object} is not supported.");
            case TypeCode.SByte:
                return TryParseNumber<sbyte>(value, sbyte.TryParse, out converted);
            case TypeCode.Single:
                return TryParseNumber<float>(value, float.TryParse, out converted);
            case TypeCode.String:
                converted = string.IsNullOrEmpty(value) ? null : value;
                return true;
            case TypeCode.UInt16:
                return TryParseNumber<ushort>(value, ushort.TryParse, out converted);
            case TypeCode.UInt32:
                return TryParseNumber<uint>(value, uint.TryParse, out converted);
            case TypeCode.UInt64:
                return TryParseNumber<ulong>(value, ulong.TryParse, out converted);
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
        out object? converted)
    {
        if (parser(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private static bool TryParseDateTime(string? value, out object? converted)
    {
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
        {
            converted = parsed;
            return true;
        }

        converted = null;
        return true;
    }

    private delegate bool TryParseHandler<T>(string? value, out T result);

    private delegate bool NumberTryParseHandler<T>(
        string? value,
        NumberStyles numberStyles,
        IFormatProvider formatProvider,
        out T result);

    private readonly record struct ColumnBinding(int SourceIndex, Type? Type);

    private readonly record struct OutputBinding(int SourceIndex, int OutputIndex, Type? Type);
}
