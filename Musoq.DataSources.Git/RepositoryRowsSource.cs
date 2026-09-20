using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class RepositoryRowsSource : GitDiagnosticRowsSourceBase<RepositoryEntity>
{
    private readonly Func<string, Repository> _createRepository;
    private readonly string _repositoryPath;
    private readonly GitProjection _projection;
    private readonly GitReferenceBackendOptions _options;

    public RepositoryRowsSource(string repositoryPath, Func<string, Repository> createRepository, SourceExecutionContext executionContext)
        : base(executionContext, "git.repository")
    {
        _repositoryPath = repositoryPath;
        _createRepository = createRepository;
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
        _options = GitReferenceBackendOptions.From(executionContext.SourceRuntimeSettings);
    }

    protected override long CollectRows(DiagnosticChunkWriter<RepositoryEntity> writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var repository = _createRepository(_repositoryPath);
        var rows = new List<RepositoryEntity>
        {
            GitEntitySnapshots.Repository(repository, _projection, _options)
        };

        return WriteChunk(writer, rows, rowsReadBeforeWrite: 0);
    }
}
