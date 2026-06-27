using System;
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
    protected override Task CollectChunksAsync(
        IChunkWriter<RepositoryEntity> writer,
        CancellationToken cancellationToken)
    {
        var repository = createRepository(repositoryPath);
        writer.Write([new RepositoryEntity(repository)]);
        return Task.CompletedTask;
    }
}
