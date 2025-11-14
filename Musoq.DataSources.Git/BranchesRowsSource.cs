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

internal sealed class BranchesRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<BranchEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<IObjectResolver>(100);

        foreach (var branch in repository.Branches)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var entity = new BranchEntity(branch, repository);
            chunk.Add(new EntityResolver<BranchEntity>(
                entity,
                BranchEntity.NameToIndexMap,
                BranchEntity.IndexToObjectAccessMap
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
