using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Git;

/// <summary>
/// Represents a Git library with various methods for querying Git repositories.
/// </summary>
public class GitLibrary : LibraryBase
{
    /// <summary>
    /// Gets the differences between two commits.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="first">The first commit entity.</param>
    /// <param name="second">The second commit entity.</param>
    /// <returns>An enumerable of difference entities.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetween(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        CommitEntity first,
        CommitEntity second)
    {
        var firstLibGitCommit = first.LibGitCommit;
        
        if (firstLibGitCommit == null)
            yield break;
        
        var secondLibGitCommit = second.LibGitCommit;
        
        if (secondLibGitCommit == null)
            yield break;
        
        var diff = repository.LibGitRepository.Diff.Compare<TreeChanges>(firstLibGitCommit.Tree, secondLibGitCommit.Tree);
        
        foreach (var treeEntryChange in diff)
        {
            yield return new DifferenceEntity(treeEntryChange, repository.LibGitRepository);
        }
    }

    /// <summary>
    /// Gets the differences between two branches.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="first">The first branch entity.</param>
    /// <param name="second">The second branch entity.</param>
    /// <returns>An enumerable of difference entities.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetween(
        [InjectSpecificSource(typeof(RepositoryEntity))]
        RepositoryEntity repository,
        BranchEntity first,
        BranchEntity second)
    {
        var firstLibGitBranch = first.LibGitBranch;
        var secondLibGitBranch = second.LibGitBranch;
        var firstLibGitCommit = firstLibGitBranch.Tip;
        var secondLibGitCommit = secondLibGitBranch.Tip;
        var diff = repository.LibGitRepository.Diff.Compare<TreeChanges>(firstLibGitCommit.Tree, secondLibGitCommit.Tree);

        foreach (var treeEntryChange in diff)
        {
            yield return new DifferenceEntity(treeEntryChange, repository.LibGitRepository);
        }
    }

    /// <summary>
    /// Gets the differences between the current branch and a specified branch.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="branch">The branch entity.</param>
    /// <returns>An enumerable of difference entities.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetweenCurrentAndBranch(
        [InjectSpecificSource(typeof(RepositoryEntity))]
        RepositoryEntity repository,
        BranchEntity branch)
    {
        var currentBranch = repository.LibGitRepository.Head;
        var branchLibGitBranch = branch.LibGitBranch;
        var currentLibGitCommit = currentBranch.Tip;
        var branchLibGitCommit = branchLibGitBranch.Tip;
        var diff = repository.LibGitRepository.Diff.Compare<TreeChanges>(currentLibGitCommit.Tree, branchLibGitCommit.Tree);

        foreach (var treeEntryChange in diff)
        {
            yield return new DifferenceEntity(treeEntryChange, repository.LibGitRepository);
        }
    }

    /// <summary>
    /// Gets the differences between a commit and a branch.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="commit">The commit entity.</param>
    /// <param name="branch">The branch entity.</param>
    /// <returns>An enumerable of difference entities.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetweenCommitAndBranch(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        CommitEntity commit,
        BranchEntity branch)
    {
        var branchLibGitBranch = branch.LibGitBranch;
        var commitLibGitCommit = commit.LibGitCommit;
        
        if (commitLibGitCommit == null)
            yield break;
        
        var branchLibGitCommit = branchLibGitBranch.Tip;
        var diff = repository.LibGitRepository.Diff.Compare<TreeChanges>(commitLibGitCommit.Tree, branchLibGitCommit.Tree);

        foreach (var treeEntryChange in diff)
        {
            yield return new DifferenceEntity(treeEntryChange, repository.LibGitRepository);
        }
    }

    /// <summary>
    /// Gets the differences between a branch and a commit.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="branch">The branch entity.</param>
    /// <param name="commit">The commit entity.</param>
    /// <returns>An enumerable of difference entities.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetweenBranchAndCommit(
        [InjectSpecificSource(typeof(RepositoryEntity))]
        RepositoryEntity repository,
        BranchEntity branch,
        CommitEntity commit)
    {
        var branchLibGitBranch = branch.LibGitBranch;
        var commitLibGitCommit = commit.LibGitCommit;
        
        if (commitLibGitCommit == null)
            yield break;
        
        var branchLibGitCommit = branchLibGitBranch.Tip;
        var diff = repository.LibGitRepository.Diff.Compare<TreeChanges>(branchLibGitCommit.Tree, commitLibGitCommit.Tree);

        foreach (var treeEntryChange in diff)
        {
            yield return new DifferenceEntity(treeEntryChange, repository.LibGitRepository);
        }
    }

    /// <summary>
    /// Gets a commit entity from a SHA.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="sha">The SHA of the commit.</param>
    /// <returns>The commit entity.</returns>
    [BindableMethod]
    public CommitEntity CommitFrom([InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository, string sha)
    {
        var commit = repository.LibGitRepository.Lookup<Commit>(sha);
        return new CommitEntity(commit);
    }

    /// <summary>
    /// Gets a branch entity from a canonical name.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="canonicalName">The canonical name of the branch.</param>
    /// <returns>The branch entity.</returns>
    [BindableMethod]
    public BranchEntity BranchFrom([InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository, string canonicalName)
    {
        var branch = repository.LibGitRepository.Branches[canonicalName];
        return new BranchEntity(branch);
    }
    
    /// <summary>
    /// Gets the patch between two commits.
    /// </summary>
    /// <param name="repository">The repository entity.</param>
    /// <param name="first">The first commit entity.</param>
    /// <param name="second">The second commit entity.</param>
    /// <returns>The patch entity.</returns>
    [BindableMethod]
    public IEnumerable<PatchEntity> PatchBetween(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        CommitEntity first,
        CommitEntity second)
    {
        var firstLibGitCommit = first.LibGitCommit;
        
        if (firstLibGitCommit == null)
            yield break;
        
        var secondLibGitCommit = second.LibGitCommit;
        
        if (secondLibGitCommit == null)
            yield break;
        
        yield return new PatchEntity(repository.LibGitRepository.Diff.Compare<Patch>(firstLibGitCommit.Tree, secondLibGitCommit.Tree));
    }
}