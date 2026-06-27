using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class TagsRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<TagEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(IChunkWriter<TagEntity> writer, CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<TagEntity>(100);

        foreach (var tag in repository.Tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            chunk.Add(new TagEntity(tag, repository));

            if (chunk.Count < 100)
                continue;

            writer.Write(chunk);
            chunk = [];
        }

        if (chunk.Count > 0)
            writer.Write(chunk);

        return Task.CompletedTask;
    }
}
