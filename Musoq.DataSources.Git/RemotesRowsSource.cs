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
        var chunk = new List<IObjectResolver>(100);

        foreach (var remote in repository.Network.Remotes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var entity = new RemoteEntity(remote);
            chunk.Add(new EntityResolver<RemoteEntity>(
                entity,
                RemoteEntity.NameToIndexMap,
                RemoteEntity.IndexToObjectAccessMap
            ));

            if (chunk.Count >= 100)
            {
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
}
