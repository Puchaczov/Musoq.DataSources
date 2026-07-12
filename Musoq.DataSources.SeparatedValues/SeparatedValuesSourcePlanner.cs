using System;
using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesSourcePlanner
{
    public static SourcePlanResult Plan(SourcePlanRequest request)
    {
        var requiredColumns = request.RequiredColumns ?? [];
        var (acceptedPredicate, residualPredicate) = SplitPredicate(request.Predicate, IsSupportedPredicate);
        var residualOrderBy = request.OrderBy ?? [];
        var acceptsSlice = residualPredicate is null && residualOrderBy.Count == 0;
        var readPlan = new SeparatedValuesReadPlan
        {
            ProjectionAccepted = request.RequiredColumns is not null,
            AcceptedPredicate = acceptedPredicate,
            HasResidualWork = residualPredicate is not null || residualOrderBy.Count > 0
        };

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = requiredColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = SeparatedValuesReadPlan.CreateProperties(readPlan)
            },
            AcceptedColumns = requiredColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Cardinality = CardinalityEstimate.Unknown("Separated values source cardinality depends on file contents."),
            Diagnostics = [],
            ContractDiagnostics = SeparatedValuesReadModifiers.Plan(requiredColumns)
        };
    }

    internal static bool TryGetComparisonParts(
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

    internal static string NormalizeColumnName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }

    private static bool IsSupportedPredicate(SourcePredicateExpression expression)
    {
        return expression is SourcePredicateComparison comparison &&
               TryGetComparisonParts(comparison, out _, out var literal, out var op) &&
               literal.Value is not null &&
               IsSupportedOperator(op);
    }

    private static bool IsSupportedOperator(SourcePredicateComparisonOperator op)
    {
        return op is SourcePredicateComparisonOperator.Equal or
            SourcePredicateComparisonOperator.NotEqual or
            SourcePredicateComparisonOperator.GreaterThan or
            SourcePredicateComparisonOperator.GreaterOrEqual or
            SourcePredicateComparisonOperator.LessThan or
            SourcePredicateComparisonOperator.LessOrEqual;
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

    private static SourcePredicateExpression? CombineAnd(
        SourcePredicateExpression? left,
        SourcePredicateExpression? right)
    {
        return (left, right) switch
        {
            (null, null) => null,
            (not null, null) => left,
            (null, not null) => right,
            _ => new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right)
        };
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
}
