using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class CommitsRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    RuntimeContext runtimeContext) : AsyncRowsSourceBase<CommitEntity>(runtimeContext.EndWorkToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<IObjectResolver>(100);
        var filters = GitWhereNodeHelper.ExtractParameters(runtimeContext.QuerySourceInfo.WhereNode);

        var commitFilter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
        };

        foreach (var commit in repository.Commits.QueryBy(commitFilter))
        {
            // Stop early if we've passed the "until" date (commits sorted newest-first)
            if (filters.Until.HasValue && commit.Author.When > filters.Until.Value)
                continue;

            if (filters.Since.HasValue && commit.Author.When < filters.Since.Value)
                break;

            // Filter by SHA prefix or full SHA
            if (!string.IsNullOrEmpty(filters.Sha) &&
                !commit.Sha.StartsWith(filters.Sha, StringComparison.OrdinalIgnoreCase))
                continue;

            // Filter by author name
            if (!string.IsNullOrEmpty(filters.Author) &&
                !string.Equals(commit.Author.Name, filters.Author, StringComparison.OrdinalIgnoreCase))
                continue;

            // Filter by author email
            if (!string.IsNullOrEmpty(filters.AuthorEmail) &&
                !string.Equals(commit.Author.Email, filters.AuthorEmail, StringComparison.OrdinalIgnoreCase))
                continue;

            // Filter by committer name
            if (!string.IsNullOrEmpty(filters.Committer) &&
                !string.Equals(commit.Committer.Name, filters.Committer, StringComparison.OrdinalIgnoreCase))
                continue;

            // Filter by committer email
            if (!string.IsNullOrEmpty(filters.CommitterEmail) &&
                !string.Equals(commit.Committer.Email, filters.CommitterEmail, StringComparison.OrdinalIgnoreCase))
                continue;

            if (cancellationToken.IsCancellationRequested)
                break;

            var entity = new CommitEntity(commit, repository);
            chunk.Add(new EntityResolver<CommitEntity>(
                entity,
                CommitEntity.NameToIndexMap,
                CommitEntity.IndexToObjectAccessMap
            ));

            if (chunk.Count >= 100)
            {
                chunkedSource.Add(chunk.ToArray(), cancellationToken);
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
            chunkedSource.Add(chunk.ToArray(), cancellationToken);

        return Task.CompletedTask;
    }
}
