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

internal sealed class CommitsRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<CommitEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var commits = repository.Commits
            .Select(commit => new CommitEntity(commit, repository))
            .ToList();

        foreach (var commit in commits)
        {
            chunkedSource.Add(
            [
                new EntityResolver<CommitEntity>(
                    commit,
                    CommitEntity.NameToIndexMap,
                    CommitEntity.IndexToObjectAccessMap
                )
            ], cancellationToken);
        }

        return Task.CompletedTask;
    }
}
