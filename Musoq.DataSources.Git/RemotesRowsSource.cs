using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class RemotesRowsSource : GitDiagnosticRowsSourceBase<RemoteEntity>
{
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly Func<string, Repository> _createRepository;
    private readonly GitFilterParameters _filters;
    private readonly GitProjection _projection;
    private readonly string _repositoryPath;

    public RemotesRowsSource(string repositoryPath, Func<string, Repository> createRepository, SourceExecutionContext executionContext)
        : base(executionContext, "git.remotes")
    {
        _repositoryPath = repositoryPath;
        _createRepository = createRepository;
        _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
        _filters = GitSourcePlanner.GetFilters(executionContext.Plan);
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
    }

    protected override long CollectRows(DiagnosticChunkWriter<RemoteEntity> writer, CancellationToken cancellationToken)
    {
        var chunk = new List<RemoteEntity>(128);
        long rowsRead = 0;
        var reader = GitOperationReaders.Remotes;

        reader.Read(_repositoryPath, _projection, _createRepository, cancellationToken, remote =>
        {
            if (!GitSourcePlanner.Matches(_filters, remote))
                return true;
            var entity = GitEntitySnapshots.Remote(remote, _projection);
            if (!GitSourcePlanner.Matches(_acceptedPredicate, entity))
                return true;

            chunk.Add(entity);
            if (chunk.Count == 128)
                rowsRead += WriteChunk(writer, chunk, rowsRead);
            return true;
        });

        rowsRead += WriteChunk(writer, chunk, rowsRead);
        Context.Diagnostics.AddMetric("Git.Remotes.Backend", reader.Backend == "git-cli" ? 1 : 2);
        return rowsRead;
    }
}
