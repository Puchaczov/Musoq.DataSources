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
    internal readonly Branch LibGitBranch;

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchEntity"/> class.
    /// </summary>
    /// <param name="branch">The Git branch to wrap.</param>
    public BranchEntity(Branch branch)
    {
        LibGitBranch = branch;
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
    public BranchEntity TrackedBranch => new(LibGitBranch.TrackedBranch);

    /// <summary>
    /// Gets the branch tracking details entity if the branch has tracking details.
    /// </summary>
    public BranchTrackingDetailsEntity BranchTrackingDetails => new(LibGitBranch.TrackingDetails);

    /// <summary>
    /// Gets the commit entity representing the tip of the branch.
    /// </summary>
    public CommitEntity Tip => new(LibGitBranch.Tip);

    /// <summary>
    /// Gets the collection of commit entities in the branch.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Commits => LibGitBranch.Commits.Select(f => new CommitEntity(f));

    /// <summary>
    /// Gets the canonical name of the upstream branch.
    /// </summary>
    public string UpstreamBranchCanonicalName => LibGitBranch.UpstreamBranchCanonicalName;

    /// <summary>
    /// Gets the name of the remote associated with the branch.
    /// </summary>
    public string RemoteName => LibGitBranch.RemoteName;
}