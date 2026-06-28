using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira;

internal static class JiraSourcePlanner
{
    public const string FiltersPropertyName = "JiraFilters";

    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        if (!name.Equals("issues", StringComparison.OrdinalIgnoreCase))
            return SourcePlanResult.RejectAll(request);

        var (acceptedPredicate, residualPredicate) = SplitPredicate(request.Predicate, IsSupported);
        var filters = ExtractFilters(acceptedPredicate);
        var acceptsSlice = residualPredicate is null && (request.OrderBy?.Count ?? 0) == 0;
        var residualOrderBy = request.OrderBy ?? [];

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = [],
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = new Dictionary<string, object?>
                {
                    [FiltersPropertyName] = filters
                }
            },
            AcceptedColumns = [],
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Cardinality = CardinalityEstimate.Unknown("Jira API cardinality depends on remote project state."),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public static JiraFilterParameters GetFilters(SourceExecutionPlan plan)
    {
        return plan.Properties is not null &&
               plan.Properties.TryGetValue(FiltersPropertyName, out var value) &&
               value is JiraFilterParameters filters
            ? filters
            : new JiraFilterParameters();
    }

    public static IReadOnlyList<IJiraIssue> ApplyAcceptedPlan(
        IEnumerable<IJiraIssue> issues,
        SourceExecutionPlan plan,
        ref long skipped,
        ref long emitted)
    {
        var result = new List<IJiraIssue>();

        foreach (var issue in issues)
        {
            if (!Matches(plan.AcceptedPredicate, issue))
                continue;

            if (plan.AcceptedSkip.HasValue && skipped < plan.AcceptedSkip.Value)
            {
                skipped++;
                continue;
            }

            if (plan.AcceptedTake.HasValue && emitted >= plan.AcceptedTake.Value)
                break;

            result.Add(issue);
            emitted++;
        }

        return result;
    }

    public static bool IsTakeSatisfied(SourceExecutionPlan plan, long emitted)
    {
        return plan.AcceptedTake.HasValue && emitted >= plan.AcceptedTake.Value;
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
        if (expression is not SourcePredicateComparison comparison ||
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        if (op == SourcePredicateComparisonOperator.Equal &&
            literal.Value is string &&
            (columnName.Equals(nameof(IJiraIssue.Status), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.Type), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.Priority), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.Resolution), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.Assignee), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.AssigneeDisplayName), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.Reporter), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.ReporterDisplayName), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.ProjectKey), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.Key), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IJiraIssue.ParentKey), StringComparison.OrdinalIgnoreCase)))
            return true;

        return (op is SourcePredicateComparisonOperator.GreaterThan
                    or SourcePredicateComparisonOperator.GreaterOrEqual
                    or SourcePredicateComparisonOperator.LessThan
                    or SourcePredicateComparisonOperator.LessOrEqual) &&
               (columnName.Equals(nameof(IJiraIssue.CreatedAt), StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals(nameof(IJiraIssue.UpdatedAt), StringComparison.OrdinalIgnoreCase)) &&
               TryGetDateTimeOffset(literal.Value, out _);
    }

    private static JiraFilterParameters ExtractFilters(SourcePredicateExpression? predicate)
    {
        var filters = new JiraFilterParameters();
        ExtractFilters(predicate, filters);
        return filters;
    }

    private static void ExtractFilters(SourcePredicateExpression? predicate, JiraFilterParameters filters)
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
                ApplyComparison(comparison, filters);
                return;
        }
    }

    private static void ApplyComparison(SourcePredicateComparison comparison, JiraFilterParameters filters)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return;

        switch (columnName, op, literal.Value)
        {
            case (nameof(IJiraIssue.Status), SourcePredicateComparisonOperator.Equal, string value):
                filters.Status = value;
                break;
            case (nameof(IJiraIssue.Type), SourcePredicateComparisonOperator.Equal, string value):
                filters.Type = value;
                break;
            case (nameof(IJiraIssue.Priority), SourcePredicateComparisonOperator.Equal, string value):
                filters.Priority = value;
                break;
            case (nameof(IJiraIssue.Resolution), SourcePredicateComparisonOperator.Equal, string value):
                filters.Resolution = value;
                break;
            case (nameof(IJiraIssue.Assignee), SourcePredicateComparisonOperator.Equal, string value):
                filters.Assignee = value;
                break;
            case (nameof(IJiraIssue.AssigneeDisplayName), SourcePredicateComparisonOperator.Equal, string value):
                filters.AssigneeDisplayName = value;
                break;
            case (nameof(IJiraIssue.Reporter), SourcePredicateComparisonOperator.Equal, string value):
                filters.Reporter = value;
                break;
            case (nameof(IJiraIssue.ReporterDisplayName), SourcePredicateComparisonOperator.Equal, string value):
                filters.ReporterDisplayName = value;
                break;
            case (nameof(IJiraIssue.ProjectKey), SourcePredicateComparisonOperator.Equal, string value):
                filters.ProjectKey = value;
                break;
            case (nameof(IJiraIssue.Key), SourcePredicateComparisonOperator.Equal, string value):
                filters.Key = value;
                break;
            case (nameof(IJiraIssue.ParentKey), SourcePredicateComparisonOperator.Equal, string value):
                filters.ParentKey = value;
                break;
        }

        if (!TryGetDateTimeOffset(literal.Value, out var date))
            return;

        switch (columnName, op)
        {
            case (nameof(IJiraIssue.CreatedAt), SourcePredicateComparisonOperator.GreaterThan):
                filters.SetCreatedAfter(date, false);
                break;
            case (nameof(IJiraIssue.CreatedAt), SourcePredicateComparisonOperator.GreaterOrEqual):
                filters.SetCreatedAfter(date, true);
                break;
            case (nameof(IJiraIssue.CreatedAt), SourcePredicateComparisonOperator.LessThan):
                filters.SetCreatedBefore(date, false);
                break;
            case (nameof(IJiraIssue.CreatedAt), SourcePredicateComparisonOperator.LessOrEqual):
                filters.SetCreatedBefore(date, true);
                break;
            case (nameof(IJiraIssue.UpdatedAt), SourcePredicateComparisonOperator.GreaterThan):
                filters.SetUpdatedAfter(date, false);
                break;
            case (nameof(IJiraIssue.UpdatedAt), SourcePredicateComparisonOperator.GreaterOrEqual):
                filters.SetUpdatedAfter(date, true);
                break;
            case (nameof(IJiraIssue.UpdatedAt), SourcePredicateComparisonOperator.LessThan):
                filters.SetUpdatedBefore(date, false);
                break;
            case (nameof(IJiraIssue.UpdatedAt), SourcePredicateComparisonOperator.LessOrEqual):
                filters.SetUpdatedBefore(date, true);
                break;
        }
    }

    private static bool Matches(SourcePredicateExpression? predicate, IJiraIssue issue)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, issue) && Matches(logical.Right, issue),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, issue),
            _ => true
        };
    }

    private static bool EvaluateComparison(SourcePredicateComparison comparison, IJiraIssue issue)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        var left = GetColumnValue(issue, columnName);
        var right = literal.Value;

        if (left is DateTimeOffset && TryGetDateTimeOffset(right, out var date))
            right = date;

        var compare = Compare(left, right);

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => Equals(left, right),
            SourcePredicateComparisonOperator.NotEqual => !Equals(left, right),
            SourcePredicateComparisonOperator.GreaterThan => compare > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => compare >= 0,
            SourcePredicateComparisonOperator.LessThan => compare < 0,
            SourcePredicateComparisonOperator.LessOrEqual => compare <= 0,
            _ => false
        };
    }

    private static object? GetColumnValue(IJiraIssue issue, string columnName)
    {
        return columnName switch
        {
            nameof(IJiraIssue.Key) => issue.Key,
            nameof(IJiraIssue.Type) => issue.Type,
            nameof(IJiraIssue.Status) => issue.Status,
            nameof(IJiraIssue.Priority) => issue.Priority,
            nameof(IJiraIssue.Resolution) => issue.Resolution,
            nameof(IJiraIssue.Assignee) => issue.Assignee,
            nameof(IJiraIssue.AssigneeDisplayName) => issue.AssigneeDisplayName,
            nameof(IJiraIssue.Reporter) => issue.Reporter,
            nameof(IJiraIssue.ReporterDisplayName) => issue.ReporterDisplayName,
            nameof(IJiraIssue.ProjectKey) => issue.ProjectKey,
            nameof(IJiraIssue.CreatedAt) => issue.CreatedAt,
            nameof(IJiraIssue.UpdatedAt) => issue.UpdatedAt,
            nameof(IJiraIssue.ParentKey) => issue.ParentKey,
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

    private static bool TryGetDateTimeOffset(object? value, out DateTimeOffset date)
    {
        switch (value)
        {
            case DateTimeOffset dateTimeOffset:
                date = dateTimeOffset;
                return true;
            case DateTime dateTime:
                date = dateTime;
                return true;
            case string text:
                return DateTimeOffset.TryParse(text, out date);
            default:
                date = default;
                return false;
        }
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
            return -1;

        return left is IComparable comparable ? comparable.CompareTo(right) : 0;
    }
}
