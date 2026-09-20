#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Archives;

internal static class ArchivesSourcePlanner
{
    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        if (!name.Equals("file", StringComparison.OrdinalIgnoreCase))
            return SourcePlanResult.RejectAll(request);

        var (acceptedPredicate, residualPredicate) = SplitPredicate(request.Predicate, IsSupportedPredicate);

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
            Cardinality = CardinalityEstimate.Unknown("Archive cardinality depends on archive contents."),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public static bool Matches(SourcePredicateExpression? predicate, EntryWrapper entity)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, entity) && Matches(logical.Right, entity),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, entity),
            _ => true
        };
    }

    private static bool IsSupportedPredicate(SourcePredicateExpression expression)
    {
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        return columnName switch
        {
            nameof(EntryWrapper.Key) => op == SourcePredicateComparisonOperator.Equal && literal.Value is string,
            nameof(EntryWrapper.IsDirectory) => op == SourcePredicateComparisonOperator.Equal && literal.Value is bool,
            nameof(EntryWrapper.Size) => TryGetInt64(literal.Value, out _),
            _ => false
        };
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, EntryWrapper entity)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        return columnName switch
        {
            nameof(EntryWrapper.Key) when literal.Value is string expected =>
                op switch
                {
                    SourcePredicateComparisonOperator.Equal => string.Equals(entity.Key, expected, StringComparison.Ordinal),
                    SourcePredicateComparisonOperator.NotEqual => !string.Equals(entity.Key, expected, StringComparison.Ordinal),
                    _ => false
                },
            nameof(EntryWrapper.IsDirectory) when literal.Value is bool expected =>
                op switch
                {
                    SourcePredicateComparisonOperator.Equal => entity.IsDirectory == expected,
                    SourcePredicateComparisonOperator.NotEqual => entity.IsDirectory != expected,
                    _ => false
                },
            nameof(EntryWrapper.Size) when TryGetInt64(literal.Value, out var expected) =>
                Compare(entity.Size, expected, op),
            _ => true
        };
    }

    private static bool Compare(long value, long expected, SourcePredicateComparisonOperator op)
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

    private static bool TryGetInt64(object? value, out long number)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long:
                number = Convert.ToInt64(value);
                return true;
            default:
                return long.TryParse(value?.ToString(), out number);
        }
    }
}
