#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.FlatFile;

internal static class FlatFileSourcePlanner
{
    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        if (!name.Equals("file", StringComparison.OrdinalIgnoreCase))
            return SourcePlanResult.RejectAll(request);

        var (acceptedPredicate, residualPredicate) = SplitPredicate(request.Predicate, IsSupportedPredicate);
        var (acceptedOrderBy, residualOrderBy) = SplitNaturalAscendingOrder(request.OrderBy, nameof(FlatFileEntity.LineNumber));
        var acceptsSlice = residualPredicate is null && residualOrderBy.Count == 0;

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = [],
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = acceptedOrderBy,
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = new Dictionary<string, object?>()
            },
            AcceptedColumns = [],
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = acceptedOrderBy,
            ResidualOrderBy = residualOrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Cardinality = CardinalityEstimate.Unknown("Flat file cardinality depends on file contents."),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public static bool Matches(SourcePredicateExpression? predicate, FlatFileEntity entity)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, entity) && Matches(logical.Right, entity),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, entity.LineNumber),
            _ => true
        };
    }

    private static bool IsSupportedPredicate(SourcePredicateExpression expression)
    {
        return expression is SourcePredicateComparison comparison &&
               TryGetComparisonParts(comparison, out var columnName, out var literal, out _) &&
               columnName.Equals(nameof(FlatFileEntity.LineNumber), StringComparison.OrdinalIgnoreCase) &&
               TryGetInt32(literal.Value, out _);
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, int value)
    {
        if (!TryGetComparisonParts(comparison, out _, out var literal, out var op) ||
            !TryGetInt32(literal.Value, out var expected))
            return true;

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

    private static (IReadOnlyList<OrderByExpression> Accepted, IReadOnlyList<OrderByExpression> Residual)
        SplitNaturalAscendingOrder(IReadOnlyList<OrderByExpression>? orderBy, string columnName)
    {
        if (orderBy is null || orderBy.Count == 0)
            return ([], []);

        if (orderBy.Count == 1 &&
            NormalizeColumnName(orderBy[0].Column.Name).Equals(columnName, StringComparison.OrdinalIgnoreCase) &&
            orderBy[0].Direction == OrderDirection.Ascending)
            return (orderBy, []);

        return ([], orderBy);
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
}
