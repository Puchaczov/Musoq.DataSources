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
        var branches = repository.Branches
            .Select(branch => new BranchEntity(branch, repository))
            .ToList();

        foreach (var branch in branches)
        {
            chunkedSource.Add(
            [
                new EntityResolver<BranchEntity>(
                    branch,
                    BranchEntity.NameToIndexMap,
                    BranchEntity.IndexToObjectAccessMap
                )
            ], cancellationToken);
        }

        return Task.CompletedTask;
    }
}
