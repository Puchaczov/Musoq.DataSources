using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class RemotesRowsSource : AsyncRowsSourceBase<RemoteEntity>
{
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly Func<string, Repository> _createRepository;
    private readonly GitFilterParameters _filters;
    private readonly string _repositoryPath;

    public RemotesRowsSource(
        string repositoryPath,
        Func<string, Repository> createRepository,
        SourceExecutionContext executionContext)
        : base(executionContext.EndWorkToken)
    {
        _repositoryPath = repositoryPath;
        _createRepository = createRepository;
        _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
        _filters = GitSourcePlanner.GetFilters(executionContext.Plan);
    }

    protected override Task CollectChunksAsync(IChunkWriter<RemoteEntity> writer, CancellationToken cancellationToken)
    {
        var repository = _createRepository(_repositoryPath);
        var chunk = new List<RemoteEntity>(100);

        foreach (var remote in repository.Network.Remotes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!GitSourcePlanner.Matches(_filters, remote))
                continue;

            var entity = new RemoteEntity(remote);

            if (!GitSourcePlanner.Matches(_acceptedPredicate, entity))
                continue;

            chunk.Add(entity);

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
