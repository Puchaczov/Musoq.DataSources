using LibGit2Sharp;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>
/// Frozen, intentionally materializing reference readers used as benchmark baselines. These readers are not
/// production code: their complete collection materialization preserves the pre-streaming comparison boundary.
/// </summary>
internal static class FrozenLegacyGitReferenceReaders
{
    public static long TagsChecksum(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        var tags = repository.Tags.ToArray();
        long checksum = 17;
        foreach (var tag in tags)
        {
            var target = tag.PeeledTarget ?? tag.Target;
            checksum = GitFileHistoryBenchmarks.Fold(
                checksum,
                tag.FriendlyName,
                tag.CanonicalName,
                target?.Sha,
                tag.IsAnnotated.ToString(),
                tag.Annotation?.Message);
        }
        return checksum;
    }

    public static long StashesChecksum(string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        var stashes = repository.Stashes.ToArray();
        long checksum = 17;
        foreach (var stash in stashes)
        {
            checksum = GitFileHistoryBenchmarks.Fold(
                checksum,
                stash.Message,
                stash.Index?.Sha,
                stash.WorkTree?.Sha,
                stash.Untracked?.Sha);
        }
        return checksum;
    }

    public static long NestedTagsChecksum(string repositoryPath) => TagsChecksum(repositoryPath);

    public static long NestedStashesChecksum(string repositoryPath) => StashesChecksum(repositoryPath);
}
