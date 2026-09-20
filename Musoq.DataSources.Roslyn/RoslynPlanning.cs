using System;
using System.Collections.Generic;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn;

internal sealed class RoslynFilterParameters
{
    public string? AssemblyName { get; set; }
    public string? Name { get; set; }
    public string? Language { get; set; }
    public string? DefaultNamespace { get; set; }
}

internal static class RoslynSourcePlanner
{
    public const string FiltersPropertyName = "RoslynFilters";

    public static SourcePlanResult Plan(SourcePlanRequest request)
    {
        var (acceptedPredicate, residualPredicate) = SplitPredicate(request.Predicate, IsSupported);
        var filters = ExtractFilters(acceptedPredicate);

        return BuildPlanResult(
            request,
            acceptedPredicate,
            residualPredicate,
            new Dictionary<string, object?>
            {
                [FiltersPropertyName] = filters
            });
    }

    public static RoslynFilterParameters GetFilters(SourceExecutionPlan plan)
    {
        return plan.Properties is not null &&
               plan.Properties.TryGetValue(FiltersPropertyName, out var value) &&
               value is RoslynFilterParameters filters
            ? filters
            : new RoslynFilterParameters();
    }

    public static bool Matches(SourcePredicateExpression? predicate, ProjectEntity project)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, project) && Matches(logical.Right, project),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, project),
            _ => true
        };
    }

    public static bool Matches(RoslynFilterParameters filters, ProjectEntity project)
    {
        return Matches(filters.AssemblyName, project.AssemblyName) &&
               Matches(filters.Name, project.Name) &&
               Matches(filters.Language, project.Language) &&
               Matches(filters.DefaultNamespace, project.DefaultNamespace);
    }

    private static SourcePlanResult BuildPlanResult(
        SourcePlanRequest request,
        SourcePredicateExpression? acceptedPredicate,
        SourcePredicateExpression? residualPredicate,
        IReadOnlyDictionary<string, object?> properties)
    {
        var residualOrderBy = request.OrderBy ?? [];

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = [],
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                Properties = properties
            },
            AcceptedColumns = [],
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            ResidualSkip = request.Skip,
            ResidualTake = request.Take,
            Cardinality = CardinalityEstimate.Unknown("Roslyn source cardinality depends on solution contents."),
            Diagnostics = [],
            ContractDiagnostics = []
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

            return (
                CombineAnd(left.Accepted, right.Accepted),
                CombineAnd(left.Residual, right.Residual));
        }

        return canAccept(predicate)
            ? (predicate, null)
            : (null, predicate);
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

    private static bool IsSupported(SourcePredicateExpression expression)
    {
        return expression is SourcePredicateComparison comparison &&
               TryGetComparisonParts(comparison, out var columnName, out var originalColumnName, out var literal, out var op) &&
               op == SourcePredicateComparisonOperator.Equal &&
               literal.Value is string &&
               IsProjectColumn(columnName, originalColumnName);
    }

    private static bool IsProjectColumn(string columnName, string originalColumnName)
    {
        if (columnName.Equals(nameof(ProjectEntity.Name), StringComparison.OrdinalIgnoreCase) &&
            !originalColumnName.StartsWith("p.", StringComparison.OrdinalIgnoreCase))
            return false;

        return columnName.Equals(nameof(ProjectEntity.AssemblyName), StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals(nameof(ProjectEntity.Name), StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals(nameof(ProjectEntity.Language), StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals(nameof(ProjectEntity.DefaultNamespace), StringComparison.OrdinalIgnoreCase);
    }

    private static RoslynFilterParameters ExtractFilters(SourcePredicateExpression? predicate)
    {
        var filters = new RoslynFilterParameters();
        ExtractFilters(predicate, filters);
        return filters;
    }

    private static void ExtractFilters(SourcePredicateExpression? predicate, RoslynFilterParameters filters)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                ExtractFilters(logical.Left, filters);
                ExtractFilters(logical.Right, filters);
                return;
            case SourcePredicateComparison comparison:
                if (!TryGetComparisonParts(comparison, out var columnName, out _, out var literal, out _) ||
                    literal.Value is not string value)
                    return;

                if (columnName.Equals(nameof(ProjectEntity.AssemblyName), StringComparison.OrdinalIgnoreCase))
                    filters.AssemblyName = value;
                else if (columnName.Equals(nameof(ProjectEntity.Name), StringComparison.OrdinalIgnoreCase))
                    filters.Name = value;
                else if (columnName.Equals(nameof(ProjectEntity.Language), StringComparison.OrdinalIgnoreCase))
                    filters.Language = value;
                else if (columnName.Equals(nameof(ProjectEntity.DefaultNamespace), StringComparison.OrdinalIgnoreCase))
                    filters.DefaultNamespace = value;
                return;
        }
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, ProjectEntity project)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out _, out var literal, out var op))
            return true;

        var left = GetColumnValue(project, columnName);
        var right = literal.Value;

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => Equals(left, right),
            SourcePredicateComparisonOperator.NotEqual => !Equals(left, right),
            _ => true
        };
    }

    private static object? GetColumnValue(ProjectEntity project, string columnName)
    {
        if (columnName.Equals(nameof(ProjectEntity.AssemblyName), StringComparison.OrdinalIgnoreCase))
            return project.AssemblyName;

        if (columnName.Equals(nameof(ProjectEntity.Name), StringComparison.OrdinalIgnoreCase))
            return project.Name;

        if (columnName.Equals(nameof(ProjectEntity.Language), StringComparison.OrdinalIgnoreCase))
            return project.Language;

        return columnName.Equals(nameof(ProjectEntity.DefaultNamespace), StringComparison.OrdinalIgnoreCase)
            ? project.DefaultNamespace
            : null;
    }

    private static bool TryGetComparisonParts(
        SourcePredicateComparison comparison,
        out string columnName,
        out string originalColumnName,
        out SourcePredicateLiteral literal,
        out SourcePredicateComparisonOperator op)
    {
        if (comparison.Left is SourcePredicateColumn leftColumn &&
            comparison.Right is SourcePredicateLiteral rightLiteral)
        {
            originalColumnName = leftColumn.Column.Name;
            columnName = NormalizeColumnName(originalColumnName);
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn &&
            comparison.Left is SourcePredicateLiteral leftLiteral)
        {
            originalColumnName = rightColumn.Column.Name;
            columnName = NormalizeColumnName(originalColumnName);
            literal = leftLiteral;
            op = comparison.Operator;
            return true;
        }

        columnName = string.Empty;
        originalColumnName = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    private static string NormalizeColumnName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }

    private static bool Matches(string? expected, string? actual)
    {
        return expected is null || string.Equals(actual, expected, StringComparison.Ordinal);
    }
}
