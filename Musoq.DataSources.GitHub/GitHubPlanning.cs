using System;
using System.Collections.Generic;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.Optimization;
using Octokit;

namespace Musoq.DataSources.GitHub;

internal sealed class GitHubFilterParameters
{
    public string? State { get; set; }
    public string? AuthorLogin { get; set; }
    public string? AuthorName { get; set; }
    public string? AssigneeLogin { get; set; }
    public string? MilestoneTitle { get; set; }
    public string? HeadRef { get; set; }
    public string? BaseRef { get; set; }
    public string? Sha { get; set; }
    public string? Language { get; set; }
    public string? Visibility { get; set; }
    public bool? IsArchived { get; set; }
    public bool? IsFork { get; set; }
    public DateTimeOffset? CreatedAfter { get; private set; }
    public bool CreatedAfterInclusive { get; private set; }
    public DateTimeOffset? UpdatedAfter { get; private set; }
    public bool UpdatedAfterInclusive { get; private set; }
    public DateTimeOffset? AuthorDateAfter { get; private set; }
    public bool AuthorDateAfterInclusive { get; private set; }
    public DateTimeOffset? AuthorDateBefore { get; private set; }
    public bool AuthorDateBeforeInclusive { get; private set; }
    public DateTimeOffset? CommitterDateAfter { get; private set; }
    public bool CommitterDateAfterInclusive { get; private set; }
    public DateTimeOffset? CommitterDateBefore { get; private set; }
    public bool CommitterDateBeforeInclusive { get; private set; }

    public void SetCreatedAfter(DateTimeOffset value, bool inclusive)
    {
        (CreatedAfter, CreatedAfterInclusive) = SelectLower(CreatedAfter, CreatedAfterInclusive, value, inclusive);
    }

    public void SetUpdatedAfter(DateTimeOffset value, bool inclusive)
    {
        (UpdatedAfter, UpdatedAfterInclusive) = SelectLower(UpdatedAfter, UpdatedAfterInclusive, value, inclusive);
    }

    public void SetAuthorDateAfter(DateTimeOffset value, bool inclusive)
    {
        (AuthorDateAfter, AuthorDateAfterInclusive) =
            SelectLower(AuthorDateAfter, AuthorDateAfterInclusive, value, inclusive);
    }

    public void SetAuthorDateBefore(DateTimeOffset value, bool inclusive)
    {
        (AuthorDateBefore, AuthorDateBeforeInclusive) =
            SelectUpper(AuthorDateBefore, AuthorDateBeforeInclusive, value, inclusive);
    }

    public void SetCommitterDateAfter(DateTimeOffset value, bool inclusive)
    {
        (CommitterDateAfter, CommitterDateAfterInclusive) =
            SelectLower(CommitterDateAfter, CommitterDateAfterInclusive, value, inclusive);
    }

    public void SetCommitterDateBefore(DateTimeOffset value, bool inclusive)
    {
        (CommitterDateBefore, CommitterDateBeforeInclusive) =
            SelectUpper(CommitterDateBefore, CommitterDateBeforeInclusive, value, inclusive);
    }

    private static (DateTimeOffset? Value, bool Inclusive) SelectLower(
        DateTimeOffset? target,
        bool targetInclusive,
        DateTimeOffset value,
        bool inclusive)
    {
        if (target is null || value > target.Value || value == target.Value && !inclusive)
            return (value, inclusive);

        return (target, targetInclusive);
    }

    private static (DateTimeOffset? Value, bool Inclusive) SelectUpper(
        DateTimeOffset? target,
        bool targetInclusive,
        DateTimeOffset value,
        bool inclusive)
    {
        if (target is null || value < target.Value || value == target.Value && !inclusive)
            return (value, inclusive);

        return (target, targetInclusive);
    }
}

internal static class GitHubSourcePlanner
{
    public const string FiltersPropertyName = "GitHubFilters";

    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        var tableName = name.ToLowerInvariant();
        var (acceptedPredicate, residualPredicate) = SplitPredicate(
            request.Predicate,
            expression => IsSupported(tableName, expression));
        var filters = ExtractFilters(tableName, acceptedPredicate);
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
            Cardinality = CardinalityEstimate.Unknown("GitHub API cardinality depends on remote repository state."),
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public static GitHubFilterParameters GetFilters(SourceExecutionPlan plan)
    {
        return plan.Properties is not null &&
               plan.Properties.TryGetValue(FiltersPropertyName, out var value) &&
               value is GitHubFilterParameters filters
            ? filters
            : new GitHubFilterParameters();
    }

    public static IReadOnlyList<T> ApplyAcceptedPlan<T>(
        IEnumerable<T> rows,
        SourceExecutionPlan plan,
        ref long skipped,
        ref long emitted)
    {
        var result = new List<T>();

        foreach (var row in rows)
        {
            if (!Matches(plan.AcceptedPredicate, row!))
                continue;

            if (plan.AcceptedSkip.HasValue && skipped < plan.AcceptedSkip.Value)
            {
                skipped++;
                continue;
            }

            if (plan.AcceptedTake.HasValue && emitted >= plan.AcceptedTake.Value)
                break;

            result.Add(row);
            emitted++;
        }

        return result;
    }

    public static bool IsTakeSatisfied(SourceExecutionPlan plan, long emitted)
    {
        return plan.AcceptedTake.HasValue && emitted >= plan.AcceptedTake.Value;
    }

    public static void ApplyIssueRequestFilters(RepositoryIssueRequest request, GitHubFilterParameters filters)
    {
        if (filters.State is not null)
        {
            request.State = filters.State.ToLowerInvariant() switch
            {
                "open" => ItemStateFilter.Open,
                "closed" => ItemStateFilter.Closed,
                _ => ItemStateFilter.All
            };
        }

        if (filters.AssigneeLogin is not null)
            request.Assignee = filters.AssigneeLogin;

        if (filters.AuthorLogin is not null)
            request.Creator = filters.AuthorLogin;

        if (filters.MilestoneTitle is not null)
            request.Milestone = filters.MilestoneTitle;

        var since = Max(filters.CreatedAfter, filters.UpdatedAfter);
        if (since.HasValue)
            request.Since = since.Value;
    }

    public static void ApplyPullRequestFilters(PullRequestRequest request, GitHubFilterParameters filters)
    {
        if (filters.State is not null)
        {
            request.State = filters.State.ToLowerInvariant() switch
            {
                "open" => ItemStateFilter.Open,
                "closed" => ItemStateFilter.Closed,
                _ => ItemStateFilter.All
            };
        }

        if (filters.HeadRef is not null)
            request.Head = filters.HeadRef;

        if (filters.BaseRef is not null)
            request.Base = filters.BaseRef;
    }

    public static void ApplyCommitFilters(CommitRequest request, GitHubFilterParameters filters)
    {
        if (filters.Sha is not null)
            request.Sha = filters.Sha;

        if (filters.AuthorLogin is not null)
            request.Author = filters.AuthorLogin;
    }

    public static void ApplyRepositoryFilters(RepositoryRequest request, GitHubFilterParameters filters)
    {
        if (filters.Visibility is null)
            return;

        request.Visibility = filters.Visibility.ToLowerInvariant() switch
        {
            "public" => RepositoryRequestVisibility.Public,
            "private" => RepositoryRequestVisibility.Private,
            "internal" => RepositoryRequestVisibility.Internal,
            _ => RepositoryRequestVisibility.All
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
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        return tableName switch
        {
            "issues" => IsSupportedIssueComparison(columnName, literal.Value, op),
            "pullrequests" => IsSupportedPullRequestComparison(columnName, literal.Value, op),
            "commits" => IsSupportedCommitComparison(columnName, literal.Value, op),
            "repositories" => IsSupportedRepositoryComparison(columnName, literal.Value, op),
            _ => false
        };
    }

    private static bool IsSupportedIssueComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        if (op == SourcePredicateComparisonOperator.Equal &&
            value is string &&
            (columnName.Equals(nameof(IssueEntity.State), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IssueEntity.AuthorLogin), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IssueEntity.AssigneeLogin), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(IssueEntity.MilestoneTitle), StringComparison.OrdinalIgnoreCase)))
            return true;

        return (op is SourcePredicateComparisonOperator.GreaterThan
                    or SourcePredicateComparisonOperator.GreaterOrEqual) &&
               (columnName.Equals(nameof(IssueEntity.CreatedAt), StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals(nameof(IssueEntity.UpdatedAt), StringComparison.OrdinalIgnoreCase)) &&
               TryGetDateTimeOffset(value, out _);
    }

    private static bool IsSupportedPullRequestComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        return op == SourcePredicateComparisonOperator.Equal &&
               value is string &&
               (columnName.Equals(nameof(PullRequestEntity.State), StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals(nameof(PullRequestEntity.HeadRef), StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals(nameof(PullRequestEntity.BaseRef), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSupportedCommitComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        if (op == SourcePredicateComparisonOperator.Equal &&
            value is string &&
            (columnName.Equals(nameof(CommitEntity.Sha), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(CommitEntity.AuthorLogin), StringComparison.OrdinalIgnoreCase) ||
             columnName.Equals(nameof(CommitEntity.AuthorName), StringComparison.OrdinalIgnoreCase)))
            return true;

        return (op is SourcePredicateComparisonOperator.GreaterThan
                    or SourcePredicateComparisonOperator.GreaterOrEqual
                    or SourcePredicateComparisonOperator.LessThan
                    or SourcePredicateComparisonOperator.LessOrEqual) &&
               (columnName.Equals(nameof(CommitEntity.AuthorDate), StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals(nameof(CommitEntity.CommitterDate), StringComparison.OrdinalIgnoreCase)) &&
               TryGetDateTimeOffset(value, out _);
    }

    private static bool IsSupportedRepositoryComparison(
        string columnName,
        object? value,
        SourcePredicateComparisonOperator op)
    {
        if (op != SourcePredicateComparisonOperator.Equal)
            return false;

        return value switch
        {
            string => columnName.Equals(nameof(RepositoryEntity.Language), StringComparison.OrdinalIgnoreCase) ||
                      columnName.Equals(nameof(RepositoryEntity.Visibility), StringComparison.OrdinalIgnoreCase),
            bool => columnName.Equals(nameof(RepositoryEntity.IsArchived), StringComparison.OrdinalIgnoreCase) ||
                    columnName.Equals(nameof(RepositoryEntity.IsFork), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static GitHubFilterParameters ExtractFilters(string tableName, SourcePredicateExpression? predicate)
    {
        var filters = new GitHubFilterParameters();
        ExtractFilters(tableName, predicate, filters);
        return filters;
    }

    private static void ExtractFilters(
        string tableName,
        SourcePredicateExpression? predicate,
        GitHubFilterParameters filters)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                ExtractFilters(tableName, logical.Left, filters);
                ExtractFilters(tableName, logical.Right, filters);
                return;
            case SourcePredicateComparison comparison:
                ApplyComparison(tableName, comparison, filters);
                return;
        }
    }

    private static void ApplyComparison(
        string tableName,
        SourcePredicateComparison comparison,
        GitHubFilterParameters filters)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return;

        switch (tableName, columnName, op, literal.Value)
        {
            case ("issues", nameof(IssueEntity.State), SourcePredicateComparisonOperator.Equal, string value):
                filters.State = value;
                break;
            case ("pullrequests", nameof(PullRequestEntity.State), SourcePredicateComparisonOperator.Equal, string value):
                filters.State = value;
                break;
            case ("issues", nameof(IssueEntity.AuthorLogin), SourcePredicateComparisonOperator.Equal, string value):
                filters.AuthorLogin = value;
                break;
            case ("commits", nameof(CommitEntity.AuthorLogin), SourcePredicateComparisonOperator.Equal, string value):
                filters.AuthorLogin = value;
                break;
            case ("commits", nameof(CommitEntity.AuthorName), SourcePredicateComparisonOperator.Equal, string value):
                filters.AuthorName = value;
                break;
            case ("issues", nameof(IssueEntity.AssigneeLogin), SourcePredicateComparisonOperator.Equal, string value):
                filters.AssigneeLogin = value;
                break;
            case ("issues", nameof(IssueEntity.MilestoneTitle), SourcePredicateComparisonOperator.Equal, string value):
                filters.MilestoneTitle = value;
                break;
            case ("pullrequests", nameof(PullRequestEntity.HeadRef), SourcePredicateComparisonOperator.Equal, string value):
                filters.HeadRef = value;
                break;
            case ("pullrequests", nameof(PullRequestEntity.BaseRef), SourcePredicateComparisonOperator.Equal, string value):
                filters.BaseRef = value;
                break;
            case ("commits", nameof(CommitEntity.Sha), SourcePredicateComparisonOperator.Equal, string value):
                filters.Sha = value;
                break;
            case ("repositories", nameof(RepositoryEntity.Language), SourcePredicateComparisonOperator.Equal, string value):
                filters.Language = value;
                break;
            case ("repositories", nameof(RepositoryEntity.Visibility), SourcePredicateComparisonOperator.Equal, string value):
                filters.Visibility = value;
                break;
            case ("repositories", nameof(RepositoryEntity.IsArchived), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsArchived = value;
                break;
            case ("repositories", nameof(RepositoryEntity.IsFork), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsFork = value;
                break;
        }

        if (!TryGetDateTimeOffset(literal.Value, out var date))
            return;

        switch (tableName, columnName, op)
        {
            case ("issues", nameof(IssueEntity.CreatedAt), SourcePredicateComparisonOperator.GreaterThan):
                filters.SetCreatedAfter(date, false);
                break;
            case ("issues", nameof(IssueEntity.CreatedAt), SourcePredicateComparisonOperator.GreaterOrEqual):
                filters.SetCreatedAfter(date, true);
                break;
            case ("issues", nameof(IssueEntity.UpdatedAt), SourcePredicateComparisonOperator.GreaterThan):
                filters.SetUpdatedAfter(date, false);
                break;
            case ("issues", nameof(IssueEntity.UpdatedAt), SourcePredicateComparisonOperator.GreaterOrEqual):
                filters.SetUpdatedAfter(date, true);
                break;
            case ("commits", nameof(CommitEntity.AuthorDate), SourcePredicateComparisonOperator.GreaterThan):
                filters.SetAuthorDateAfter(date, false);
                break;
            case ("commits", nameof(CommitEntity.AuthorDate), SourcePredicateComparisonOperator.GreaterOrEqual):
                filters.SetAuthorDateAfter(date, true);
                break;
            case ("commits", nameof(CommitEntity.AuthorDate), SourcePredicateComparisonOperator.LessThan):
                filters.SetAuthorDateBefore(date, false);
                break;
            case ("commits", nameof(CommitEntity.AuthorDate), SourcePredicateComparisonOperator.LessOrEqual):
                filters.SetAuthorDateBefore(date, true);
                break;
            case ("commits", nameof(CommitEntity.CommitterDate), SourcePredicateComparisonOperator.GreaterThan):
                filters.SetCommitterDateAfter(date, false);
                break;
            case ("commits", nameof(CommitEntity.CommitterDate), SourcePredicateComparisonOperator.GreaterOrEqual):
                filters.SetCommitterDateAfter(date, true);
                break;
            case ("commits", nameof(CommitEntity.CommitterDate), SourcePredicateComparisonOperator.LessThan):
                filters.SetCommitterDateBefore(date, false);
                break;
            case ("commits", nameof(CommitEntity.CommitterDate), SourcePredicateComparisonOperator.LessOrEqual):
                filters.SetCommitterDateBefore(date, true);
                break;
        }
    }

    private static bool Matches(SourcePredicateExpression? predicate, object entity)
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

    private static bool EvaluateComparison(SourcePredicateComparison comparison, object entity)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return true;

        var left = GetColumnValue(entity, columnName);
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

    private static object? GetColumnValue(object entity, string columnName)
    {
        return entity switch
        {
            IssueEntity issue => columnName switch
            {
                nameof(IssueEntity.State) => issue.State,
                nameof(IssueEntity.AuthorLogin) => issue.AuthorLogin,
                nameof(IssueEntity.AssigneeLogin) => issue.AssigneeLogin,
                nameof(IssueEntity.MilestoneTitle) => issue.MilestoneTitle,
                nameof(IssueEntity.CreatedAt) => issue.CreatedAt,
                nameof(IssueEntity.UpdatedAt) => issue.UpdatedAt,
                _ => null
            },
            PullRequestEntity pullRequest => columnName switch
            {
                nameof(PullRequestEntity.State) => pullRequest.State,
                nameof(PullRequestEntity.HeadRef) => pullRequest.HeadRef,
                nameof(PullRequestEntity.BaseRef) => pullRequest.BaseRef,
                _ => null
            },
            CommitEntity commit => columnName switch
            {
                nameof(CommitEntity.Sha) => commit.Sha,
                nameof(CommitEntity.AuthorLogin) => commit.AuthorLogin,
                nameof(CommitEntity.AuthorName) => commit.AuthorName,
                nameof(CommitEntity.AuthorDate) => commit.AuthorDate,
                nameof(CommitEntity.CommitterDate) => commit.CommitterDate,
                _ => null
            },
            RepositoryEntity repository => columnName switch
            {
                nameof(RepositoryEntity.Language) => repository.Language,
                nameof(RepositoryEntity.Visibility) => repository.Visibility,
                nameof(RepositoryEntity.IsArchived) => repository.IsArchived,
                nameof(RepositoryEntity.IsFork) => repository.IsFork,
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

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null)
            return right;

        if (right is null)
            return left;

        return left.Value >= right.Value ? left : right;
    }
}
