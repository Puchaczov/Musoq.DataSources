using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;

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
    public IEnumerable<CommitEntity> Commits => LibGitBranch.Commits.Select(f => new CommitEntity(f, _libGitRepository));

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

            if (libGitBranch.TrackedBranch != null)
                return new BranchEntity(libGitBranch.TrackedBranch, _libGitRepository);

            var branchTip = libGitBranch.Tip;
            var candidates = new Dictionary<Branch, int>();

            foreach (var possibleParent in _libGitRepository.Branches.Where(b => b.FriendlyName != branch.FriendlyName && !b.IsRemote))
            {
                try
                {
                    var mergeBase = _libGitRepository.ObjectDatabase.FindMergeBase(branchTip, possibleParent.Tip);
                    if (mergeBase == null) continue;
                    
                    var filter = new CommitFilter
                    {
                        IncludeReachableFrom = branchTip,
                        ExcludeReachableFrom = possibleParent.Tip
                    };
                    
                    var distance = _libGitRepository.Commits.QueryBy(filter).Count();
                    
                    if (distance == 0)
                        continue;
                    
                    candidates[possibleParent] = distance;
                }
                catch
                {
                    // Skip if we can't find merge base
                }
            }

            if (candidates.Count != 0)
            {
                var closestCandidate = candidates.OrderBy(c => c.Value).First();
                return new BranchEntity(closestCandidate.Key, _libGitRepository);
            }

            var defaultBranch = _libGitRepository.Branches["main"] ?? _libGitRepository.Branches["master"];
            return defaultBranch != null ? new BranchEntity(defaultBranch, _libGitRepository) : null;
        }
    }
    
    /// <summary>
    /// Gets the branch entity itself.
    /// </summary>
    public BranchEntity Self => this;
}