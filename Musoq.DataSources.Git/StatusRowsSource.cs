using System;
using System.Collections.Generic;
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
    protected override Task CollectChunksAsync(IChunkWriter<StatusEntity> writer, CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var status = repository.RetrieveStatus();
        var chunk = new List<StatusEntity>(100);

        foreach (var entry in status)
        {
            cancellationToken.ThrowIfCancellationRequested();

            chunk.Add(new StatusEntity(entry));

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
