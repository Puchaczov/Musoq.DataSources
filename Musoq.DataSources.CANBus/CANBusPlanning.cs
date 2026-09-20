#nullable enable

using System;
using System.Collections.Generic;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.CANBus.Messages;
using Musoq.DataSources.CANBus.Signals;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus;

internal static class CANBusSourcePlanner
{
    public static SourcePlanResult PlanMessages(SourcePlanRequest request)
    {
        return PlanPredicateOnly(
            request,
            predicate => SplitPredicate(predicate, IsSupportedMessagePredicate),
            "CAN bus message cardinality depends on DBC contents.");
    }

    public static SourcePlanResult PlanSignals(SourcePlanRequest request)
    {
        return PlanPredicateOnly(
            request,
            predicate => SplitPredicate(predicate, IsSupportedSignalPredicate),
            "CAN bus signal cardinality depends on DBC contents.");
    }

    public static bool MatchesFrame(SourcePredicateExpression? predicate, SourceCanFrame frame)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                MatchesFrame(logical.Left, frame) && MatchesFrame(logical.Right, frame),
            SourcePredicateComparison comparison => EvaluateFrameComparison(comparison, frame),
            _ => true
        };
    }

    public static bool MatchesMessage(SourcePredicateExpression? predicate, MessageEntity entity)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                MatchesMessage(logical.Left, entity) && MatchesMessage(logical.Right, entity),
            SourcePredicateComparison comparison => EvaluateMessageComparison(comparison, entity),
            _ => true
        };
    }

    public static bool MatchesSignal(SourcePredicateExpression? predicate, SignalEntity entity)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                MatchesSignal(logical.Left, entity) && MatchesSignal(logical.Right, entity),
            SourcePredicateComparison comparison => EvaluateSignalComparison(comparison, entity),
            _ => true
        };
    }

    public static (SourcePredicateExpression? Accepted, SourcePredicateExpression? Residual) SplitFramePredicate(
        SourcePredicateExpression? predicate)
    {
        return SplitPredicate(predicate, IsSupportedFramePredicate);
    }

    private static SourcePlanResult PlanPredicateOnly(
        SourcePlanRequest request,
        Func<SourcePredicateExpression?, (SourcePredicateExpression? Accepted, SourcePredicateExpression? Residual)> splitPredicate,
        string cardinalityReason)
    {
        var (acceptedPredicate, residualPredicate) = splitPredicate(request.Predicate);

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = [],
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                Properties = new Dictionary<string, object?>()
            },
            AcceptedColumns = [],
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = request.OrderBy ?? [],
            ResidualSkip = request.Skip,
            ResidualTake = request.Take,
            Cardinality = CardinalityEstimate.Unknown(cardinalityReason),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    private static bool IsSupportedFramePredicate(SourcePredicateExpression expression)
    {
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        return columnName switch
        {
            "ID" => TryGetUInt32(literal.Value, out _),
            "Timestamp" => TryGetUInt64(literal.Value, out _),
            "DLC" => TryGetByte(literal.Value, out _),
            "IsWellKnown" => IsEqualityOperator(op) && literal.Value is bool,
            _ => false
        };
    }

    private static bool IsSupportedMessagePredicate(SourcePredicateExpression expression)
    {
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        return columnName switch
        {
            nameof(MessageEntity.Id) => TryGetUInt32(literal.Value, out _),
            nameof(MessageEntity.IsExtId) => IsEqualityOperator(op) && literal.Value is bool,
            nameof(MessageEntity.Name) => IsEqualityOperator(op) && literal.Value is string,
            nameof(MessageEntity.DLC) => TryGetUInt16(literal.Value, out _),
            nameof(MessageEntity.Transmitter) => IsEqualityOperator(op) && literal.Value is string,
            _ => false
        };
    }

    private static bool IsSupportedSignalPredicate(SourcePredicateExpression expression)
    {
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        return columnName switch
        {
            nameof(SignalEntity.Id) => TryGetUInt32(literal.Value, out _),
            nameof(SignalEntity.Name) => IsEqualityOperator(op) && literal.Value is string,
            nameof(SignalEntity.MessageName) => IsEqualityOperator(op) && literal.Value is string,
            nameof(SignalEntity.MessageOrder) => TryGetInt32(literal.Value, out _),
            _ => false
        };
    }

    private static bool EvaluateFrameComparison(SourcePredicateComparison comparison, SourceCanFrame frame)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        return columnName switch
        {
            "ID" when TryGetUInt32(literal.Value, out var expected) => Compare(frame.Frame.Id, expected, op),
            "Timestamp" when TryGetUInt64(literal.Value, out var expected) => Compare(frame.Timestamp, expected, op),
            "DLC" when frame.Dlc.HasValue && TryGetByte(literal.Value, out var expected) =>
                Compare(frame.Dlc.Value, expected, op),
            "IsWellKnown" when literal.Value is bool expected => op switch
            {
                SourcePredicateComparisonOperator.Equal => (frame.Message is not null) == expected,
                SourcePredicateComparisonOperator.NotEqual => (frame.Message is not null) != expected,
                _ => false
            },
            _ => true
        };
    }

    private static bool EvaluateMessageComparison(SourcePredicateComparison comparison, MessageEntity entity)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        return columnName switch
        {
            nameof(MessageEntity.Id) when TryGetUInt32(literal.Value, out var expected) =>
                Compare(entity.Id, expected, op),
            nameof(MessageEntity.IsExtId) when literal.Value is bool expected =>
                Compare(entity.IsExtId, expected, op),
            nameof(MessageEntity.Name) when literal.Value is string expected =>
                Compare(entity.Name, expected, op),
            nameof(MessageEntity.DLC) when TryGetUInt16(literal.Value, out var expected) =>
                Compare(entity.DLC, expected, op),
            nameof(MessageEntity.Transmitter) when literal.Value is string expected =>
                Compare(entity.Transmitter, expected, op),
            _ => true
        };
    }

    private static bool EvaluateSignalComparison(SourcePredicateComparison comparison, SignalEntity entity)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        return columnName switch
        {
            nameof(SignalEntity.Id) when TryGetUInt32(literal.Value, out var expected) =>
                Compare(entity.Id, expected, op),
            nameof(SignalEntity.Name) when literal.Value is string expected =>
                Compare(entity.Name, expected, op),
            nameof(SignalEntity.MessageName) when literal.Value is string expected =>
                Compare(entity.MessageName, expected, op),
            nameof(SignalEntity.MessageOrder) when TryGetInt32(literal.Value, out var expected) =>
                Compare(entity.MessageOrder, expected, op),
            _ => true
        };
    }

    private static bool Compare(ulong value, ulong expected, SourcePredicateComparisonOperator op)
    {
        var compare = value.CompareTo(expected);

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

    private static bool Compare(uint value, uint expected, SourcePredicateComparisonOperator op)
    {
        var compare = value.CompareTo(expected);

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

    private static bool Compare(ushort value, ushort expected, SourcePredicateComparisonOperator op)
    {
        var compare = value.CompareTo(expected);

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

    private static bool Compare(byte value, byte expected, SourcePredicateComparisonOperator op)
    {
        var compare = value.CompareTo(expected);

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

    private static bool Compare(int value, int expected, SourcePredicateComparisonOperator op)
    {
        var compare = value.CompareTo(expected);

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

    private static bool Compare(string value, string expected, SourcePredicateComparisonOperator op)
    {
        var equals = string.Equals(value, expected, StringComparison.Ordinal);

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => equals,
            SourcePredicateComparisonOperator.NotEqual => !equals,
            _ => false
        };
    }

    private static bool Compare(bool value, bool expected, SourcePredicateComparisonOperator op)
    {
        return op switch
        {
            SourcePredicateComparisonOperator.Equal => value == expected,
            SourcePredicateComparisonOperator.NotEqual => value != expected,
            _ => false
        };
    }

    private static (SourcePredicateExpression? Accepted, SourcePredicateExpression? Residual) SplitPredicate(
        SourcePredicateExpression? predicate,
        Func<SourcePredicateExpression, bool> canAccept)
    {
        if (predicate is null)
            return (null, null);

        if (predicate is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical)
        {
            var left = SplitPredicate(logical.Left, canAccept);
            var right = SplitPredicate(logical.Right, canAccept);

            return (CombineAnd(left.Accepted, right.Accepted), CombineAnd(left.Residual, right.Residual));
        }

        return canAccept(predicate) ? (predicate, null) : (null, predicate);
    }

    private static SourcePredicateExpression? CombineAnd(SourcePredicateExpression? left, SourcePredicateExpression? right)
    {
        return (left, right) switch
        {
            (null, null) => null,
            (not null, null) => left,
            (null, not null) => right,
            _ => new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right)
        };
    }

    private static bool TryGetComparisonParts(
        SourcePredicateComparison comparison,
        out string columnName,
        out SourcePredicateLiteral literal,
        out SourcePredicateComparisonOperator op)
    {
        if (comparison.Left is SourcePredicateColumn leftColumn &&
            comparison.Right is SourcePredicateLiteral rightLiteral)
        {
            columnName = NormalizeColumnName(leftColumn.Column.Name);
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn &&
            comparison.Left is SourcePredicateLiteral leftLiteral)
        {
            columnName = NormalizeColumnName(rightColumn.Column.Name);
            literal = leftLiteral;
            op = Invert(comparison.Operator);
            return true;
        }

        columnName = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    private static SourcePredicateComparisonOperator Invert(SourcePredicateComparisonOperator op)
    {
        return op switch
        {
            SourcePredicateComparisonOperator.GreaterThan => SourcePredicateComparisonOperator.LessThan,
            SourcePredicateComparisonOperator.GreaterOrEqual => SourcePredicateComparisonOperator.LessOrEqual,
            SourcePredicateComparisonOperator.LessThan => SourcePredicateComparisonOperator.GreaterThan,
            SourcePredicateComparisonOperator.LessOrEqual => SourcePredicateComparisonOperator.GreaterOrEqual,
            _ => op
        };
    }

    private static string NormalizeColumnName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }

    private static bool TryGetUInt32(object? value, out uint number)
    {
        switch (value)
        {
            case byte or ushort or uint:
                number = Convert.ToUInt32(value);
                return true;
            case sbyte or short or int or long:
                var signed = Convert.ToInt64(value);
                if (signed < uint.MinValue || signed > uint.MaxValue)
                {
                    number = default;
                    return false;
                }

                number = (uint)signed;
                return true;
            default:
                return uint.TryParse(value?.ToString(), out number);
        }
    }

    private static bool TryGetUInt64(object? value, out ulong number)
    {
        switch (value)
        {
            case byte or ushort or uint or ulong:
                number = Convert.ToUInt64(value);
                return true;
            case sbyte or short or int or long:
                var signed = Convert.ToInt64(value);
                if (signed < 0)
                {
                    number = default;
                    return false;
                }

                number = (ulong)signed;
                return true;
            default:
                return ulong.TryParse(value?.ToString(), out number);
        }
    }

    private static bool TryGetUInt16(object? value, out ushort number)
    {
        switch (value)
        {
            case byte or ushort:
                number = Convert.ToUInt16(value);
                return true;
            case sbyte or short or int or long:
                var signed = Convert.ToInt64(value);
                if (signed < ushort.MinValue || signed > ushort.MaxValue)
                {
                    number = default;
                    return false;
                }

                number = (ushort)signed;
                return true;
            default:
                return ushort.TryParse(value?.ToString(), out number);
        }
    }

    private static bool TryGetByte(object? value, out byte number)
    {
        switch (value)
        {
            case byte:
                number = (byte)value;
                return true;
            case sbyte or short or ushort or int or long:
                var signed = Convert.ToInt64(value);
                if (signed < byte.MinValue || signed > byte.MaxValue)
                {
                    number = default;
                    return false;
                }

                number = (byte)signed;
                return true;
            default:
                return byte.TryParse(value?.ToString(), out number);
        }
    }

    private static bool TryGetInt32(object? value, out int number)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int:
                number = Convert.ToInt32(value);
                return true;
            default:
                return int.TryParse(value?.ToString(), out number);
        }
    }

    private static bool IsEqualityOperator(SourcePredicateComparisonOperator op)
    {
        return op is SourcePredicateComparisonOperator.Equal or SourcePredicateComparisonOperator.NotEqual;
    }
}
