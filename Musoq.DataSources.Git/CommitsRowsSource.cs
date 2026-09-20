using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class CommitsRowsSource : GitDiagnosticRowsSourceBase<CommitEntity>
{
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly Func<string, Repository> _createRepository;
    private readonly GitFilterParameters _filters;
    private readonly GitProjection _projection;
    private readonly string _repositoryPath;

    public CommitsRowsSource(string repositoryPath, Func<string, Repository> createRepository, SourceExecutionContext executionContext)
        : base(executionContext, "git.commits")
    {
        _repositoryPath = repositoryPath;
        _createRepository = createRepository;
        _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
        _filters = GitSourcePlanner.GetFilters(executionContext.Plan);
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
    }

    protected override long CollectRows(DiagnosticChunkWriter<CommitEntity> writer, CancellationToken cancellationToken)
    {
        if (Context.Plan.AcceptedTake == 0)
            return 0;

        var chunk = new List<CommitEntity>(128);
        long rowsRead = 0;
        long skipped = 0;
        var reader = GitOperationReaders.Commits;

        reader.Read(
            _repositoryPath,
            _projection,
            _filters.Sha,
            _createRepository,
            cancellationToken,
            commit =>
            {
                if (!GitSourcePlanner.Matches(_filters, commit))
                    return true;
                var entity = GitEntitySnapshots.Commit(commit, _projection);
                if (!GitSourcePlanner.Matches(_acceptedPredicate, entity))
                    return true;

                if (Context.Plan.AcceptedSkip.HasValue && skipped < Context.Plan.AcceptedSkip.Value)
                {
                    skipped++;
                    return true;
                }

                if (Context.Plan.AcceptedTake.HasValue && rowsRead + chunk.Count >= Context.Plan.AcceptedTake.Value)
                    return false;

                chunk.Add(entity);
                if (chunk.Count == 128)
                    rowsRead += WriteChunk(writer, chunk, rowsRead);
                return true;
            });

        rowsRead += WriteChunk(writer, chunk, rowsRead);
        Context.Diagnostics.AddMetric("Git.Commits.Backend", reader.Backend == "git-cli" ? 1 : 2);
        Context.Diagnostics.AddMetric("Git.Commits.DirectSha", string.IsNullOrWhiteSpace(_filters.Sha) ? 0 : 1);
        return rowsRead;
    }
}
