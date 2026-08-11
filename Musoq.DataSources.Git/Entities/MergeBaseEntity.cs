using System;
using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a merge base entity in a Git repository.
/// </summary>
/// <param name="mergeBaseCommit">The commit entity representing the merge base.</param>
/// <param name="firstBranch">The first branch entity involved in the merge base.</param>
/// <param name="secondBranch">The second branch entity involved in the merge base.</param>
/// <param name="repository">The source repository accepted by the compatibility constructor; no native handle is retained.</param>
public class MergeBaseEntity(
    CommitEntity mergeBaseCommit,
    BranchEntity firstBranch,
    BranchEntity secondBranch,
    Repository repository)
{
    // Keep the compatibility parameter part of construction without retaining the native repository handle.
    private readonly bool _compatibilityRepositoryWasProvided = repository is not null;

    /// <summary>
    ///     Gets the commit entity representing the merge base.
    /// </summary>
    public CommitEntity MergeBaseCommit { get; } =
        mergeBaseCommit ?? throw new ArgumentNullException(nameof(mergeBaseCommit));

    /// <summary>
    ///     Gets the first branch entity involved in the merge base.
    /// </summary>
    public BranchEntity FirstBranch { get; } = firstBranch ?? throw new ArgumentNullException(nameof(firstBranch));

    /// <summary>
    ///     Gets the second branch entity involved in the merge base.
    /// </summary>
    public BranchEntity SecondBranch { get; } = secondBranch ?? throw new ArgumentNullException(nameof(secondBranch));
}
