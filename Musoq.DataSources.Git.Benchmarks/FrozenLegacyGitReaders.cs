using LibGit2Sharp;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>
/// Frozen pre-detachment read shapes for non-history tables. Keep these independent from production so later
/// optimizations remain accountable to a stable baseline.
/// </summary>
internal static class FrozenLegacyGitReaders
{
    public static long CommitsChecksum(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        long checksum = 17;
        foreach (var commit in repository.Commits.QueryBy(new CommitFilter
                 { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time }))
            checksum = GitFileHistoryBenchmarks.Fold(checksum, commit.Sha, commit.Author.Name, commit.Author.Email,
                commit.Committer.When.ToString("O"));
        return checksum;
    }

    public static long ReferencesChecksum(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        long checksum = 17;
        foreach (var branch in repository.Branches)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, branch.FriendlyName, branch.CanonicalName,
                branch.IsRemote.ToString());
        foreach (var tag in repository.Tags)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, tag.FriendlyName, tag.CanonicalName,
                tag.IsAnnotated.ToString());
        foreach (var remote in repository.Network.Remotes)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, remote.Name, remote.Url, remote.PushUrl);
        return checksum;
    }

    public static long StatusChecksum(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        long checksum = 17;
        foreach (var entry in repository.RetrieveStatus())
            checksum = GitFileHistoryBenchmarks.Fold(checksum, entry.FilePath, entry.State.ToString(),
                IsStaged(entry.State) ? "Staged" : "NotStaged", IsWorktreeModified(entry.State) ? "Modified" : "Unmodified");
        return checksum;
    }

    public static long BlameAndNestedChecksum(string repositoryPath, string filePath)
    {
        using var repository = new Repository(repositoryPath);
        long checksum = 17;
        foreach (var hunk in repository.Blame(filePath))
            checksum = GitFileHistoryBenchmarks.Fold(checksum, hunk.FinalCommit.Sha, hunk.FinalStartLineNumber.ToString(),
                hunk.LineCount.ToString());
        foreach (var commit in repository.Head.Commits.Take(32))
            checksum = GitFileHistoryBenchmarks.Fold(checksum, commit.Sha, commit.Parents.Count().ToString());
        return checksum;
    }

    private static bool IsStaged(FileStatus state) =>
        state.HasFlag(FileStatus.NewInIndex) || state.HasFlag(FileStatus.ModifiedInIndex) ||
        state.HasFlag(FileStatus.DeletedFromIndex) || state.HasFlag(FileStatus.RenamedInIndex) ||
        state.HasFlag(FileStatus.TypeChangeInIndex);

    private static bool IsWorktreeModified(FileStatus state) =>
        state.HasFlag(FileStatus.NewInWorkdir) || state.HasFlag(FileStatus.ModifiedInWorkdir) ||
        state.HasFlag(FileStatus.DeletedFromWorkdir) || state.HasFlag(FileStatus.RenamedInWorkdir) ||
        state.HasFlag(FileStatus.TypeChangeInWorkdir);
}
