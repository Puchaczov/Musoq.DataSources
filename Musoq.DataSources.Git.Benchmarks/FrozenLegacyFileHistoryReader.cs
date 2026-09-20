using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>
/// Immutable copy of the pre-redesign file-history traversal. Keep this independent from production so benchmark
/// comparisons remain meaningful after <c>Musoq.DataSources.Git</c> changes.
/// </summary>
internal static class FrozenLegacyFileHistoryReader
{
    public static IEnumerable<FrozenLegacyFileHistoryRow> Read(
        string repositoryPath,
        string filePattern,
        int skip,
        int take)
    {
        using var repository = new Repository(repositoryPath);
        var fromOldest = take < 0;
        var actualTake = Math.Abs((long)take);
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
        };
        var normalizedPattern = NormalizePathToRepositoryRelative(filePattern, repositoryPath);
        var isFullPathPattern = normalizedPattern.Contains('/') || normalizedPattern.Contains('\\');
        var isWildcardPattern = normalizedPattern.Contains('*') || normalizedPattern.Contains('?');
        var matchingPaths = !isWildcardPattern && isFullPathPattern
            ? [normalizedPattern.Replace('\\', '/')]
            : FindMatchingPaths(repository, normalizedPattern, isFullPathPattern, isWildcardPattern);
        var skipped = 0;
        long taken = 0;

        foreach (var fullPath in matchingPaths)
        {
            if (taken >= actualTake)
                yield break;

            IEnumerable<LogEntry> history = repository.Commits.QueryBy(fullPath, filter);
            if (fromOldest)
            {
                var allEntries = history.ToList();
                allEntries.Reverse();
                history = allEntries;
            }

            foreach (var entry in history)
            {
                if (taken >= actualTake)
                    yield break;

                if (skipped < skip)
                {
                    skipped++;
                    continue;
                }

                taken++;
                yield return new FrozenLegacyFileHistoryRow(
                    entry.Commit.Sha,
                    entry.Commit.Author.Name,
                    entry.Commit.Author.Email,
                    entry.Commit.Committer.When,
                    entry.Path,
                    "Modified",
                    null);
            }
        }
    }

    private static List<string> FindMatchingPaths(
        Repository repository,
        string pattern,
        bool isFullPathPattern,
        bool isWildcardPattern)
    {
        var matches = new List<string>();
        if (repository.Head.Tip?.Tree is not { } tree)
            return matches;

        Regex? regex = null;
        if (isWildcardPattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern.Replace('\\', '/'))
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        var stack = new Stack<(Tree Tree, string Path)>();
        stack.Push((tree, string.Empty));
        while (stack.Count > 0)
        {
            var (currentTree, currentPath) = stack.Pop();
            foreach (var entry in currentTree)
            {
                var entryPath = string.IsNullOrEmpty(currentPath) ? entry.Name : $"{currentPath}/{entry.Name}";
                if (entry.TargetType == TreeEntryTargetType.Tree)
                {
                    stack.Push(((Tree)entry.Target, entryPath));
                    continue;
                }

                if (entry.TargetType == TreeEntryTargetType.Blob &&
                    IsMatch(entryPath, entry.Name, pattern, isFullPathPattern, isWildcardPattern, regex))
                    matches.Add(entryPath);
            }
        }

        return matches;
    }

    private static bool IsMatch(
        string fullPath,
        string fileName,
        string pattern,
        bool isFullPathPattern,
        bool isWildcardPattern,
        Regex? regex)
    {
        if (isWildcardPattern && regex is not null)
            return regex.IsMatch(isFullPathPattern ? fullPath : fileName);

        return isFullPathPattern
            ? fullPath.Equals(pattern.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
            : fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathToRepositoryRelative(string pattern, string repositoryPath)
    {
        if (!Path.IsPathRooted(pattern))
            return pattern;

        var repository = Path.GetFullPath(repositoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(pattern);
        return candidate.StartsWith(repository, StringComparison.OrdinalIgnoreCase)
            ? candidate[repository.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : pattern;
    }
}

internal sealed record FrozenLegacyFileHistoryRow(
    string CommitSha,
    string Author,
    string AuthorEmail,
    DateTimeOffset CommittedWhen,
    string FilePath,
    string ChangeType,
    string? OldPath);
