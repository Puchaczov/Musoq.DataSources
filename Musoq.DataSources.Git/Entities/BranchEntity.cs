using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a Git branch entity with various properties and related entities.
/// </summary>
public class BranchEntity
{
    private readonly Repository _libGitRepository;

    internal readonly Branch LibGitBranch;

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchEntity"/> class.
    /// </summary>
    /// <param name="branch">The Git branch to wrap.</param>
    /// <param name="repository">The Git repository the branch belongs to.</param>
    public BranchEntity(Branch branch, Repository repository)
    {
        LibGitBranch = branch;
        _libGitRepository = repository;
    }

    /// <summary>
    /// A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    /// A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<BranchEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    /// An array of schema columns representing the structure of the branch entity.
    /// </summary>
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

    /// <summary>
    /// Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static BranchEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(FriendlyName), 0},
            {nameof(CanonicalName), 1},
            {nameof(IsRemote), 2},
            {nameof(IsTracking), 3},
            {nameof(IsCurrentRepositoryHead), 4},
            {nameof(TrackedBranch), 5},
            {nameof(BranchTrackingDetails), 6},
            {nameof(Tip), 7},
            {nameof(Commits), 8},
            {nameof(UpstreamBranchCanonicalName), 9},
            {nameof(RemoteName), 10},
            {nameof(ParentBranch), 11},
            {nameof(Self), 12}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<BranchEntity, object?>>
        {
            {0, entity => entity.FriendlyName},
            {1, entity => entity.CanonicalName},
            {2, entity => entity.IsRemote},
            {3, entity => entity.IsTracking},
            {4, entity => entity.IsCurrentRepositoryHead},
            {5, entity => entity.TrackedBranch},
            {6, entity => entity.BranchTrackingDetails},
            {7, entity => entity.Tip},
            {8, entity => entity.Commits},
            {9, entity => entity.UpstreamBranchCanonicalName},
            {10, entity => entity.RemoteName},
            {11, entity => entity.ParentBranch},
            {12, entity => entity.Self}
        };
    }

    /// <summary>
    /// Gets the friendly name of the branch.
    /// </summary>
    public string FriendlyName => LibGitBranch.FriendlyName;

    /// <summary>
    /// Gets the canonical name of the branch.
    /// </summary>
    public string CanonicalName => LibGitBranch.CanonicalName;

    /// <summary>
    /// Gets a value indicating whether the branch is remote.
    /// </summary>
    public bool IsRemote => LibGitBranch.IsRemote;

    /// <summary>
    /// Gets a value indicating whether the branch is tracking another branch.
    /// </summary>
    public bool IsTracking => LibGitBranch.IsTracking;

    /// <summary>
    /// Gets a value indicating whether the branch is the current repository head.
    /// </summary>
    public bool IsCurrentRepositoryHead => LibGitBranch.IsCurrentRepositoryHead;

    /// <summary>
    /// Gets the tracked branch entity if the branch is tracking another branch.
    /// </summary>
    public BranchEntity TrackedBranch => new(LibGitBranch.TrackedBranch, _libGitRepository);

    /// <summary>
    /// Gets the branch tracking details entity if the branch has tracking details.
    /// </summary>
    public BranchTrackingDetailsEntity BranchTrackingDetails => new(LibGitBranch.TrackingDetails, _libGitRepository);

    /// <summary>
    /// Gets the commit entity representing the tip of the branch.
    /// </summary>
    public CommitEntity Tip => new(LibGitBranch.Tip, _libGitRepository);

    /// <summary>
    /// Gets the collection of commit entities in the branch.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits =>
        LibGitBranch.Commits.Select(f => new CommitEntity(f, _libGitRepository));

    /// <summary>
    /// Gets the canonical name of the upstream branch.
    /// </summary>
    public string UpstreamBranchCanonicalName => LibGitBranch.UpstreamBranchCanonicalName;

    /// <summary>
    /// Gets the name of the remote associated with the branch.
    /// </summary>
    public string RemoteName => LibGitBranch.RemoteName;

    /// <summary>
    /// Gets the parent branch of the branch, if any.
    /// </summary>
    public BranchEntity? ParentBranch
    {
        get
        {
            var branch = LibGitBranch;
            var libGitBranch = _libGitRepository.Branches[branch.FriendlyName];
        
            if (libGitBranch == null)
                return null;

            try
            {
                var possibleParents = _libGitRepository.Branches
                    .Where(b => !b.IsRemote && 
                                b.FriendlyName != branch.FriendlyName &&
                                !b.FriendlyName.StartsWith("origin/"))
                    .ToList();

                var branchesWithMergeBases = possibleParents
                    .Select(parentBranch => new
                    {
                        Branch = parentBranch,
                        MergeBase = _libGitRepository.ObjectDatabase.FindMergeBase(
                            libGitBranch.Tip,
                            parentBranch.Tip)
                    })
                    .Where(x => x.MergeBase != null)
                    .ToList();

                var orderedBranches = branchesWithMergeBases
                    .Where(x => x.MergeBase.Sha != branch.Tip.Sha)
                    .Select(x => new 
                    {
                        x.Branch,
                        x.MergeBase,
                        CommitCount = _libGitRepository.Commits.QueryBy(new CommitFilter 
                        {
                            IncludeReachableFrom = branch.Tip,
                            ExcludeReachableFrom = x.MergeBase
                        }).Count()
                    })
                    .OrderBy(x => x.CommitCount);

                var parentBranch = orderedBranches.FirstOrDefault()?.Branch;

                return parentBranch != null 
                    ? new BranchEntity(parentBranch, _libGitRepository) 
                    : null;
            }
            catch
            {
                var defaultBranch = _libGitRepository.Branches["main"] ?? _libGitRepository.Branches["master"];
                return defaultBranch != null ? new BranchEntity(defaultBranch, _libGitRepository) : null;
            }
        }
    }

    /// <summary>
    /// Gets the branch entity itself.
    /// </summary>
    public BranchEntity Self => this;
}