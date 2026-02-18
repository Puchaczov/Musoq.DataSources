using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class StatusRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    RuntimeContext runtimeContext) : AsyncRowsSourceBase<StatusEntity>(runtimeContext.EndWorkToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var status = repository.RetrieveStatus();
        var filters = GitWhereNodeHelper.ExtractParameters(runtimeContext.QuerySourceInfo.WhereNode);

        foreach (var entry in status)
        {
            if (cancellationToken.IsCancellationRequested)
                break;


            if (!string.IsNullOrEmpty(filters.State) &&
                !string.Equals(entry.State.ToString(), filters.State, StringComparison.OrdinalIgnoreCase))
                continue;
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