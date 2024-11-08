using System;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a merge base entity in a Git repository.
/// </summary>
/// <param name="mergeBaseCommit">The commit entity representing the merge base.</param>
/// <param name="firstBranch">The first branch entity involved in the merge base.</param>
/// <param name="secondBranch">The second branch entity involved in the merge base.</param>
public class MergeBaseEntity(CommitEntity mergeBaseCommit, BranchEntity firstBranch, BranchEntity secondBranch)
{
    /// <summary>
    /// Gets the commit entity representing the merge base.
    /// </summary>
    public CommitEntity MergeBaseCommit { get; } = mergeBaseCommit ?? throw new ArgumentNullException(nameof(mergeBaseCommit));

    /// <summary>
    /// Gets the first branch entity involved in the merge base.
    /// </summary>
    public BranchEntity FirstBranch { get; } = firstBranch ?? throw new ArgumentNullException(nameof(firstBranch));

    /// <summary>
    /// Gets the second branch entity involved in the merge base.
    /// </summary>
    public BranchEntity SecondBranch { get; } = secondBranch ?? throw new ArgumentNullException(nameof(secondBranch));
}