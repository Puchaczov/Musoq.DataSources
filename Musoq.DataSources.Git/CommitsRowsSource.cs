using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class CommitsRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<CommitEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(IChunkWriter<CommitEntity> writer, CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<CommitEntity>(100);
        var commitFilter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
        };

        foreach (var commit in repository.Commits.QueryBy(commitFilter))
        {
            cancellationToken.ThrowIfCancellationRequested();

            chunk.Add(new CommitEntity(commit, repository));

            if (chunk.Count < 100)
                continue;

            writer.Write(chunk);
            chunk = [];
        }

        if (chunk.Count > 0)
            writer.Write(chunk);

        return Task.CompletedTask;
    }
}
