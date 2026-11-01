using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

internal sealed partial class GitRepositoryContext
{
    public static GitRepositoryContext FromPath(string path) => new(GitRepositoryLocation.Resolve(path),
        new Dictionary<string, string>(), CancellationToken.None);

    internal Repository OpenNative()
    {
        Token.ThrowIfCancellationRequested();
        Diagnostics.AddMetric("Git.RepositoryOpens", 1);
        return new Repository(Location.ExecutionRoot);
    }

    private IEnumerable<CommitEntity> NativeCommits(string revision, bool single, string? exclude, bool topological)
    {
        using var repository = OpenNative();
        IEnumerable<Commit> commits;
        if (single)
            commits = repository.Lookup<Commit>(revision) is { } commit ? [commit] : [];
        else if (!topological && exclude is null && revision == "HEAD") commits = repository.Commits;
        else if (!topological && exclude is null && repository.Branches[revision] is { } branch) commits = branch.Commits;
        else commits = repository.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = revision,
            ExcludeReachableFrom = exclude, SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time });
        foreach (var commit in commits)
        {
            Token.ThrowIfCancellationRequested();
            yield return new CommitEntity(commit, repository) { ReadContext = this };
        }
    }

    private BranchEntity NativeBranch(Branch branch, Repository repository) =>
        new(branch, repository) { ReadContext = this };

    private IEnumerable<BranchEntity> NativeBranches()
    {
        using var repository = OpenNative();
        foreach (var branch in repository.Branches)
        {
            Token.ThrowIfCancellationRequested();
            yield return NativeBranch(branch, repository);
        }
    }

    internal CommitEntity CommitIdentity(string sha) => new(Location.GitDirectory, sha, null, null, null, null,
        null, null, default) { ReadContext = this, DetailsLoader = () => CommitFrom(sha) };
}
