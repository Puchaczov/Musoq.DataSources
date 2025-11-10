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

internal sealed class RemotesRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<RemoteEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var remotes = repository.Network.Remotes
            .Select(remote => new RemoteEntity(remote))
            .ToList();

        foreach (var remote in remotes)
        {
            chunkedSource.Add(
            [
                new EntityResolver<RemoteEntity>(
                    remote,
                    RemoteEntity.NameToIndexMap,
                    RemoteEntity.IndexToObjectAccessMap
                )
            ], cancellationToken);
        }

        return Task.CompletedTask;
    }
}
