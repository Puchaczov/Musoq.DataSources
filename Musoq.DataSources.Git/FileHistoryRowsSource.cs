using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class FileHistoryRowsSource(
    string repositoryPath,
    string filePattern,
    int skip,
    int take,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<FileHistoryEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<IObjectResolver>(100);
        
        var fromOldest = take < 0;
        var actualTake = Math.Abs(take);
        
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
        };

        var isFullPathPattern = filePattern.Contains('/') || filePattern.Contains('\\');
        var isWildcardPattern = filePattern.Contains('*') || filePattern.Contains('?');
        
        List<string> matchingPaths;
        
        if (!isWildcardPattern && isFullPathPattern)
        {
            matchingPaths = [filePattern.Replace('\\', '/')];
        }
        else
        {
            matchingPaths = FindMatchingPaths(repository, filePattern, isFullPathPattern, isWildcardPattern);
        }

        var skipped = 0;
        var taken = 0;
        
        foreach (var fullPath in matchingPaths)
        {
            if (cancellationToken.IsCancellationRequested || taken >= actualTake)
                break;
            
            IEnumerable<LogEntry> fileHistory;
            
            if (fromOldest)
            {
                var allEntries = new List<LogEntry>(repository.Commits.QueryBy(fullPath, filter));
                allEntries.Reverse();
                fileHistory = allEntries;
            }
            else
            {
                fileHistory = repository.Commits.QueryBy(fullPath, filter);
            }
            
            foreach (var entry in fileHistory)
            {
                if (cancellationToken.IsCancellationRequested || taken >= actualTake)
                    break;
                
                if (skipped < skip)
                {
                    skipped++;
                    continue;
                }
                
                var entity = new FileHistoryEntity(entry.Commit, entry.Path, ChangeKind.Modified);
                chunk.Add(new EntityResolver<FileHistoryEntity>(
                    entity,
                    FileHistoryEntity.NameToIndexMap,
                    FileHistoryEntity.IndexToObjectAccessMap
                ));
                
                taken++;

                if (chunk.Count < 100) 
                    continue;
                
                chunkedSource.Add(chunk.ToArray(), cancellationToken);
                chunk.Clear();
            }
        }
        
        if (chunk.Count > 0)
        {
            chunkedSource.Add(chunk.ToArray(), cancellationToken);
        }

        return Task.CompletedTask;
    }
    
    private static List<string> FindMatchingPaths(Repository repository, string pattern, bool isFullPathPattern, bool isWildcardPattern)
    {
        var matchingPaths = new List<string>();
        var headCommit = repository.Head.Tip;
        
        if (headCommit?.Tree == null)
            return matchingPaths;

        Regex? compiledRegex = null;
        if (isWildcardPattern)
        {
            var normalizedPattern = pattern.Replace('\\', '/');
            var regexPattern = "^" + Regex.Escape(normalizedPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            compiledRegex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        
        var stack = new Stack<(Tree Tree, string Path)>();
        stack.Push((headCommit.Tree, string.Empty));
        
        while (stack.Count > 0)
        {
            var (tree, currentPath) = stack.Pop();
            
            foreach (var entry in tree)
            {
                var entryPath = string.IsNullOrEmpty(currentPath) ? entry.Name : $"{currentPath}/{entry.Name}";

                switch (entry.TargetType)
                {
                    case TreeEntryTargetType.Tree:
                        stack.Push(((Tree)entry.Target, entryPath));
                        break;
                    case TreeEntryTargetType.Blob when IsMatch(entryPath, entry.Name, pattern, isFullPathPattern, isWildcardPattern, compiledRegex):
                        matchingPaths.Add(entryPath);
                        break;
                }
            }
        }
        
        return matchingPaths;
    }
    
    private static bool IsMatch(string fullPath, string fileName, string pattern, bool isFullPathPattern, 
        bool isWildcardPattern, Regex? compiledRegex)
    {
        if (isWildcardPattern && compiledRegex != null)
        {
            var matchTarget = isFullPathPattern ? fullPath : fileName;
            return compiledRegex.IsMatch(matchTarget);
        }
        
        if (isFullPathPattern)
        {
            var normalizedPattern = pattern.Replace('\\', '/');
            return fullPath.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }
        
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
