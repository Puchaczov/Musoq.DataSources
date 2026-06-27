using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class GitFilterParameters
{
    public string? Author { get; set; }
    public string? AuthorEmail { get; set; }
    public string? Committer { get; set; }
    public string? CommitterEmail { get; set; }
    public string? Sha { get; set; }
    public DateTimeOffset? Since { get; private set; }
    public bool SinceInclusive { get; private set; }
    public DateTimeOffset? Until { get; private set; }
    public bool UntilInclusive { get; private set; }
    public string? FriendlyName { get; set; }
    public string? CanonicalName { get; set; }
    public bool? IsRemote { get; set; }
    public bool? IsCurrentRepositoryHead { get; set; }
    public bool? IsTracking { get; set; }
    public bool? IsAnnotated { get; set; }
    public string? RemoteName { get; set; }
    public string? Url { get; set; }
    public string? State { get; set; }

    public void SetSince(DateTimeOffset value, bool inclusive)
    {
        if (Since is null || value > Since.Value || value == Since.Value && !inclusive)
        {
            Since = value;
            SinceInclusive = inclusive;
        }
    }

    public void SetUntil(DateTimeOffset value, bool inclusive)
    {
        if (Until is null || value < Until.Value || value == Until.Value && !inclusive)
        {
            Until = value;
            UntilInclusive = inclusive;
        }
    }
}

internal static class GitSourcePlanner
{
    public const string FiltersPropertyName = "GitFilters";

    private static readonly IReadOnlyDictionary<string, HashSet<string>> EqualityColumnsByTable =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["commits"] =
            [
                nameof(CommitEntity.Author),
                nameof(CommitEntity.AuthorEmail),
                nameof(CommitEntity.Committer),
                nameof(CommitEntity.CommitterEmail),
                nameof(CommitEntity.Sha)
            ],
            ["branches"] =
            [
                nameof(BranchEntity.FriendlyName),
                nameof(BranchEntity.CanonicalName),
                nameof(BranchEntity.IsRemote),
                nameof(BranchEntity.IsCurrentRepositoryHead),
                nameof(BranchEntity.IsTracking)
            ],
            ["tags"] =
            [
                nameof(TagEntity.FriendlyName),
                nameof(TagEntity.CanonicalName),
                nameof(TagEntity.IsAnnotated)
            ],
            ["remotes"] =
            [
                nameof(RemoteEntity.Name),
                nameof(RemoteEntity.Url)
            ],
            ["status"] =
            [
                nameof(StatusEntity.State)
            ]
        };

    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        var tableName = name.ToLowerInvariant();
        var (acceptedPredicate, residualPredicate) = SplitPredicate(
            request.Predicate,
            expression => IsSupported(tableName, expression));
        var filters = ExtractFilters(tableName, acceptedPredicate);

        return BuildPlanResult(
            request,
            acceptedPredicate,
            residualPredicate,
            new Dictionary<string, object?>
            {
                [FiltersPropertyName] = filters
            });
    }

    public static GitFilterParameters GetFilters(SourceExecutionPlan plan)
    {
        return plan.Properties is not null &&
               plan.Properties.TryGetValue(FiltersPropertyName, out var value) &&
               value is GitFilterParameters filters
            ? filters
            : new GitFilterParameters();
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

    public static bool Matches(GitFilterParameters filters, Commit commit)
    {
        if (filters.Since is not null)
        {
            var comparison = commit.Committer.When.CompareTo(filters.Since.Value);
            if (comparison < 0 || comparison == 0 && !filters.SinceInclusive)
                return false;
        }

        if (filters.Until is not null)
        {
            var comparison = commit.Committer.When.CompareTo(filters.Until.Value);
            if (comparison > 0 || comparison == 0 && !filters.UntilInclusive)
                return false;
        }

        return Matches(filters.Sha, commit.Sha) &&
               Matches(filters.Author, commit.Author.Name) &&
               Matches(filters.AuthorEmail, commit.Author.Email) &&
               Matches(filters.Committer, commit.Committer.Name) &&
               Matches(filters.CommitterEmail, commit.Committer.Email);
    }

    public static bool Matches(GitFilterParameters filters, Branch branch)
    {
        return Matches(filters.FriendlyName, branch.FriendlyName) &&
               Matches(filters.CanonicalName, branch.CanonicalName) &&
               Matches(filters.IsRemote, branch.IsRemote) &&
               Matches(filters.IsCurrentRepositoryHead, branch.IsCurrentRepositoryHead) &&
               Matches(filters.IsTracking, branch.IsTracking);
    }

    public static bool Matches(GitFilterParameters filters, Tag tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated);
    }

    public static bool Matches(GitFilterParameters filters, Remote remote)
    {
        return Matches(filters.RemoteName, remote.Name) &&
               Matches(filters.Url, remote.Url);
    }

    public static bool Matches(GitFilterParameters filters, StatusEntry entry)
    {
        return Matches(filters.State, entry.State.ToString());
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
            Cardinality = CardinalityEstimate.Unknown("Git source cardinality depends on repository contents."),
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
            !TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        if (tableName.Equals("commits", StringComparison.OrdinalIgnoreCase) &&
            columnName.Equals(nameof(CommitEntity.CommittedWhen), StringComparison.OrdinalIgnoreCase))
        {
            return op is SourcePredicateComparisonOperator.GreaterThan
                       or SourcePredicateComparisonOperator.GreaterOrEqual
                       or SourcePredicateComparisonOperator.LessThan
                       or SourcePredicateComparisonOperator.LessOrEqual &&
                   TryGetDateTimeOffset(literal.Value, out _);
        }

        return op == SourcePredicateComparisonOperator.Equal &&
               EqualityColumnsByTable.TryGetValue(tableName, out var supportedColumns) &&
               supportedColumns.Contains(columnName) &&
               IsLiteralTypeSupported(tableName, columnName, literal.Value);
    }

    private static bool IsLiteralTypeSupported(string tableName, string columnName, object? value)
    {
        if (tableName is "branches" && columnName is nameof(BranchEntity.IsRemote)
                or nameof(BranchEntity.IsCurrentRepositoryHead)
                or nameof(BranchEntity.IsTracking) ||
            tableName is "tags" && columnName is nameof(TagEntity.IsAnnotated))
        {
            return value is bool;
        }

        return value is string;
    }

    private static GitFilterParameters ExtractFilters(string tableName, SourcePredicateExpression? predicate)
    {
        var filters = new GitFilterParameters();
        ExtractFilters(tableName, predicate, filters);
        return filters;
    }

    private static void ExtractFilters(
        string tableName,
        SourcePredicateExpression? predicate,
        GitFilterParameters filters)
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

    private static void ApplyComparison(string tableName, SourcePredicateComparison comparison, GitFilterParameters filters)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return;

        switch (tableName, columnName, op, literal.Value)
        {
            case ("commits", nameof(CommitEntity.Author), SourcePredicateComparisonOperator.Equal, string value):
                filters.Author = value;
                break;
            case ("commits", nameof(CommitEntity.AuthorEmail), SourcePredicateComparisonOperator.Equal, string value):
                filters.AuthorEmail = value;
                break;
            case ("commits", nameof(CommitEntity.Committer), SourcePredicateComparisonOperator.Equal, string value):
                filters.Committer = value;
                break;
            case ("commits", nameof(CommitEntity.CommitterEmail), SourcePredicateComparisonOperator.Equal, string value):
                filters.CommitterEmail = value;
                break;
            case ("commits", nameof(CommitEntity.Sha), SourcePredicateComparisonOperator.Equal, string value):
                filters.Sha = value;
                break;
            case ("branches", nameof(BranchEntity.FriendlyName), SourcePredicateComparisonOperator.Equal, string value):
                filters.FriendlyName = value;
                break;
            case ("branches", nameof(BranchEntity.CanonicalName), SourcePredicateComparisonOperator.Equal, string value):
                filters.CanonicalName = value;
                break;
            case ("branches", nameof(BranchEntity.IsRemote), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsRemote = value;
                break;
            case ("branches", nameof(BranchEntity.IsCurrentRepositoryHead), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsCurrentRepositoryHead = value;
                break;
            case ("branches", nameof(BranchEntity.IsTracking), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsTracking = value;
                break;
            case ("tags", nameof(TagEntity.FriendlyName), SourcePredicateComparisonOperator.Equal, string value):
                filters.FriendlyName = value;
                break;
            case ("tags", nameof(TagEntity.CanonicalName), SourcePredicateComparisonOperator.Equal, string value):
                filters.CanonicalName = value;
                break;
            case ("tags", nameof(TagEntity.IsAnnotated), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsAnnotated = value;
                break;
            case ("remotes", nameof(RemoteEntity.Name), SourcePredicateComparisonOperator.Equal, string value):
                filters.RemoteName = value;
                break;
            case ("remotes", nameof(RemoteEntity.Url), SourcePredicateComparisonOperator.Equal, string value):
                filters.Url = value;
                break;
            case ("status", nameof(StatusEntity.State), SourcePredicateComparisonOperator.Equal, string value):
                filters.State = value;
                break;
        }

        if (!tableName.Equals("commits", StringComparison.OrdinalIgnoreCase) ||
            !columnName.Equals(nameof(CommitEntity.CommittedWhen), StringComparison.OrdinalIgnoreCase) ||
            !TryGetDateTimeOffset(literal.Value, out var date))
            return;

        switch (op)
        {
            case SourcePredicateComparisonOperator.GreaterThan:
                filters.SetSince(date, false);
                break;
            case SourcePredicateComparisonOperator.GreaterOrEqual:
                filters.SetSince(date, true);
                break;
            case SourcePredicateComparisonOperator.LessThan:
                filters.SetUntil(date, false);
                break;
            case SourcePredicateComparisonOperator.LessOrEqual:
                filters.SetUntil(date, true);
                break;
        }
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
            CommitEntity commit => columnName switch
            {
                nameof(CommitEntity.Author) => commit.Author,
                nameof(CommitEntity.AuthorEmail) => commit.AuthorEmail,
                nameof(CommitEntity.Committer) => commit.Committer,
                nameof(CommitEntity.CommitterEmail) => commit.CommitterEmail,
                nameof(CommitEntity.Sha) => commit.Sha,
                nameof(CommitEntity.CommittedWhen) => commit.CommittedWhen,
                _ => null
            },
            BranchEntity branch => columnName switch
            {
                nameof(BranchEntity.FriendlyName) => branch.FriendlyName,
                nameof(BranchEntity.CanonicalName) => branch.CanonicalName,
                nameof(BranchEntity.IsRemote) => branch.IsRemote,
                nameof(BranchEntity.IsCurrentRepositoryHead) => branch.IsCurrentRepositoryHead,
                nameof(BranchEntity.IsTracking) => branch.IsTracking,
                _ => null
            },
            TagEntity tag => columnName switch
            {
                nameof(TagEntity.FriendlyName) => tag.FriendlyName,
                nameof(TagEntity.CanonicalName) => tag.CanonicalName,
                nameof(TagEntity.IsAnnotated) => tag.IsAnnotated,
                _ => null
            },
            RemoteEntity remote => columnName switch
            {
                nameof(RemoteEntity.Name) => remote.Name,
                nameof(RemoteEntity.Url) => remote.Url,
                _ => null
            },
            StatusEntity status => columnName switch
            {
                nameof(StatusEntity.State) => status.State,
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
        if (left is IComparable comparable && right is not null)
            return comparable.CompareTo(right);

        return 0;
    }

    private static bool Matches(string? expected, string? actual)
    {
        return expected is null || string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static bool Matches(bool? expected, bool actual)
    {
        return expected is null || actual == expected.Value;
    }
}
