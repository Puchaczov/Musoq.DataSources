using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class BranchesRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    RuntimeContext runtimeContext) : AsyncRowsSourceBase<BranchEntity>(runtimeContext.EndWorkToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<IObjectResolver>(100);
        var filters = GitWhereNodeHelper.ExtractParameters(runtimeContext.QuerySourceInfo.WhereNode);

        foreach (var branch in repository.Branches)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Apply pushdown filters
            if (!string.IsNullOrEmpty(filters.FriendlyName) &&
                !string.Equals(branch.FriendlyName, filters.FriendlyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(filters.CanonicalName) &&
                !string.Equals(branch.CanonicalName, filters.CanonicalName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (filters.IsRemote.HasValue && branch.IsRemote != filters.IsRemote.Value)
                continue;

            if (filters.IsCurrentRepositoryHead.HasValue && branch.IsCurrentRepositoryHead != filters.IsCurrentRepositoryHead.Value)
                continue;

            if (filters.IsTracking.HasValue && branch.IsTracking != filters.IsTracking.Value)
                continue;

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
