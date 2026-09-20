using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class StashesRowsSource : GitDiagnosticRowsSourceBase<StashEntity>
{
    private const int ChunkSize = 128;
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly Func<string, Repository> _createRepository;
    private readonly GitFilterParameters _filters;
    private readonly GitProjection _projection;
    private readonly GitReferenceBackendOptions _options;
    private readonly string _repositoryPath;

    public StashesRowsSource(
        string repositoryPath,
        Func<string, Repository> createRepository,
        SourceExecutionContext executionContext)
        : base(executionContext, "git.stashes")
    {
        _repositoryPath = repositoryPath;
        _createRepository = createRepository;
        _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
        _filters = GitSourcePlanner.GetFilters(executionContext.Plan);
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
        _options = GitReferenceBackendOptions.From(executionContext.SourceRuntimeSettings);
    }

    protected override long CollectRows(DiagnosticChunkWriter<StashEntity> writer, CancellationToken cancellationToken)
    {
        if (Context.Plan.AcceptedTake == 0)
            return 0;

        var chunk = new List<StashEntity>(ChunkSize);
        long rowsRead = 0;
        long skipped = 0;
        long rowsExamined = 0;
        long rowsFiltered = 0;
        var earlyStopped = false;
        var maximumBufferedRows = 0;
        var reader = _options.Backend == GitHistoryBackend.LibGit2
            ? GitOperationReaders.Stashes
            : GitOperationReaders.CliStashes;
        var query = new GitStashReadQuery(
            _filters.Selector,
            _filters.Sha,
            GitSourcePlanner.OrderedExactValues(_filters.Selectors),
            GitSourcePlanner.OrderedExactValues(_filters.Shas));

        void ReadWith(IGitStashReader selectedReader)
        {
            selectedReader.Read(_repositoryPath, _options, _projection, query, _createRepository, cancellationToken, stash =>
            {
                rowsExamined++;
                if (!GitSourcePlanner.Matches(_filters, stash))
                {
                    rowsFiltered++;
                    return true;
                }

                var entity = GitEntitySnapshots.Stash(stash, _projection);
                if (!GitSourcePlanner.Matches(_acceptedPredicate, entity))
                {
                    rowsFiltered++;
                    return true;
                }

                if (Context.Plan.AcceptedSkip.HasValue && skipped < Context.Plan.AcceptedSkip.Value)
                {
                    skipped++;
                    return true;
                }

                if (Context.Plan.AcceptedTake.HasValue && rowsRead + chunk.Count >= Context.Plan.AcceptedTake.Value)
                {
                    earlyStopped = true;
                    return false;
                }

                chunk.Add(entity);
                maximumBufferedRows = Math.Max(maximumBufferedRows, chunk.Count);
                if (chunk.Count == ChunkSize)
                    rowsRead += WriteChunk(writer, chunk, rowsRead);
                return true;
            });
        }

        try
        {
            ReadWith(reader);
        }
        catch (GitCliUnavailableException) when (_options.Backend == GitHistoryBackend.Auto)
        {
            reader = GitOperationReaders.Stashes;
            ReadWith(reader);
        }

        rowsRead += WriteChunk(writer, chunk, rowsRead);
        Context.Diagnostics.AddMetric("Git.Stashes.Backend", reader.Backend == "git-cli" ? 1 : 2);
        Context.Diagnostics.AddMetric("Git.Stashes.RowsExamined", rowsExamined);
        Context.Diagnostics.AddMetric("Git.Stashes.RowsFiltered", rowsFiltered);
        Context.Diagnostics.AddMetric("Git.Stashes.RowsSkipped", skipped);
        Context.Diagnostics.AddMetric("Git.Stashes.RowsEmitted", rowsRead);
        Context.Diagnostics.AddMetric("Git.Stashes.EarlyStop", earlyStopped ? 1 : 0);
        // Selector/SHA filtering is still a streaming reflog scan until Wave 10 supplies a genuine direct
        // selector operation. Do not advertise pushdown merely because a filter was accepted by the planner.
        Context.Diagnostics.AddMetric("Git.Stashes.DirectLookup",
            query.IsDirectSelectorLookup && reader.Backend == "git-cli" ? 1 : 0);
        Context.Diagnostics.AddMetric("Git.Stashes.ProjectionMode", !_projection.IsAccepted ? 0 : _projection.Columns.Count == 0 ? 1 : 2);
        Context.Diagnostics.AddMetric("Git.Stashes.MaximumBufferedRows", maximumBufferedRows);
        return rowsRead;
    }
}
