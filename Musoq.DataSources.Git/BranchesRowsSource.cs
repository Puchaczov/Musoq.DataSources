using System;
using System.Collections.Generic;
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
    protected override Task CollectChunksAsync(IChunkWriter<BranchEntity> writer, CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        var chunk = new List<BranchEntity>(100);

        foreach (var branch in repository.Branches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            chunk.Add(new BranchEntity(branch, repository));

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
