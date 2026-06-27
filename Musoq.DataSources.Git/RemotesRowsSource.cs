using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git;

internal sealed class RemotesRowsSource(
    string repositoryPath,
    Func<string, Repository> createRepository,
    CancellationToken cancellationToken) : AsyncRowsSourceBase<RemoteEntity>(cancellationToken)
{
    protected override Task CollectChunksAsync(IChunkWriter<RemoteEntity> writer, CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<RemoteEntity>(100);

        foreach (var remote in repository.Network.Remotes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            chunk.Add(new RemoteEntity(remote));

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
