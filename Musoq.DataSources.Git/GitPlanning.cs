using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Common;
using System.Linq.Expressions;

namespace Musoq.DataSources.Git;

internal sealed class GitFilterParameters
{
    public TypedPredicatePlan TypedPredicates { get; } = new();
    public Func<Commit, bool>? CommitPredicate { get; set; }
    public Func<GitCommitRecord, bool>? GitCommitRecordPredicate { get; set; }
    public Func<Branch, bool>? BranchPredicate { get; set; }
    public Func<GitBranchRecord, bool>? GitBranchRecordPredicate { get; set; }
    public Func<Tag, bool>? TagPredicate { get; set; }
    public Func<GitTagRecord, bool>? GitTagRecordPredicate { get; set; }
    public Func<GitStashRecord, bool>? GitStashRecordPredicate { get; set; }
    public Func<StashEntity, bool>? StashEntityPredicate { get; set; }
    public Func<GitRemoteTagRecord, bool>? GitRemoteTagRecordPredicate { get; set; }
    public Func<RemoteTagEntity, bool>? RemoteTagEntityPredicate { get; set; }
    public Func<Remote, bool>? RemotePredicate { get; set; }
    public Func<GitRemoteRecord, bool>? GitRemoteRecordPredicate { get; set; }
    public Func<StatusEntry, bool>? StatusEntryPredicate { get; set; }
    public Func<GitStatusRecord, bool>? GitStatusRecordPredicate { get; set; }
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
    public string? TargetSha { get; set; }
    public string? ObjectSha { get; set; }
    public string? Selector { get; set; }
    public HashSet<string> FriendlyNames { get; } = new(StringComparer.Ordinal);
    public HashSet<string> CanonicalNames { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TargetShas { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ObjectShas { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Selectors { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Shas { get; } = new(StringComparer.Ordinal);
    public string? CanonicalNameAfter { get; private set; }
    public bool CanonicalNameAfterInclusive { get; private set; }
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

    public void SetCanonicalNameAfter(string value, bool inclusive)
    {
        if (CanonicalNameAfter is null || string.Compare(value, CanonicalNameAfter, StringComparison.Ordinal) > 0 ||
            string.Equals(value, CanonicalNameAfter, StringComparison.Ordinal) && !inclusive)
        {
            CanonicalNameAfter = value;
            CanonicalNameAfterInclusive = inclusive;
        }
    }
}

/// <summary>
/// Immutable projection contract carried from planning to the source. An empty accepted projection is distinct from
/// an unplanned source and represents a valid zero-column projection such as <c>COUNT(*)</c>.
/// </summary>
internal sealed class GitProjection
{
    public GitProjection(
        bool isAccepted,
        IEnumerable<string> columns,
        IEnumerable<string>? predicateDependencies = null,
        bool requiresNestedReferenceCapabilities = false)
    {
        IsAccepted = isAccepted;
        Columns = columns.Concat(predicateDependencies ?? []).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        RequiresNestedReferenceCapabilities = requiresNestedReferenceCapabilities;
    }

    public bool IsAccepted { get; }

    public IReadOnlySet<string> Columns { get; }

    public bool RequiresNestedReferenceCapabilities { get; }

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
    private const int MaxReferenceInPushdownValues = 128;

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
                nameof(TagEntity.TargetSha),
                nameof(TagEntity.IsAnnotated)
            ],
            ["stashes"] =
            [
                nameof(StashEntity.Selector),
                nameof(StashEntity.Sha)
            ],
            ["remotetags"] =
            [
                nameof(RemoteTagEntity.FriendlyName),
                nameof(RemoteTagEntity.CanonicalName),
                nameof(RemoteTagEntity.ObjectSha),
                nameof(RemoteTagEntity.IsAnnotated)
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
            ["stashes"] = Names(StashEntity.NameToIndexMap.Keys),
            ["remotetags"] = Names(RemoteTagEntity.NameToIndexMap.Keys),
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
            // Residual predicates are evaluated by the runtime, but their dependencies still have to be
            // physically present in the source row. Otherwise the evaluator can observe a default value rather
            // than the actual source value (notably for remote-tag peel metadata).
            GetPredicateColumns(request.Predicate));
        var requestedOrderBy = request.OrderBy ?? [];
        var acceptedOrderBy = CanPushDownOrder(tableName, requestedOrderBy)
            ? requestedOrderBy
            : [];
        var residualOrderBy = acceptedOrderBy.Count == requestedOrderBy.Count
            ? []
            : requestedOrderBy;
        var acceptsSlice = SupportsNaturalWindow(tableName) &&
                           residualPredicate is null &&
                           residualOrderBy.Count == 0 &&
                           IsNonNegativeWindow(request.Skip, request.Take);
        var filters = ExtractFilters(tableName, acceptedPredicate);
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FiltersPropertyName] = filters,
            [TypedPredicatePlan.PropertyName] = filters.TypedPredicates,
            [ProjectionPropertyName] = projection
        };

        return new SourcePlanResult
        {
            ExecutionPlan = new SourceExecutionPlan
            {
                Identity = request.Identity,
                AcceptedColumns = acceptedColumns,
                AcceptedPredicate = acceptedPredicate,
                AcceptedOrderBy = acceptedOrderBy,
                AcceptedSkip = acceptsSlice ? request.Skip : null,
                AcceptedTake = acceptsSlice ? request.Take : null,
                Properties = properties
            },
            AcceptedColumns = acceptedColumns,
            AcceptedPredicate = acceptedPredicate,
            ResidualPredicate = residualPredicate,
            AcceptedOrderBy = acceptedOrderBy,
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

    public static IReadOnlyList<string>? OrderedExactValues(IReadOnlySet<string> values) =>
        values.Count == 0 ? null : values.OrderBy(static value => value, StringComparer.Ordinal).ToArray();

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
               (filters.CommitPredicate?.Invoke(commit) ?? true);
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
               (filters.GitCommitRecordPredicate?.Invoke(commit) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, Branch branch)
    {
        return Matches(filters.FriendlyName, branch.FriendlyName) &&
               Matches(filters.CanonicalName, branch.CanonicalName) &&
               Matches(filters.IsRemote, branch.IsRemote) &&
               Matches(filters.IsCurrentRepositoryHead, branch.IsCurrentRepositoryHead) &&
               Matches(filters.IsTracking, branch.IsTracking) &&
               (filters.BranchPredicate?.Invoke(branch) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, GitBranchRecord branch)
    {
        return Matches(filters.FriendlyName, branch.FriendlyName) &&
               Matches(filters.CanonicalName, branch.CanonicalName) &&
               Matches(filters.IsRemote, branch.IsRemote) &&
               Matches(filters.IsCurrentRepositoryHead, branch.IsCurrentRepositoryHead) &&
               Matches(filters.IsTracking, branch.IsTracking) &&
               (filters.GitBranchRecordPredicate?.Invoke(branch) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, Tag tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.FriendlyNames, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.CanonicalNames, tag.CanonicalName) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated) &&
               (filters.TagPredicate?.Invoke(tag) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, GitTagRecord tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.FriendlyNames, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.CanonicalNames, tag.CanonicalName) &&
               Matches(filters.TargetSha, tag.TargetSha) &&
               Matches(filters.TargetShas, tag.TargetSha) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated) &&
               (filters.GitTagRecordPredicate?.Invoke(tag) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, GitStashRecord stash)
    {
        return Matches(filters.Selector, stash.Selector) &&
               Matches(filters.Selectors, stash.Selector) &&
               Matches(filters.Sha, stash.Sha) &&
               Matches(filters.Shas, stash.Sha) &&
               (filters.GitStashRecordPredicate?.Invoke(stash) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, StashEntity stash)
    {
        return Matches(filters.Selector, stash.Selector) &&
               Matches(filters.Selectors, stash.Selector) &&
               Matches(filters.Sha, stash.Sha) &&
               Matches(filters.Shas, stash.Sha) &&
               (filters.StashEntityPredicate?.Invoke(stash) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, GitRemoteTagRecord tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.FriendlyNames, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.CanonicalNames, tag.CanonicalName) &&
               Matches(filters.ObjectSha, tag.ObjectSha) &&
               Matches(filters.ObjectShas, tag.ObjectSha) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated) &&
               (filters.GitRemoteTagRecordPredicate?.Invoke(tag) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, RemoteTagEntity tag)
    {
        return Matches(filters.FriendlyName, tag.FriendlyName) &&
               Matches(filters.FriendlyNames, tag.FriendlyName) &&
               Matches(filters.CanonicalName, tag.CanonicalName) &&
               Matches(filters.CanonicalNames, tag.CanonicalName) &&
               Matches(filters.ObjectSha, tag.ObjectSha) &&
               Matches(filters.ObjectShas, tag.ObjectSha) &&
               Matches(filters.IsAnnotated, tag.IsAnnotated) &&
               (filters.RemoteTagEntityPredicate?.Invoke(tag) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, Remote remote)
    {
        return Matches(filters.RemoteName, remote.Name) && Matches(filters.Url, remote.Url) &&
               (filters.RemotePredicate?.Invoke(remote) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, GitRemoteRecord remote)
    {
        return Matches(filters.RemoteName, remote.Name) && Matches(filters.Url, remote.Url) &&
               (filters.GitRemoteRecordPredicate?.Invoke(remote) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, StatusEntry entry)
    {
        var state = entry.State.ToString();
        return Matches(filters.State, state) && (filters.StatusEntryPredicate?.Invoke(entry) ?? true);
    }

    public static bool Matches(GitFilterParameters filters, GitStatusRecord entry)
    {
        var state = entry.State.ToString();
        return Matches(filters.State, state) && (filters.GitStatusRecordPredicate?.Invoke(entry) ?? true);
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
        tableName.Equals("filehistory", StringComparison.OrdinalIgnoreCase) ||
        tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
        tableName.Equals("stashes", StringComparison.OrdinalIgnoreCase) ||
        tableName.Equals("remotetags", StringComparison.OrdinalIgnoreCase);

    private static bool CanPushDownOrder(string tableName, IReadOnlyList<OrderByExpression> orderBy)
    {
        return tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) &&
               orderBy.Count == 1 &&
               orderBy[0].Direction == OrderDirection.Ascending &&
               NormalizeColumnName(orderBy[0].Column.Name)
                   .Equals(nameof(TagEntity.CanonicalName), StringComparison.OrdinalIgnoreCase);
    }

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

        if ((tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
             tableName.Equals("remotetags", StringComparison.OrdinalIgnoreCase)) &&
            columnName.Equals(nameof(TagEntity.CanonicalName), StringComparison.OrdinalIgnoreCase))
        {
            return IsFilterColumn(tableName, columnName) &&
                   IsLiteralTypeSupported(tableName, columnName, literal.Value);
        }

        return op is SourcePredicateComparisonOperator.Equal or SourcePredicateComparisonOperator.NotEqual &&
               IsFilterColumn(tableName, columnName) &&
               IsLiteralTypeSupported(tableName, columnName, literal.Value);
    }

    private static bool IsSupportedIn(string tableName, SourcePredicateIn predicate)
    {
        if ((tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
             tableName.Equals("stashes", StringComparison.OrdinalIgnoreCase) ||
             tableName.Equals("remotetags", StringComparison.OrdinalIgnoreCase)) &&
            (predicate.IsNegated || predicate.Values.Count > MaxReferenceInPushdownValues))
            return false;

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
            (tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
             tableName.Equals("remotetags", StringComparison.OrdinalIgnoreCase)) &&
            columnName == nameof(TagEntity.IsAnnotated))
            return value is bool;

        return value is string;
    }

    private static bool IsFilterColumn(string tableName, string columnName)
    {
        return FilterColumnsByTable.TryGetValue(tableName, out var supported) && supported.Contains(columnName);
    }

    private static GitFilterParameters ExtractFilters(string tableName, SourcePredicateExpression? predicate)
    {
        var filters = new GitFilterParameters();
        ExtractFilters(tableName, predicate, filters);
        BindTypedPredicates(tableName, predicate, filters);
        return filters;
    }

    private static void BindTypedPredicates(string tableName, SourcePredicateExpression? predicate, GitFilterParameters filters)
    {
        switch (tableName)
        {
            case "commits":
                BindNative<Commit>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(CommitEntity.Author)] = "Author.Name",
                        [nameof(CommitEntity.AuthorEmail)] = "Author.Email",
                        [nameof(CommitEntity.Committer)] = "Committer.Name",
                        [nameof(CommitEntity.CommitterEmail)] = "Committer.Email",
                        [nameof(CommitEntity.Sha)] = "Sha",
                        [nameof(CommitEntity.CommittedWhen)] = "Committer.When"
                    });
                filters.CommitPredicate = filters.TypedPredicates.Get<Commit>();
                BindNative<GitCommitRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(CommitEntity.Author)] = "Author",
                        [nameof(CommitEntity.AuthorEmail)] = "AuthorEmail",
                        [nameof(CommitEntity.Committer)] = "Committer",
                        [nameof(CommitEntity.CommitterEmail)] = "CommitterEmail",
                        [nameof(CommitEntity.Sha)] = "Sha",
                        [nameof(CommitEntity.CommittedWhen)] = "CommittedWhen"
                    });
                filters.GitCommitRecordPredicate = filters.TypedPredicates.Get<GitCommitRecord>();
                filters.TypedPredicates.Bind<CommitEntity>(predicate);
                break;
            case "branches":
                BindNative<Branch>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(BranchEntity.FriendlyName)] = "FriendlyName",
                        [nameof(BranchEntity.CanonicalName)] = "CanonicalName",
                        [nameof(BranchEntity.IsRemote)] = "IsRemote",
                        [nameof(BranchEntity.IsCurrentRepositoryHead)] = "IsCurrentRepositoryHead",
                        [nameof(BranchEntity.IsTracking)] = "IsTracking"
                    });
                filters.BranchPredicate = filters.TypedPredicates.Get<Branch>();
                BindNative<GitBranchRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(BranchEntity.FriendlyName)] = "FriendlyName",
                        [nameof(BranchEntity.CanonicalName)] = "CanonicalName",
                        [nameof(BranchEntity.IsRemote)] = "IsRemote",
                        [nameof(BranchEntity.IsCurrentRepositoryHead)] = "IsCurrentRepositoryHead",
                        [nameof(BranchEntity.IsTracking)] = "IsTracking"
                    });
                filters.GitBranchRecordPredicate = filters.TypedPredicates.Get<GitBranchRecord>();
                filters.TypedPredicates.Bind<BranchEntity>(predicate);
                break;
            case "tags":
                BindNative<Tag>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(TagEntity.FriendlyName)] = "FriendlyName",
                        [nameof(TagEntity.CanonicalName)] = "CanonicalName",
                        [nameof(TagEntity.IsAnnotated)] = "IsAnnotated"
                    });
                filters.TagPredicate = filters.TypedPredicates.Get<Tag>();
                BindNative<GitTagRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(TagEntity.FriendlyName)] = "FriendlyName",
                        [nameof(TagEntity.CanonicalName)] = "CanonicalName",
                        [nameof(TagEntity.TargetSha)] = "TargetSha",
                        [nameof(TagEntity.IsAnnotated)] = "IsAnnotated"
                    });
                filters.GitTagRecordPredicate = filters.TypedPredicates.Get<GitTagRecord>();
                filters.TypedPredicates.Bind<TagEntity>(predicate);
                break;
            case "stashes":
                BindNative<GitStashRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(StashEntity.Selector)] = "Selector",
                        [nameof(StashEntity.Sha)] = "Sha",
                        [nameof(StashEntity.Message)] = "Message"
                    });
                filters.GitStashRecordPredicate = filters.TypedPredicates.Get<GitStashRecord>();
                filters.TypedPredicates.Bind<StashEntity>(predicate);
                filters.StashEntityPredicate = filters.TypedPredicates.Get<StashEntity>();
                break;
            case "remotetags":
                BindNative<GitRemoteTagRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(RemoteTagEntity.RemoteName)] = "RemoteName",
                        [nameof(RemoteTagEntity.RemoteUrl)] = "RemoteUrl",
                        [nameof(RemoteTagEntity.FriendlyName)] = "FriendlyName",
                        [nameof(RemoteTagEntity.CanonicalName)] = "CanonicalName",
                        [nameof(RemoteTagEntity.ObjectSha)] = "ObjectSha",
                        [nameof(RemoteTagEntity.PeeledSha)] = "PeeledSha",
                        [nameof(RemoteTagEntity.IsAnnotated)] = "IsAnnotated"
                    });
                filters.GitRemoteTagRecordPredicate = filters.TypedPredicates.Get<GitRemoteTagRecord>();
                filters.TypedPredicates.Bind<RemoteTagEntity>(predicate);
                filters.RemoteTagEntityPredicate = filters.TypedPredicates.Get<RemoteTagEntity>();
                break;
            case "remotes":
                BindNative<Remote>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(RemoteEntity.Name)] = "Name",
                        [nameof(RemoteEntity.Url)] = "Url"
                    });
                filters.RemotePredicate = filters.TypedPredicates.Get<Remote>();
                BindNative<GitRemoteRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(RemoteEntity.Name)] = "Name",
                        [nameof(RemoteEntity.Url)] = "Url"
                    });
                filters.GitRemoteRecordPredicate = filters.TypedPredicates.Get<GitRemoteRecord>();
                filters.TypedPredicates.Bind<RemoteEntity>(predicate);
                break;
            case "status":
                BindNative<StatusEntry>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(StatusEntity.State)] = "State.ToString()"
                    });
                filters.StatusEntryPredicate = filters.TypedPredicates.Get<StatusEntry>();
                BindNative<GitStatusRecord>(predicate, filters.TypedPredicates, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(StatusEntity.State)] = "State.ToString()"
                    });
                filters.GitStatusRecordPredicate = filters.TypedPredicates.Get<GitStatusRecord>();
                filters.TypedPredicates.Bind<StatusEntity>(predicate);
                break;
        }
    }

    private static void BindNative<TRow>(SourcePredicateExpression? predicate, TypedPredicatePlan bindings,
        IReadOnlyDictionary<string, string> paths)
    {
        // Native pruning is optional; the complete entity predicate remains authoritative.
        if (GetPredicateColumns(predicate).Any(name => !paths.ContainsKey(NormalizeColumnName(name))))
            return;
        bindings.Bind<TRow>(predicate, (row, name) =>
        {
            Expression value = row;
            foreach (var member in paths[name].Split('.'))
                value = member == "ToString()"
                    ? Expression.Call(value, nameof(ToString), null)
                    : Expression.PropertyOrField(value, member);
            return value;
        });
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
            case SourcePredicateIn inPredicate:
                ApplyIn(tableName, inPredicate, filters);
                return;
        }
    }

    private static void ApplyIn(string tableName, SourcePredicateIn predicate, GitFilterParameters filters)
    {
        if (predicate.IsNegated || !TryGetColumn(predicate.Expression, out var columnName))
            return;

        var values = predicate.Values
            .OfType<SourcePredicateLiteral>()
            .Select(static literal => literal.Value)
            .OfType<string>()
            .ToArray();
        if (values.Length == 0)
            return;

        switch (tableName, columnName)
        {
            case ("tags", nameof(TagEntity.FriendlyName)):
            case ("remotetags", nameof(RemoteTagEntity.FriendlyName)):
                filters.FriendlyNames.UnionWith(values);
                break;
            case ("tags", nameof(TagEntity.CanonicalName)):
            case ("remotetags", nameof(RemoteTagEntity.CanonicalName)):
                filters.CanonicalNames.UnionWith(values);
                break;
            case ("tags", nameof(TagEntity.TargetSha)):
                filters.TargetShas.UnionWith(values);
                break;
            case ("remotetags", nameof(RemoteTagEntity.ObjectSha)):
                filters.ObjectShas.UnionWith(values);
                break;
            case ("stashes", nameof(StashEntity.Selector)):
                filters.Selectors.UnionWith(values);
                break;
            case ("stashes", nameof(StashEntity.Sha)):
                filters.Shas.UnionWith(values);
                break;
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
            case ("tags", nameof(TagEntity.TargetSha), SourcePredicateComparisonOperator.Equal, string value):
                filters.TargetSha = value;
                break;
            case ("tags", nameof(TagEntity.IsAnnotated), SourcePredicateComparisonOperator.Equal, bool value):
                filters.IsAnnotated = value;
                break;
            case ("stashes", nameof(StashEntity.Selector), SourcePredicateComparisonOperator.Equal, string value):
                filters.Selector = value;
                break;
            case ("stashes", nameof(StashEntity.Sha), SourcePredicateComparisonOperator.Equal, string value):
                filters.Sha = value;
                break;
            case ("remotetags", nameof(RemoteTagEntity.FriendlyName), SourcePredicateComparisonOperator.Equal, string value):
                filters.FriendlyName = value;
                break;
            case ("remotetags", nameof(RemoteTagEntity.CanonicalName), SourcePredicateComparisonOperator.Equal, string value):
                filters.CanonicalName = value;
                break;
            case ("remotetags", nameof(RemoteTagEntity.ObjectSha), SourcePredicateComparisonOperator.Equal, string value):
                filters.ObjectSha = value;
                break;
            case ("remotetags", nameof(RemoteTagEntity.IsAnnotated), SourcePredicateComparisonOperator.Equal, bool value):
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

        if ((tableName.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
             tableName.Equals("remotetags", StringComparison.OrdinalIgnoreCase)) &&
            columnName.Equals(nameof(TagEntity.CanonicalName), StringComparison.OrdinalIgnoreCase) &&
            literal.Value is string canonicalName)
        {
            switch (op)
            {
                case SourcePredicateComparisonOperator.GreaterThan:
                    filters.SetCanonicalNameAfter(canonicalName, false);
                    break;
                case SourcePredicateComparisonOperator.GreaterOrEqual:
                    filters.SetCanonicalNameAfter(canonicalName, true);
                    break;
            }
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

    private static bool Matches(string? expected, string? actual) =>
        expected is null || string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool Matches(IReadOnlySet<string> expected, string? actual) =>
        expected.Count == 0 || actual is not null && expected.Contains(actual);

    private static bool Matches(bool? expected, bool actual) =>
        expected is null || actual == expected.Value;
}
