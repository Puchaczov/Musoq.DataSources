using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class RepositoryRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<RepositoryEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var repositoryEntity = new RepositoryEntity(repository);
        chunkedSource.Add(
        [
            new EntityResolver<RepositoryEntity>(
                repositoryEntity,
                RepositoryEntity.NameToIndexMap,
                RepositoryEntity.IndexToObjectAccessMap
            )
        ], cancellationToken);
        return Task.CompletedTask;
    }
}