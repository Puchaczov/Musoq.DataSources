using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a detached Git branch snapshot. Nested values reopen a short-lived repository scope.
/// </summary>
public class BranchEntity
{
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    /// <summary>Maps row indexes to the corresponding property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<BranchEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a branch row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(FriendlyName), 0, typeof(string)),
        new SchemaColumn(nameof(CanonicalName), 1, typeof(string)),
        new SchemaColumn(nameof(IsRemote), 2, typeof(bool)),
        new SchemaColumn(nameof(IsTracking), 3, typeof(bool)),
        new SchemaColumn(nameof(IsCurrentRepositoryHead), 4, typeof(bool)),
        new SchemaColumn(nameof(TrackedBranch), 5, typeof(BranchEntity)),
        new SchemaColumn(nameof(BranchTrackingDetails), 6, typeof(BranchTrackingDetailsEntity)),
        new SchemaColumn(nameof(Tip), 7, typeof(CommitEntity)),
        new SchemaColumn(nameof(Commits), 8, typeof(IEnumerable<CommitEntity>)),
        new SchemaColumn(nameof(UpstreamBranchCanonicalName), 9, typeof(string)),
        new SchemaColumn(nameof(RemoteName), 10, typeof(string)),
        new SchemaColumn(nameof(ParentBranch), 11, typeof(BranchEntity)),
        new SchemaColumn(nameof(Self), 12, typeof(BranchEntity))
    ];

    private readonly string _repositoryPath;
    private readonly string _friendlyName;
    private readonly string _canonicalName;
    private readonly bool _isRemote;
    private readonly bool _isTracking;
    private readonly bool _isCurrentRepositoryHead;
    private readonly string? _trackedBranchCanonicalName;
    private readonly int? _aheadBy;
    private readonly int? _behindBy;
    private readonly string? _tipSha;
    private readonly string? _upstreamBranchCanonicalName;
    private readonly string? _remoteName;
    private readonly GitNestedSnapshot<BranchEntity> _parentBranch = new();
    private readonly GitNestedSnapshot<BranchEntity> _trackedBranch = new();
    private readonly GitNestedSnapshot<CommitEntity> _tip = new();
    private readonly GitNestedSnapshot<IReadOnlyList<CommitEntity>> _commits = new();

    static BranchEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(FriendlyName), 0 }, { nameof(CanonicalName), 1 }, { nameof(IsRemote), 2 },
            { nameof(IsTracking), 3 }, { nameof(IsCurrentRepositoryHead), 4 }, { nameof(TrackedBranch), 5 },
            { nameof(BranchTrackingDetails), 6 }, { nameof(Tip), 7 }, { nameof(Commits), 8 },
            { nameof(UpstreamBranchCanonicalName), 9 }, { nameof(RemoteName), 10 }, { nameof(ParentBranch), 11 },
            { nameof(Self), 12 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<BranchEntity, object?>>
        {
            { 0, entity => entity.FriendlyName }, { 1, entity => entity.CanonicalName },
            { 2, entity => entity.IsRemote }, { 3, entity => entity.IsTracking },
            { 4, entity => entity.IsCurrentRepositoryHead }, { 5, entity => entity.TrackedBranch },
            { 6, entity => entity.BranchTrackingDetails }, { 7, entity => entity.Tip },
            { 8, entity => entity.Commits }, { 9, entity => entity.UpstreamBranchCanonicalName },
            { 10, entity => entity.RemoteName }, { 11, entity => entity.ParentBranch }, { 12, entity => entity.Self }
        };
    }

    /// <summary>Creates a snapshot from a LibGit2Sharp branch without retaining it or its repository.</summary>
    public BranchEntity(Branch branch, Repository repository)
        : this(
            repository.Info.Path,
            branch.FriendlyName,
            branch.CanonicalName,
            branch.IsRemote,
            branch.IsTracking,
            branch.IsCurrentRepositoryHead,
            branch.TrackedBranch?.CanonicalName,
            branch.TrackingDetails?.AheadBy,
            branch.TrackingDetails?.BehindBy,
            branch.Tip?.Sha,
            branch.UpstreamBranchCanonicalName,
            branch.RemoteName)
    {
    }

    internal BranchEntity(
        string repositoryPath,
        string friendlyName,
        string canonicalName,
        bool isRemote,
        bool isTracking,
        bool isCurrentRepositoryHead,
        string? trackedBranchCanonicalName,
        int? aheadBy,
        int? behindBy,
        string? tipSha,
        string? upstreamBranchCanonicalName,
        string? remoteName)
    {
        _repositoryPath = repositoryPath;
        _friendlyName = friendlyName;
        _canonicalName = canonicalName;
        _isRemote = isRemote;
        _isTracking = isTracking;
        _isCurrentRepositoryHead = isCurrentRepositoryHead;
        _trackedBranchCanonicalName = trackedBranchCanonicalName;
        _aheadBy = aheadBy;
        _behindBy = behindBy;
        _tipSha = tipSha;
        _upstreamBranchCanonicalName = upstreamBranchCanonicalName;
        _remoteName = remoteName;
    }

    /// <summary>Gets the display name of the branch.</summary>
    public string FriendlyName => _friendlyName;
    /// <summary>Gets the canonical Git reference name of the branch.</summary>
    public string CanonicalName => _canonicalName;
    /// <summary>Gets a value indicating whether this is a remote-tracking branch.</summary>
    public bool IsRemote => _isRemote;
    /// <summary>Gets a value indicating whether the branch tracks another branch.</summary>
    public bool IsTracking => _isTracking;
    /// <summary>Gets a value indicating whether this branch is the repository's current HEAD.</summary>
    public bool IsCurrentRepositoryHead => _isCurrentRepositoryHead;

    /// <summary>Gets the tracked branch, resolving and caching a detached snapshot on first access.</summary>
    public BranchEntity? TrackedBranch => _trackedBranch.GetOrCreate(() => ResolveBranch(_trackedBranchCanonicalName));

    /// <summary>Gets the ahead/behind counts for the branch's upstream tracking relationship.</summary>
    public BranchTrackingDetailsEntity BranchTrackingDetails => new(_aheadBy, _behindBy);

    /// <summary>Gets the tip commit, resolving and caching a detached snapshot on first access.</summary>
    public CommitEntity? Tip => _tip.GetOrCreate(() => ResolveCommit(_tipSha));

    /// <summary>Gets the commits reachable from this branch, materializing detached snapshots lazily.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits
    {
        get => _commits.GetOrCreate(() =>
        {
            using var repository = new Repository(_repositoryPath);
            return repository.Branches[_canonicalName]?.Commits
                .Select(commit => new CommitEntity(commit, repository))
                .ToArray() ?? Array.Empty<CommitEntity>();
        }) ?? Array.Empty<CommitEntity>();
    }

    /// <summary>Gets the canonical name of the configured upstream branch, if any.</summary>
    public string? UpstreamBranchCanonicalName => _upstreamBranchCanonicalName;
    /// <summary>Gets the configured remote name, if any.</summary>
    public string? RemoteName => _remoteName;

    /// <summary>Gets the best matching parent branch, resolving and caching a detached snapshot on first access.</summary>
    public BranchEntity? ParentBranch
    {
        get => _parentBranch.GetOrCreate(ResolveParentBranch);
    }

    private BranchEntity? ResolveBranch(string? canonicalName)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
            return null;

        using var repository = new Repository(_repositoryPath);
        var branch = repository.Branches[canonicalName];
        return branch is null ? null : new BranchEntity(branch, repository);
    }

    private CommitEntity? ResolveCommit(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            return null;

        using var repository = new Repository(_repositoryPath);
        var commit = repository.Lookup<Commit>(sha);
        return commit is null ? null : new CommitEntity(commit, repository);
    }

    private BranchEntity? ResolveParentBranch()
    {
        try
        {
            using var repository = new Repository(_repositoryPath);
            var branch = repository.Branches[_canonicalName];
            if (branch is null || branch.Tip is null)
                return null;

            var parent = repository.Branches
                .Where(candidate => !candidate.IsRemote && candidate.Tip is not null &&
                                    candidate.FriendlyName != branch.FriendlyName &&
                                    !candidate.FriendlyName.StartsWith("origin/"))
                .Select(candidate => new { Branch = candidate, MergeBase = repository.ObjectDatabase.FindMergeBase(branch.Tip, candidate.Tip) })
                .Where(candidate => candidate.MergeBase is not null && candidate.MergeBase.Sha != branch.Tip.Sha)
                .Select(candidate => new
                {
                    candidate.Branch,
                    CommitCount = repository.Commits.QueryBy(new CommitFilter
                    {
                        IncludeReachableFrom = branch.Tip,
                        ExcludeReachableFrom = candidate.MergeBase
                    }).Count()
                })
                .OrderBy(candidate => candidate.CommitCount)
                .FirstOrDefault()?.Branch;

            return parent is null ? null : new BranchEntity(parent, repository);
        }
        catch (LibGit2SharpException exception)
        {
            throw new InvalidOperationException(
                $"Git could not determine the parent branch for '{_canonicalName}'.", exception);
        }
    }

    /// <summary>Gets this row instance.</summary>
    public BranchEntity Self => this;

    internal string RepositoryPath => _repositoryPath;
    internal string? TipSha => _tipSha;
}
