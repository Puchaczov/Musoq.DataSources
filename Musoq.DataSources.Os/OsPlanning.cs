using System;
using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Files;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os;

internal sealed class OsFileFilterParameters
{
    public string? Extension { get; set; }
    public string? Name { get; set; }

    public string? GetSearchPattern()
    {
        if (Name is not null)
            return Name;

        if (Extension is null)
            return null;

        return Extension.StartsWith('*') ? Extension : $"*{Extension}";
    }
}

internal sealed class OsDirectoryFilterParameters
{
    public string? Name { get; set; }
}

internal static class OsSourcePlanner
{
    public const string FileFiltersPropertyName = "OsFileFilters";
    public const string DirectoryFiltersPropertyName = "OsDirectoryFilters";

    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        var tableName = name.ToLowerInvariant();
        var (acceptedPredicate, residualPredicate) = SplitPredicate(
            request.Predicate,
            expression => IsSupported(tableName, expression));

        var properties = new Dictionary<string, object?>();
        switch (tableName)
        {
            case "files":
            case "dlls":
            case "metadata":
                properties[FileFiltersPropertyName] = ExtractFileFilters(acceptedPredicate);
                break;
            case "directories":
                properties[DirectoryFiltersPropertyName] = ExtractDirectoryFilters(acceptedPredicate);
                break;
        }

        return BuildPlanResult(request, acceptedPredicate, residualPredicate, properties);
    }

    public static OsFileFilterParameters GetFileFilters(SourceExecutionPlan plan)
    {
        return plan.Properties is not null &&
               plan.Properties.TryGetValue(FileFiltersPropertyName, out var value) &&
               value is OsFileFilterParameters filters
            ? filters
            : new OsFileFilterParameters();
    }

    public static OsDirectoryFilterParameters GetDirectoryFilters(SourceExecutionPlan plan)
    {
        return plan.Properties is not null &&
               plan.Properties.TryGetValue(DirectoryFiltersPropertyName, out var value) &&
               value is OsDirectoryFilterParameters filters
            ? filters
            : new OsDirectoryFilterParameters();
    }

    public static bool Matches(SourcePredicateExpression? predicate, object entity)
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

    private static SourcePlanResult BuildPlanResult(
        SourcePlanRequest request,
        SourcePredicateExpression? acceptedPredicate,
        SourcePredicateExpression? residualPredicate,
        IReadOnlyDictionary<string, object?> properties)
    {
        var requiredColumns = request.RequiredColumns ?? [];
        var residualOrderBy = request.OrderBy ?? [];

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = requiredColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                Properties = properties
            },
            AcceptedColumns = requiredColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            ResidualSkip = request.Skip,
            ResidualTake = request.Take,
            Cardinality = CardinalityEstimate.Unknown("OS source cardinality depends on filesystem contents."),
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

    private static bool IsSupported(string tableName, SourcePredicateExpression expression)
    {
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op) ||
            op != SourcePredicateComparisonOperator.Equal ||
            literal.Value is not string value)
            return false;

        if (tableName is "files" or "dlls" or "metadata")
        {
            return columnName.Equals(nameof(FileEntity.Extension), StringComparison.OrdinalIgnoreCase) ||
                   (columnName.Equals(nameof(FileEntity.Name), StringComparison.OrdinalIgnoreCase) ||
                    columnName.Equals(nameof(FileEntity.FileName), StringComparison.OrdinalIgnoreCase)) &&
                   !ContainsWildcard(value);
        }

        return tableName == "directories" &&
               columnName.Equals(nameof(DirectoryInfo.Name), StringComparison.OrdinalIgnoreCase) &&
               !ContainsWildcard(value);
    }

    private static bool ContainsWildcard(string value)
    {
        return value.Contains('*') || value.Contains('?');
    }

    private static OsFileFilterParameters ExtractFileFilters(SourcePredicateExpression? predicate)
    {
        var filters = new OsFileFilterParameters();
        ExtractFileFilters(predicate, filters);
        return filters;
    }

    private static void ExtractFileFilters(SourcePredicateExpression? predicate, OsFileFilterParameters filters)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                ExtractFileFilters(logical.Left, filters);
                ExtractFileFilters(logical.Right, filters);
                return;
            case SourcePredicateComparison comparison:
                if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out _) ||
                    literal.Value is not string value)
                    return;

                if (columnName.Equals(nameof(FileEntity.Extension), StringComparison.OrdinalIgnoreCase))
                    filters.Extension = value;
                else if (columnName.Equals(nameof(FileEntity.Name), StringComparison.OrdinalIgnoreCase) ||
                         columnName.Equals(nameof(FileEntity.FileName), StringComparison.OrdinalIgnoreCase))
                    filters.Name = value;
                return;
        }
    }

    private static OsDirectoryFilterParameters ExtractDirectoryFilters(SourcePredicateExpression? predicate)
    {
        var filters = new OsDirectoryFilterParameters();
        ExtractDirectoryFilters(predicate, filters);
        return filters;
    }

    private static void ExtractDirectoryFilters(
        SourcePredicateExpression? predicate,
        OsDirectoryFilterParameters filters)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                ExtractDirectoryFilters(logical.Left, filters);
                ExtractDirectoryFilters(logical.Right, filters);
                return;
            case SourcePredicateComparison comparison:
                if (TryGetComparisonParts(comparison, out var columnName, out var literal, out _) &&
                    columnName.Equals(nameof(DirectoryInfo.Name), StringComparison.OrdinalIgnoreCase) &&
                    literal.Value is string value)
                    filters.Name = value;
                return;
        }
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, object entity)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        var left = GetColumnValue(entity, columnName);
        var right = literal.Value;

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => Equals(left, right),
            SourcePredicateComparisonOperator.NotEqual => !Equals(left, right),
            _ => true
        };
    }

    private static object? GetColumnValue(object entity, string columnName)
    {
        return entity switch
        {
            FileEntity file => columnName switch
            {
                nameof(FileEntity.Extension) => file.Extension,
                nameof(FileEntity.Name) => file.Name,
                nameof(FileEntity.FileName) => file.FileName,
                _ => null
            },
            DirectoryInfo directoryInfo => columnName switch
            {
                nameof(DirectoryInfo.Name) => directoryInfo.Name,
                _ => null
            },
            _ => null
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
            op = comparison.Operator;
            return true;
        }

        columnName = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    private static string NormalizeColumnName(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }
}
