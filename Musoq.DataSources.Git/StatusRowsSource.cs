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

internal sealed class StatusRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<StatusEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var status = repository.RetrieveStatus();
        
        foreach (var entry in status)
        {
            var entity = new StatusEntity(entry);
            chunkedSource.Add(
            [
                new EntityResolver<StatusEntity>(
                    entity,
                    StatusEntity.NameToIndexMap,
                    StatusEntity.IndexToObjectAccessMap
                )
            ], cancellationToken);
        }

        return Task.CompletedTask;
    }
}
