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
    private enum PredicateColumn
    {
        Unsupported,
        FileName,
        FileExtension,
        DirectoryName
    }

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

    public static bool MatchesFilePredicate(
        SourcePredicateExpression? predicate,
        FileInfo fileInfo)
    {
        return MatchesPredicate(predicate, columnPath => GetFileColumnValue(columnPath, fileInfo));
    }

    public static bool MatchesDirectoryPredicate(
        SourcePredicateExpression? predicate,
        DirectoryInfo directoryInfo)
    {
        return MatchesPredicate(predicate, columnPath => GetDirectoryColumnValue(columnPath, directoryInfo));
    }

    public static bool Matches(SourcePredicateExpression? predicate, object entity)
    {
        return entity switch
        {
            FileEntity file => MatchesFilePredicate(predicate, file.FileInfo),
            DirectoryInfo directoryInfo => MatchesDirectoryPredicate(predicate, directoryInfo),
            _ => false
        };
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
            !TryGetComparisonParts(comparison, out var columnPath, out var literal, out var op) ||
            op != SourcePredicateComparisonOperator.Equal ||
            literal.Value is not string value)
            return false;

        if (ContainsWildcard(value))
            return false;

        return tableName switch
        {
            "file" or "files" => ClassifyFileEntityColumn(columnPath) != PredicateColumn.Unsupported,
            "dlls" => ClassifyDllFileInfoColumn(columnPath) != PredicateColumn.Unsupported,
            "directories" => ClassifyDirectoryColumn(columnPath) == PredicateColumn.DirectoryName,
            _ => false
        };
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
                if (!TryGetComparisonParts(comparison, out var columnPath, out var literal, out _) ||
                    literal.Value is not string value)
                    return;

                switch (ClassifyFileEntityColumn(columnPath))
                {
                    case PredicateColumn.FileExtension:
                        filters.Extension = value;
                        break;
                    case PredicateColumn.FileName:
                        filters.Name = value;
                        break;
                }
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
                if (TryGetComparisonParts(comparison, out var columnPath, out var literal, out _) &&
                    ClassifyDirectoryColumn(columnPath) == PredicateColumn.DirectoryName &&
                    literal.Value is string value)
                    filters.Name = value;
                return;
        }
    }

    private static bool MatchesPredicate(
        SourcePredicateExpression? predicate,
        Func<string, string?> getColumnValue)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                MatchesPredicate(logical.Left, getColumnValue) &&
                MatchesPredicate(logical.Right, getColumnValue),
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.Or } logical =>
                MatchesPredicate(logical.Left, getColumnValue) ||
                MatchesPredicate(logical.Right, getColumnValue),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, getColumnValue),
            _ => false
        };
    }

    private static bool EvaluateComparison(
        SourcePredicateComparison comparison,
        Func<string, string?> getColumnValue)
    {
        if (!TryGetComparisonParts(comparison, out var columnPath, out var literal, out var op) ||
            op != SourcePredicateComparisonOperator.Equal ||
            literal.Value is not string expected)
            return false;

        var actual = getColumnValue(columnPath);
        return actual is not null &&
               string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static string? GetFileColumnValue(string columnPath, FileInfo fileInfo)
    {
        return ClassifyFileEntityColumn(columnPath) switch
        {
            PredicateColumn.FileExtension => fileInfo.Extension,
            PredicateColumn.FileName => fileInfo.Name,
            _ => null
        };
    }

    private static string? GetDirectoryColumnValue(string columnPath, DirectoryInfo directoryInfo)
    {
        return ClassifyDirectoryColumn(columnPath) switch
        {
            PredicateColumn.DirectoryName => directoryInfo.Name,
            _ => null
        };
    }

    private static PredicateColumn ClassifyFileEntityColumn(string columnPath)
    {
        return GetLastPathSegment(columnPath) switch
        {
            var name when name.Equals(nameof(FileEntity.Extension), StringComparison.OrdinalIgnoreCase) =>
                PredicateColumn.FileExtension,
            var name when name.Equals(nameof(FileEntity.Name), StringComparison.OrdinalIgnoreCase) ||
                          name.Equals(nameof(FileEntity.FileName), StringComparison.OrdinalIgnoreCase) =>
                PredicateColumn.FileName,
            _ => PredicateColumn.Unsupported
        };
    }

    private static PredicateColumn ClassifyDllFileInfoColumn(string columnPath)
    {
        if (TryGetQualifiedMember(columnPath, nameof(FileEntity.FileInfo), nameof(FileInfo.Extension)))
            return PredicateColumn.FileExtension;

        if (TryGetQualifiedMember(columnPath, nameof(FileEntity.FileInfo), nameof(FileInfo.Name)))
            return PredicateColumn.FileName;

        return PredicateColumn.Unsupported;
    }

    private static PredicateColumn ClassifyDirectoryColumn(string columnPath)
    {
        return GetLastPathSegment(columnPath).Equals(
                   nameof(DirectoryInfo.Name),
                   StringComparison.OrdinalIgnoreCase)
            ? PredicateColumn.DirectoryName
            : PredicateColumn.Unsupported;
    }

    private static bool TryGetQualifiedMember(
        string columnPath,
        string parentMember,
        string member)
    {
        var lastDot = columnPath.LastIndexOf('.');
        if (lastDot < 0 ||
            !columnPath[(lastDot + 1)..].Equals(member, StringComparison.OrdinalIgnoreCase))
            return false;

        var parentPath = columnPath[..lastDot];
        var parentDot = parentPath.LastIndexOf('.');
        var actualParent = parentDot < 0 ? parentPath : parentPath[(parentDot + 1)..];
        return actualParent.Equals(parentMember, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetComparisonParts(
        SourcePredicateComparison comparison,
        out string columnPath,
        out SourcePredicateLiteral literal,
        out SourcePredicateComparisonOperator op)
    {
        if (comparison.Left is SourcePredicateColumn leftColumn &&
            comparison.Right is SourcePredicateLiteral rightLiteral)
        {
            columnPath = leftColumn.Column.Name;
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn &&
            comparison.Left is SourcePredicateLiteral leftLiteral)
        {
            columnPath = rightColumn.Column.Name;
            literal = leftLiteral;
            op = comparison.Operator;
            return true;
        }

        columnPath = string.Empty;
        literal = null!;
        op = comparison.Operator;
        return false;
    }

    private static string GetLastPathSegment(string name)
    {
        var dotIndex = name.LastIndexOf('.');
        return dotIndex >= 0 ? name[(dotIndex + 1)..] : name;
    }
}
