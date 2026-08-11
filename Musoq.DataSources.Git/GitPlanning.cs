using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class GitFilterParameters
{
    public SourcePredicateExpression? RawPredicate { get; set; }
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

/// <summary>
/// Immutable projection contract carried from planning to the source. An empty accepted projection is distinct from
/// an unplanned source and represents a valid zero-column projection such as <c>COUNT(*)</c>.
/// </summary>
internal sealed class GitProjection
{
    public GitProjection(bool isAccepted, IEnumerable<string> columns, IEnumerable<string>? predicateDependencies = null)
    {
        IsAccepted = isAccepted;
        Columns = columns.Concat(predicateDependencies ?? []).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAccepted { get; }

    public IReadOnlySet<string> Columns { get; }

    /// <summary>
    /// An unplanned direct source must preserve its historical full-row behavior. Only an accepted projection may
    /// physically omit a public column; an accepted empty projection is therefore the cardinality-only case.
    /// </summary>
    public bool Includes(string column) => !IsAccepted || Columns.Contains(column);

    public static GitProjection NotAccepted { get; } = new(false, []);
}

internal static class GitSourcePlanner
{
    public const string FiltersPropertyName = "GitFilters";
    public const string ProjectionPropertyName = "GitProjection";

    private static readonly IReadOnlyDictionary<string, HashSet<string>> FilterColumnsByTable =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["commits"] =
            [
                nameof(CommitEntity.Author),
                nameof(CommitEntity.AuthorEmail),
                nameof(CommitEntity.Committer),
                nameof(CommitEntity.CommitterEmail),
                nameof(CommitEntity.Sha),
                nameof(CommitEntity.CommittedWhen)
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

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ColumnsByTable =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["repository"] = Names(RepositoryEntity.NameToIndexMap.Keys),
            ["commits"] = Names(CommitEntity.NameToIndexMap.Keys),
            ["branches"] = Names(BranchEntity.NameToIndexMap.Keys),
            ["tags"] = Names(TagEntity.NameToIndexMap.Keys),
            ["remotes"] = Names(RemoteEntity.NameToIndexMap.Keys),
            ["status"] = Names(StatusEntity.NameToIndexMap.Keys),
            ["filehistory"] = Names(FileHistoryEntity.NameToIndexMap.Keys),
            ["blame"] = Names(BlameHunkEntity.NameToIndexMap.Keys)
        };

    public static SourcePlanResult Plan(string name, SourcePlanRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(request);

        var tableName = name.ToLowerInvariant();
        var (acceptedPredicate, residualPredicate) = SplitPredicate(
            request.Predicate,
            expression => CanEvaluatePredicate(tableName, expression));
        var acceptedColumns = CanReadColumns(tableName, request.RequiredColumns)
            ? request.RequiredColumns
            : [];
        var projection = new GitProjection(
            acceptedColumns.Count == request.RequiredColumns.Count,
            acceptedColumns.Select(column => NormalizeColumnName(column.Name)),
            GetPredicateColumns(acceptedPredicate));
        var residualOrderBy = request.OrderBy ?? [];
        var acceptsSlice = SupportsNaturalWindow(tableName) &&
                           residualPredicate is null &&
                           residualOrderBy.Count == 0 &&
                           IsNonNegativeWindow(request.Skip, request.Take);
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FiltersPropertyName] = ExtractFilters(tableName, acceptedPredicate),
            [ProjectionPropertyName] = projection
        };

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = acceptedColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = [],
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = properties
            },
            AcceptedColumns = acceptedColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = [],
            ResidualOrderBy = residualOrderBy,
            AcceptedSkip = acceptsSlice ? request.Skip : null,
            ResidualSkip = acceptsSlice ? null : request.Skip,
            AcceptedTake = acceptsSlice ? request.Take : null,
            ResidualTake = acceptsSlice ? null : request.Take,
            Cardinality = CardinalityEstimate.Unknown("Git source cardinality depends on repository contents."),
            Diagnostics = CreateDiagnostics(request, residualPredicate, residualOrderBy, acceptsSlice)
        };
    }

    public static GitFilterParameters GetFilters(SourceExecutionPlan plan)
    {
        return plan.Properties.TryGetValue(FiltersPropertyName, out var value) && value is GitFilterParameters filters
            ? filters
            : new GitFilterParameters();
    }

    public static GitProjection GetProjection(SourceExecutionPlan plan)
    {
        return plan.Properties.TryGetValue(ProjectionPropertyName, out var value) && value is GitProjection projection
            ? projection
            : GitProjection.NotAccepted;
    }

    public static bool Matches(SourcePredicateExpression? predicate, object entity)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                Matches(logical.Left, entity) && Matches(logical.Right, entity),
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.Or } logical =>
                Matches(logical.Left, entity) || Matches(logical.Right, entity),
            SourcePredicateComparison comparison => EvaluateComparison(comparison, entity),
            SourcePredicateIn inPredicate => EvaluateIn(inPredicate, entity),
            SourcePredicateNullCheck nullCheck =>
                (EvaluateValue(nullCheck.Expression, entity) is null) ^ nullCheck.IsNegated,
            _ => false
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
               Matches(filters.CommitterEmail, commit.Committer.Email) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(CommitEntity.Author) => commit.Author.Name,
                   nameof(CommitEntity.AuthorEmail) => commit.Author.Email,
                   nameof(CommitEntity.Committer) => commit.Committer.Name,
                   nameof(CommitEntity.CommitterEmail) => commit.Committer.Email,
                   nameof(CommitEntity.Sha) => commit.Sha,
                   nameof(CommitEntity.CommittedWhen) => commit.Committer.When,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, GitCommitRecord commit)
    {
        if (filters.Since is not null)
        {
            var comparison = commit.CommittedWhen.CompareTo(filters.Since.Value);
            if (comparison < 0 || comparison == 0 && !filters.SinceInclusive)
                return false;
        }

        if (filters.Until is not null)
        {
            var comparison = commit.CommittedWhen.CompareTo(filters.Until.Value);
            if (comparison > 0 || comparison == 0 && !filters.UntilInclusive)
                return false;
        }

        return Matches(filters.Sha, commit.Sha) &&
               Matches(filters.Author, commit.Author) &&
               Matches(filters.AuthorEmail, commit.AuthorEmail) &&
               Matches(filters.Committer, commit.Committer) &&
               Matches(filters.CommitterEmail, commit.CommitterEmail) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(CommitEntity.Author) => commit.Author,
                   nameof(CommitEntity.AuthorEmail) => commit.AuthorEmail,
                   nameof(CommitEntity.Committer) => commit.Committer,
                   nameof(CommitEntity.CommitterEmail) => commit.CommitterEmail,
                   nameof(CommitEntity.Sha) => commit.Sha,
                   nameof(CommitEntity.CommittedWhen) => commit.CommittedWhen,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, Branch branch)
    {
        return Matches(filters.FriendlyName, branch.FriendlyName) &&
               Matches(filters.CanonicalName, branch.CanonicalName) &&
               Matches(filters.IsRemote, branch.IsRemote) &&
               Matches(filters.IsCurrentRepositoryHead, branch.IsCurrentRepositoryHead) &&
               Matches(filters.IsTracking, branch.IsTracking) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(BranchEntity.FriendlyName) => branch.FriendlyName,
                   nameof(BranchEntity.CanonicalName) => branch.CanonicalName,
                   nameof(BranchEntity.IsRemote) => branch.IsRemote,
                   nameof(BranchEntity.IsCurrentRepositoryHead) => branch.IsCurrentRepositoryHead,
                   nameof(BranchEntity.IsTracking) => branch.IsTracking,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, GitBranchRecord branch)
    {
        return Matches(filters.FriendlyName, branch.FriendlyName) &&
               Matches(filters.CanonicalName, branch.CanonicalName) &&
               Matches(filters.IsRemote, branch.IsRemote) &&
               Matches(filters.IsCurrentRepositoryHead, branch.IsCurrentRepositoryHead) &&
               Matches(filters.IsTracking, branch.IsTracking) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(BranchEntity.FriendlyName) => branch.FriendlyName,
                   nameof(BranchEntity.CanonicalName) => branch.CanonicalName,
                   nameof(BranchEntity.IsRemote) => branch.IsRemote,
                   nameof(BranchEntity.IsCurrentRepositoryHead) => branch.IsCurrentRepositoryHead,
                   nameof(BranchEntity.IsTracking) => branch.IsTracking,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, Tag tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(TagEntity.FriendlyName) => tag.FriendlyName,
                   nameof(TagEntity.CanonicalName) => tag.CanonicalName,
                   nameof(TagEntity.IsAnnotated) => tag.IsAnnotated,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, GitTagRecord tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(TagEntity.FriendlyName) => tag.FriendlyName,
                   nameof(TagEntity.CanonicalName) => tag.CanonicalName,
                   nameof(TagEntity.IsAnnotated) => tag.IsAnnotated,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, Remote remote)
    {
        return Matches(filters.RemoteName, remote.Name) && Matches(filters.Url, remote.Url) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(RemoteEntity.Name) => remote.Name,
                   nameof(RemoteEntity.Url) => remote.Url,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, GitRemoteRecord remote)
    {
        return Matches(filters.RemoteName, remote.Name) && Matches(filters.Url, remote.Url) &&
               MatchesNative(filters.RawPredicate, column => column switch
               {
                   nameof(RemoteEntity.Name) => remote.Name,
                   nameof(RemoteEntity.Url) => remote.Url,
                   _ => null
               });
    }

    public static bool Matches(GitFilterParameters filters, StatusEntry entry)
    {
        var state = entry.State.ToString();
        return Matches(filters.State, state) && MatchesNative(filters.RawPredicate, column => column switch
        {
            nameof(StatusEntity.State) => state,
            _ => null
        });
    }

    public static bool Matches(GitFilterParameters filters, GitStatusRecord entry)
    {
        var state = entry.State.ToString();
        return Matches(filters.State, state) && MatchesNative(filters.RawPredicate, column => column switch
        {
            nameof(StatusEntity.State) => state,
            _ => null
        });
    }

    private static IReadOnlyList<OptimizationDiagnostic> CreateDiagnostics(
        SourcePlanRequest request,
        SourcePredicateExpression? residualPredicate,
        IReadOnlyList<OrderByExpression> residualOrderBy,
        bool acceptsSlice)
    {
        var diagnostics = new List<OptimizationDiagnostic>();
        var target = $"#{request.Identity.SchemaName}.{request.Identity.MethodName}";

        if (residualPredicate is not null)
            diagnostics.Add(OptimizationDiagnostic.Warning("Git source retained an unsupported predicate as evaluator residual work.") with
            {
                Optimization = "GitPredicatePushdown",
                Target = target,
                Reason = "The predicate references an unsupported or expensive Git column."
            });

        if (residualOrderBy.Count > 0)
            diagnostics.Add(OptimizationDiagnostic.Info("Git source does not yet execute ORDER BY pushdown; ordering remains evaluator work.") with
            {
                Optimization = "GitOrderPushdown",
                Target = target,
                Reason = "No source order is accepted until the backend can preserve the requested order."
            });

        if (!acceptsSlice && (request.Skip.HasValue || request.Take.HasValue))
            diagnostics.Add(OptimizationDiagnostic.Info("Git source does not yet execute outer SKIP/TAKE pushdown; the evaluator owns the window.") with
            {
                Optimization = "GitSlicePushdown",
                Target = target,
                Reason = "Residual ordering and predicate dependencies must remain correct before a source window is accepted."
            });

        return diagnostics;
    }

    private static bool SupportsNaturalWindow(string tableName) =>
        tableName.Equals("commits", StringComparison.OrdinalIgnoreCase) ||
        tableName.Equals("filehistory", StringComparison.OrdinalIgnoreCase);

    private static bool IsNonNegativeWindow(long? skip, long? take) =>
        (!skip.HasValue || skip.Value >= 0) && (!take.HasValue || take.Value >= 0);

    private static bool CanReadColumns(string tableName, IReadOnlyList<SourceColumnRef> columns)
    {
        if (!ColumnsByTable.TryGetValue(tableName, out var supported))
            return false;

        return columns.All(column => supported.Contains(NormalizeColumnName(column.Name)));
    }

    private static IEnumerable<string> GetPredicateColumns(SourcePredicateExpression? predicate)
    {
        if (predicate is null)
            return [];

        return predicate switch
        {
            SourcePredicateColumn column => [NormalizeColumnName(column.Column.Name)],
            SourcePredicateComparison comparison => GetPredicateColumns(comparison.Left)
                .Concat(GetPredicateColumns(comparison.Right)),
            SourcePredicateIn inPredicate => GetPredicateColumns(inPredicate.Expression)
                .Concat(inPredicate.Values.SelectMany(GetPredicateColumns)),
            SourcePredicateNullCheck nullCheck => GetPredicateColumns(nullCheck.Expression),
            SourcePredicateLogical logical => GetPredicateColumns(logical.Left).Concat(GetPredicateColumns(logical.Right)),
            _ => []
        };
    }

    private static (SourcePredicateExpression? Accepted, SourcePredicateExpression? Residual) SplitPredicate(
        SourcePredicateExpression? predicate,
        Func<SourcePredicateExpression, bool> canEvaluate)
    {
        if (predicate is null)
            return (null, null);

        if (predicate is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical)
        {
            var left = SplitPredicate(logical.Left, canEvaluate);
            var right = SplitPredicate(logical.Right, canEvaluate);
            return (CombineAnd(left.Accepted, right.Accepted), CombineAnd(left.Residual, right.Residual));
        }

        return canEvaluate(predicate) ? (predicate, null) : (null, predicate);
    }

    private static bool CanEvaluatePredicate(string tableName, SourcePredicateExpression predicate)
    {
        return predicate switch
        {
            SourcePredicateLogical logical =>
                CanEvaluatePredicate(tableName, logical.Left) && CanEvaluatePredicate(tableName, logical.Right),
            SourcePredicateComparison comparison => IsSupportedComparison(tableName, comparison),
            SourcePredicateIn inPredicate => IsSupportedIn(tableName, inPredicate),
            SourcePredicateNullCheck nullCheck =>
                TryGetColumn(nullCheck.Expression, out var column) && IsFilterColumn(tableName, column),
            _ => false
        };
    }

    private static bool IsSupportedComparison(string tableName, SourcePredicateComparison comparison)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        if (tableName.Equals("commits", StringComparison.OrdinalIgnoreCase) &&
            columnName.Equals(nameof(CommitEntity.CommittedWhen), StringComparison.OrdinalIgnoreCase))
        {
            return op is SourcePredicateComparisonOperator.GreaterThan
                or SourcePredicateComparisonOperator.GreaterOrEqual
                or SourcePredicateComparisonOperator.LessThan
                or SourcePredicateComparisonOperator.LessOrEqual
                or SourcePredicateComparisonOperator.Equal && TryGetDateTimeOffset(literal.Value, out _);
        }

        return op is SourcePredicateComparisonOperator.Equal or SourcePredicateComparisonOperator.NotEqual &&
               IsFilterColumn(tableName, columnName) &&
               IsLiteralTypeSupported(tableName, columnName, literal.Value);
    }

    private static bool IsSupportedIn(string tableName, SourcePredicateIn predicate)
    {
        return TryGetColumn(predicate.Expression, out var column) &&
               IsFilterColumn(tableName, column) &&
               predicate.Values.All(value => value is SourcePredicateLiteral literal &&
                   IsLiteralTypeSupported(tableName, column, literal.Value));
    }

    private static bool IsLiteralTypeSupported(string tableName, string columnName, object? value)
    {
        if (tableName.Equals("commits", StringComparison.OrdinalIgnoreCase) &&
            columnName.Equals(nameof(CommitEntity.CommittedWhen), StringComparison.OrdinalIgnoreCase))
            return TryGetDateTimeOffset(value, out _);

        if (tableName.Equals("branches", StringComparison.OrdinalIgnoreCase) && columnName is
                nameof(BranchEntity.IsRemote) or nameof(BranchEntity.IsCurrentRepositoryHead) or nameof(BranchEntity.IsTracking) ||
            tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) && columnName == nameof(TagEntity.IsAnnotated))
            return value is bool;

        return value is string;
    }

    private static bool IsFilterColumn(string tableName, string columnName)
    {
        return FilterColumnsByTable.TryGetValue(tableName, out var supported) && supported.Contains(columnName);
    }

    private static GitFilterParameters ExtractFilters(string tableName, SourcePredicateExpression? predicate)
    {
        var filters = new GitFilterParameters { RawPredicate = predicate };
        ExtractFilters(tableName, predicate, filters);
        return filters;
    }

    private static void ExtractFilters(string tableName, SourcePredicateExpression? predicate, GitFilterParameters filters)
    {
        switch (predicate)
        {
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
            return false;

        var left = GetColumnValue(entity, columnName);
        var right = literal.Value;
        if (left is DateTimeOffset && TryGetDateTimeOffset(right, out var date))
            right = date;

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => ValuesEqual(left, right),
            SourcePredicateComparisonOperator.NotEqual => !ValuesEqual(left, right),
            SourcePredicateComparisonOperator.GreaterThan => Compare(left, right) > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => Compare(left, right) >= 0,
            SourcePredicateComparisonOperator.LessThan => Compare(left, right) < 0,
            SourcePredicateComparisonOperator.LessOrEqual => Compare(left, right) <= 0,
            _ => false
        };
    }

    private static bool MatchesNative(SourcePredicateExpression? predicate, Func<string, object?> value)
    {
        return predicate switch
        {
            null => true,
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical =>
                MatchesNative(logical.Left, value) && MatchesNative(logical.Right, value),
            SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.Or } logical =>
                MatchesNative(logical.Left, value) || MatchesNative(logical.Right, value),
            SourcePredicateComparison comparison => EvaluateNativeComparison(comparison, value),
            SourcePredicateIn inPredicate => EvaluateNativeIn(inPredicate, value),
            SourcePredicateNullCheck nullCheck when TryGetColumn(nullCheck.Expression, out var column) =>
                (value(column) is null) ^ nullCheck.IsNegated,
            _ => false
        };
    }

    private static bool EvaluateNativeComparison(SourcePredicateComparison comparison, Func<string, object?> value)
    {
        if (!TryGetComparisonParts(comparison, out var columnName, out var literal, out var op))
            return false;

        var left = value(columnName);
        var right = literal.Value;
        if (left is DateTimeOffset && TryGetDateTimeOffset(right, out var date))
            right = date;

        return op switch
        {
            SourcePredicateComparisonOperator.Equal => ValuesEqual(left, right),
            SourcePredicateComparisonOperator.NotEqual => !ValuesEqual(left, right),
            SourcePredicateComparisonOperator.GreaterThan => Compare(left, right) > 0,
            SourcePredicateComparisonOperator.GreaterOrEqual => Compare(left, right) >= 0,
            SourcePredicateComparisonOperator.LessThan => Compare(left, right) < 0,
            SourcePredicateComparisonOperator.LessOrEqual => Compare(left, right) <= 0,
            _ => false
        };
    }

    private static bool EvaluateNativeIn(SourcePredicateIn predicate, Func<string, object?> value)
    {
        if (!TryGetColumn(predicate.Expression, out var column))
            return false;

        var sourceValue = value(column);
        var contains = predicate.Values.OfType<SourcePredicateLiteral>().Any(item =>
        {
            object? candidate = item.Value;
            if (sourceValue is DateTimeOffset && TryGetDateTimeOffset(candidate, out var date))
                candidate = date;
            return ValuesEqual(sourceValue, candidate);
        });
        return predicate.IsNegated ? !contains : contains;
    }

    private static bool EvaluateIn(SourcePredicateIn predicate, object entity)
    {
        var value = EvaluateValue(predicate.Expression, entity);
        var contains = predicate.Values.Any(item => ValuesEqual(value, EvaluateValue(item, entity)));
        return predicate.IsNegated ? !contains : contains;
    }

    private static object? EvaluateValue(SourcePredicateExpression expression, object entity)
    {
        return expression switch
        {
            SourcePredicateColumn column => GetColumnValue(entity, NormalizeColumnName(column.Column.Name)),
            SourcePredicateLiteral literal => literal.Value,
            _ => null
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
        if (comparison.Left is SourcePredicateColumn leftColumn && comparison.Right is SourcePredicateLiteral rightLiteral)
        {
            columnName = NormalizeColumnName(leftColumn.Column.Name);
            literal = rightLiteral;
            op = comparison.Operator;
            return true;
        }

        if (comparison.Right is SourcePredicateColumn rightColumn && comparison.Left is SourcePredicateLiteral leftLiteral)
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

    private static bool TryGetColumn(SourcePredicateExpression expression, out string column)
    {
        if (expression is SourcePredicateColumn predicateColumn)
        {
            column = NormalizeColumnName(predicateColumn.Column.Name);
            return true;
        }

        column = string.Empty;
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

    private static IReadOnlySet<string> Names(IEnumerable<string> names) =>
        new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

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
                return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);
            default:
                date = default;
                return false;
        }
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        if (IsNumber(left) && IsNumber(right))
            return Convert.ToDecimal(left, CultureInfo.InvariantCulture) == Convert.ToDecimal(right, CultureInfo.InvariantCulture);
        return Equals(left, right);
    }

    private static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        if (left is string leftText && right is string rightText)
            return string.Compare(leftText, rightText, StringComparison.Ordinal);
        if (IsNumber(left) && IsNumber(right))
            return Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        return left is IComparable comparable ? comparable.CompareTo(right) : 0;
    }

    private static bool IsNumber(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool Matches(string? expected, string? actual) =>
        expected is null || string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool Matches(bool? expected, bool actual) =>
        expected is null || actual == expected.Value;
}
