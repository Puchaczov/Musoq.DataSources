using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<FileHistoryEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<IObjectResolver>(100);
        
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
        };
        
        IEnumerable<Commit> commits;
        if (IsWildcardPattern(filePattern))
        {
            commits = repository.Commits.QueryBy(filter);
        }
        else
        {
            commits = repository.Commits.QueryBy(filePattern, filter).Select(entry => entry.Commit);
        }
        
        foreach (var commit in commits)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var parent = commit.Parents.FirstOrDefault();
            TreeChanges changes;
            
            if (parent == null)
            {
                changes = repository.Diff.Compare<TreeChanges>(null, commit.Tree);
            }
            else
            {
                changes = repository.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
            }
            
            foreach (var change in changes)
            {
                if (!IsMatch(change.Path, filePattern))
                    continue;

                var entity = new FileHistoryEntity(commit, change);
                chunk.Add(new EntityResolver<FileHistoryEntity>(
                    entity,
                    FileHistoryEntity.NameToIndexMap,
                    FileHistoryEntity.IndexToObjectAccessMap
                ));
                
                if (chunk.Count >= 100)
                {
                    chunkedSource.Add(chunk.ToArray(), cancellationToken);
                    chunk.Clear();
                }
            }
        }
        
        if (chunk.Count > 0)
        {
            chunkedSource.Add(chunk.ToArray(), cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static bool IsWildcardPattern(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?');
    }

    private static bool IsMatch(string path, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(path, regexPattern);
        }
        return path.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
