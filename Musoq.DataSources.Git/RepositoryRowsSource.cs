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

internal class RepositoryRowsSource(
    string repositoryPath,
    CancellationToken cancellationToken,
    Func<string, Repository> createRepository) : AsyncRowsSourceBase<RepositoryEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var repositoryEntity = new RepositoryEntity(repository);
        chunkedSource.Add(
        [
            new EntityResolver<RepositoryEntity>(repositoryEntity, RepositoryEntity.NameToIndexMap,
                RepositoryEntity.IndexToObjectAccessMap)
        ], cancellationToken);
        return Task.CompletedTask;
    }
}