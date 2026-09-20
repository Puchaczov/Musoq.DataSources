using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Git;

/// <summary>Library methods for Git snapshots. Native repositories are scoped to an individual operation.</summary>
public class GitLibrary : LibraryBase
{
    /// <summary>Initializes the Git library.</summary>
    public GitLibrary()
    {
    }

    /// <summary>Returns the tree differences between two commits.</summary>
    /// <param name="repository">The repository containing both commits.</param>
    /// <param name="first">The older or left-hand commit.</param>
    /// <param name="second">The newer or right-hand commit.</param>
    /// <returns>Detached difference snapshots; an empty sequence is returned when either commit cannot be resolved.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetween(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        CommitEntity first,
        CommitEntity second)
    {
        return repository.Read(libGitRepository =>
        {
            var firstCommit = LookupCommit(libGitRepository, first);
            var secondCommit = LookupCommit(libGitRepository, second);
            return firstCommit is null || secondCommit is null
                ? []
                : libGitRepository.Diff.Compare<TreeChanges>(firstCommit.Tree, secondCommit.Tree)
                    .Select(change => new DifferenceEntity(change, libGitRepository)).ToArray();
        });
    }

    /// <summary>Returns the tree differences between the tips of two branches.</summary>
    /// <param name="repository">The repository containing both branches.</param>
    /// <param name="first">The older or left-hand branch.</param>
    /// <param name="second">The newer or right-hand branch.</param>
    /// <returns>Detached difference snapshots; an empty sequence is returned when either tip cannot be resolved.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetween(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        BranchEntity first,
        BranchEntity second)
    {
        return repository.Read(libGitRepository =>
        {
            var firstCommit = LookupBranchTip(libGitRepository, first);
            var secondCommit = LookupBranchTip(libGitRepository, second);
            return firstCommit is null || secondCommit is null
                ? []
                : libGitRepository.Diff.Compare<TreeChanges>(firstCommit.Tree, secondCommit.Tree)
                    .Select(change => new DifferenceEntity(change, libGitRepository)).ToArray();
        });
    }

    /// <summary>Returns the tree differences between the current HEAD and a branch tip.</summary>
    /// <param name="repository">The repository to inspect.</param>
    /// <param name="branch">The branch to compare with HEAD.</param>
    /// <returns>Detached difference snapshots, or an empty sequence when either side has no commit.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetweenCurrentAndBranch(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        BranchEntity branch)
    {
        return repository.Read(libGitRepository =>
        {
            var current = libGitRepository.Head?.Tip;
            var target = LookupBranchTip(libGitRepository, branch);
            return current is null || target is null
                ? []
                : libGitRepository.Diff.Compare<TreeChanges>(current.Tree, target.Tree)
                    .Select(change => new DifferenceEntity(change, libGitRepository)).ToArray();
        });
    }

    /// <summary>Returns differences from a commit to a branch tip.</summary>
    /// <param name="repository">The repository containing the commit and branch.</param>
    /// <param name="commit">The commit on the left-hand side.</param>
    /// <param name="branch">The branch on the right-hand side.</param>
    /// <returns>Detached difference snapshots.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetweenCommitAndBranch(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        CommitEntity commit,
        BranchEntity branch)
    {
        return DifferenceBetweenCommitAndBranchCore(repository, commit, branch, reverse: false);
    }

    /// <summary>Returns differences from a branch tip to a commit.</summary>
    /// <param name="repository">The repository containing the branch and commit.</param>
    /// <param name="branch">The branch on the left-hand side.</param>
    /// <param name="commit">The commit on the right-hand side.</param>
    /// <returns>Detached difference snapshots.</returns>
    [BindableMethod]
    public IEnumerable<DifferenceEntity> DifferenceBetweenBranchAndCommit(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        BranchEntity branch,
        CommitEntity commit)
    {
        return DifferenceBetweenCommitAndBranchCore(repository, commit, branch, reverse: true);
    }

    /// <summary>Looks up a commit by SHA and returns a detached commit snapshot.</summary>
    /// <param name="repository">The repository to search.</param>
    /// <param name="sha">The commit SHA or resolvable object identifier.</param>
    /// <returns>The resolved commit snapshot.</returns>
    [BindableMethod]
    public CommitEntity CommitFrom([InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository, string sha)
    {
        return repository.Read(libGitRepository => new CommitEntity(libGitRepository.Lookup<Commit>(sha), libGitRepository));
    }

    /// <summary>Looks up a branch by canonical name and returns a detached branch snapshot.</summary>
    /// <param name="repository">The repository to search.</param>
    /// <param name="canonicalName">The branch's canonical Git name.</param>
    /// <returns>The resolved branch snapshot.</returns>
    [BindableMethod]
    public BranchEntity BranchFrom([InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository, string canonicalName)
    {
        return repository.Read(libGitRepository => new BranchEntity(libGitRepository.Branches[canonicalName], libGitRepository));
    }

    /// <summary>Returns a lazily materialized patch between two commits.</summary>
    /// <param name="repository">The repository containing both commits.</param>
    /// <param name="first">The first commit in the comparison.</param>
    /// <param name="second">The second commit in the comparison.</param>
    /// <returns>A sequence containing one detached patch snapshot, or an empty sequence for missing SHAs.</returns>
    /// <remarks>Large patch content is loaded only when a patch property is projected or accessed.</remarks>
    [BindableMethod]
    public IEnumerable<PatchEntity> PatchBetween(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        CommitEntity first,
        CommitEntity second)
    {
        if (string.IsNullOrWhiteSpace(first.Sha) || string.IsNullOrWhiteSpace(second.Sha))
            return [];

        // The returned row keeps only identifiers. The potentially large textual patch is resolved in a short-lived
        // repository scope if a projected patch property is actually accessed.
        return [new PatchEntity(repository.RepositoryPath, first.Sha, second.Sha)];
    }

    /// <summary>Finds branches whose friendly names match a regular expression.</summary>
    /// <param name="repository">The repository to inspect.</param>
    /// <param name="searchPatternRegex">The regular expression applied to each friendly branch name.</param>
    /// <returns>Detached branch snapshots matching the expression.</returns>
    [BindableMethod]
    public IEnumerable<BranchEntity> SearchForBranches(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        string searchPatternRegex)
    {
        return repository.Read(libGitRepository => libGitRepository.Branches
            .Where(branch => Regex.IsMatch(branch.FriendlyName, searchPatternRegex))
            .Select(branch => new BranchEntity(branch, libGitRepository)).ToArray());
    }

    /// <summary>Returns commits reachable from a branch after its merge base.</summary>
    /// <param name="repository">The repository containing the branch.</param>
    /// <param name="branch">The branch whose commits are requested.</param>
    /// <param name="excludeMergeBase">Whether to exclude the merge-base commit itself; defaults to <see langword="true"/>.</param>
    /// <returns>Detached commits in topological and commit-time order.</returns>
    [BindableMethod]
    public IEnumerable<CommitEntity> GetBranchSpecificCommits(
        [InjectSpecificSource(typeof(RepositoryEntity))] RepositoryEntity repository,
        BranchEntity branch,
        bool excludeMergeBase = true)
    {
        var mergeBase = FindMergeBase(repository, branch);
        if (mergeBase is null || string.IsNullOrWhiteSpace(mergeBase.MergeBaseCommit.Sha))
            return [];

        return repository.Read(libGitRepository =>
        {
            var target = libGitRepository.Branches[branch.CanonicalName]?.Tip;
            var mergeBaseCommit = libGitRepository.Lookup<Commit>(mergeBase.MergeBaseCommit.Sha);
            if (target is null || mergeBaseCommit is null)
                return [];

            var filter = new CommitFilter
            {
                IncludeReachableFrom = target,
                ExcludeReachableFrom = excludeMergeBase ? mergeBaseCommit : mergeBaseCommit.Parents.FirstOrDefault(),
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
            };
            return libGitRepository.Commits.QueryBy(filter)
                .Select(commit => new CommitEntity(commit, libGitRepository)).ToArray();
        });
    }

    /// <summary>Finds the merge base between a branch and its parent branch.</summary>
    /// <param name="repository">The repository to inspect, or <see langword="null"/>.</param>
    /// <param name="branch">The branch to compare, or <see langword="null"/>.</param>
    /// <returns>A detached merge-base snapshot, or <see langword="null"/> when no parent or merge base exists.</returns>
    [BindableMethod]
    public MergeBaseEntity? FindMergeBase(RepositoryEntity? repository, BranchEntity? branch)
    {
        if (repository is null || branch is null)
            return null;

        var parent = branch.ParentBranch;
        if (parent is null)
            return null;

        return repository.Read(libGitRepository =>
        {
            var first = LookupBranchTip(libGitRepository, branch);
            var second = LookupBranchTip(libGitRepository, parent);
            var mergeBase = first is null || second is null
                ? null
                : libGitRepository.ObjectDatabase.FindMergeBase(first, second);
            return mergeBase is null
                ? null
                : new MergeBaseEntity(new CommitEntity(mergeBase, libGitRepository), branch, parent, libGitRepository);
        });
    }

    /// <summary>Declares the maximum-commit aggregate to the Musoq engine.</summary>
    /// <param name="value">The commit value supplied for the current row.</param>
    /// <returns>The aggregate result supplied by <see cref="MaxCommitAggregateKernel"/>.</returns>
    [AggregateFunction(typeof(MaxCommitAggregateKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public CommitEntity? MaxCommit(CommitEntity? value) => AggregateFunction.NotInvoked<CommitEntity?>();

    /// <summary>Declares the parent-aware maximum-commit aggregate to the Musoq engine.</summary>
    /// <param name="value">The commit value supplied for the current row.</param>
    /// <param name="parent">The engine-provided aggregate parent identifier.</param>
    /// <returns>The aggregate result supplied by <see cref="MaxCommitAggregateKernel"/>.</returns>
    [AggregateFunction(typeof(MaxCommitAggregateKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public CommitEntity? MaxCommit(CommitEntity? value, [AggregateParent] int parent) => AggregateFunction.NotInvoked<CommitEntity?>();

    /// <summary>Declares the minimum-commit aggregate to the Musoq engine.</summary>
    /// <param name="value">The commit value supplied for the current row.</param>
    /// <returns>The aggregate result supplied by <see cref="MinCommitAggregateKernel"/>.</returns>
    [AggregateFunction(typeof(MinCommitAggregateKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public CommitEntity? MinCommit(CommitEntity? value) => AggregateFunction.NotInvoked<CommitEntity?>();

    /// <summary>Declares the parent-aware minimum-commit aggregate to the Musoq engine.</summary>
    /// <param name="value">The commit value supplied for the current row.</param>
    /// <param name="parent">The engine-provided aggregate parent identifier.</param>
    /// <returns>The aggregate result supplied by <see cref="MinCommitAggregateKernel"/>.</returns>
    [AggregateFunction(typeof(MinCommitAggregateKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public CommitEntity? MinCommit(CommitEntity? value, [AggregateParent] int parent) => AggregateFunction.NotInvoked<CommitEntity?>();

    /// <summary>Returns the SHA of a commit, or <see langword="null"/> for an empty aggregate.</summary>
    /// <param name="commit">The commit whose SHA is requested.</param>
    /// <returns>The commit SHA.</returns>
    [BindableMethod]
    public string? CommitSha(CommitEntity? commit) => commit?.Sha;

    private static IEnumerable<DifferenceEntity> DifferenceBetweenCommitAndBranchCore(
        RepositoryEntity repository,
        CommitEntity commit,
        BranchEntity branch,
        bool reverse)
    {
        return repository.Read(libGitRepository =>
        {
            var first = LookupCommit(libGitRepository, commit);
            var second = LookupBranchTip(libGitRepository, branch);
            if (first is null || second is null)
                return [];

            var diff = reverse
                ? libGitRepository.Diff.Compare<TreeChanges>(second.Tree, first.Tree)
                : libGitRepository.Diff.Compare<TreeChanges>(first.Tree, second.Tree);
            return diff.Select(change => new DifferenceEntity(change, libGitRepository)).ToArray();
        });
    }

    private static Commit? LookupCommit(Repository repository, CommitEntity entity)
    {
        return string.IsNullOrWhiteSpace(entity.Sha) ? null : repository.Lookup<Commit>(entity.Sha);
    }

    private static Commit? LookupBranchTip(Repository repository, BranchEntity entity)
    {
        return repository.Branches[entity.CanonicalName]?.Tip;
    }
}

/// <summary>Implements the engine kernel that selects the newest non-null commit.</summary>
public static class MaxCommitAggregateKernel
{
    /// <summary>Stores the current maximum-commit aggregate value.</summary>
    public struct State
    {
        /// <summary>Gets or sets the current newest commit.</summary>
        public CommitEntity? Value;
    }

    /// <summary>Includes a commit in the aggregate state when it is newer than the current value.</summary>
    /// <param name="state">The state to update.</param>
    /// <param name="value">The candidate commit.</param>
    public static void Set(ref State state, CommitEntity? value) { if (IsNewer(value, state.Value)) state.Value = value; }

    /// <summary>Gets the aggregate value.</summary>
    /// <param name="state">The aggregate state.</param>
    /// <returns>The newest commit, or <see langword="null"/> when no value was supplied.</returns>
    public static CommitEntity? Get(in State state) => state.Value;

    /// <summary>Merges another partial aggregate state into the current state.</summary>
    /// <param name="state">The state to update.</param>
    /// <param name="other">The partial state to merge.</param>
    public static void Merge(ref State state, in State other) { if (IsNewer(other.Value, state.Value)) state.Value = other.Value; }
    private static bool IsNewer(CommitEntity? candidate, CommitEntity? current) =>
        candidate is not null && (current is null || candidate.CommittedWhen > current.CommittedWhen);
}

/// <summary>Implements the engine kernel that selects the oldest non-null commit.</summary>
public static class MinCommitAggregateKernel
{
    /// <summary>Stores the current minimum-commit aggregate value.</summary>
    public struct State
    {
        /// <summary>Gets or sets the current oldest commit.</summary>
        public CommitEntity? Value;
    }

    /// <summary>Includes a commit in the aggregate state when it is older than the current value.</summary>
    /// <param name="state">The state to update.</param>
    /// <param name="value">The candidate commit.</param>
    public static void Set(ref State state, CommitEntity? value) { if (IsOlder(value, state.Value)) state.Value = value; }

    /// <summary>Gets the aggregate value.</summary>
    /// <param name="state">The aggregate state.</param>
    /// <returns>The oldest commit, or <see langword="null"/> when no value was supplied.</returns>
    public static CommitEntity? Get(in State state) => state.Value;

    /// <summary>Merges another partial aggregate state into the current state.</summary>
    /// <param name="state">The state to update.</param>
    /// <param name="other">The partial state to merge.</param>
    public static void Merge(ref State state, in State other) { if (IsOlder(other.Value, state.Value)) state.Value = other.Value; }
    private static bool IsOlder(CommitEntity? candidate, CommitEntity? current) =>
        candidate is not null && (current is null || candidate.CommittedWhen < current.CommittedWhen);
}
