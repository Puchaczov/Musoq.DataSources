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

internal sealed class TagsRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    RuntimeContext runtimeContext) : AsyncRowsSourceBase<TagEntity>(runtimeContext.EndWorkToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<IObjectResolver>(100);
        var filters = GitWhereNodeHelper.ExtractParameters(runtimeContext.QuerySourceInfo.WhereNode);

        foreach (var tag in repository.Tags)
        {
            if (cancellationToken.IsCancellationRequested)
                break;


            if (!string.IsNullOrEmpty(filters.FriendlyName) &&
                !string.Equals(tag.FriendlyName, filters.FriendlyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(filters.CanonicalName) &&
                !string.Equals(tag.CanonicalName, filters.CanonicalName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (filters.IsAnnotated.HasValue && tag.IsAnnotated != filters.IsAnnotated.Value)
                continue;

            var entity = new TagEntity(tag, repository);
            chunk.Add(new EntityResolver<TagEntity>(
                entity,
                TagEntity.NameToIndexMap,
                TagEntity.IndexToObjectAccessMap
            ));

            if (chunk.Count >= 100)
            {
                chunkedSource.Add(chunk.ToArray(), cancellationToken);
                chunk.Clear();
            }
        }

        if (chunk.Count > 0) chunkedSource.Add(chunk.ToArray(), cancellationToken);

        return Task.CompletedTask;
    }
}