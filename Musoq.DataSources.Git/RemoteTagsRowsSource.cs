using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class RemoteTagsRowsSource : GitDiagnosticRowsSourceBase<RemoteTagEntity>
{
    private const int ChunkSize = 128;
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly Func<string, Repository> _createRepository;
    private readonly GitFilterParameters _filters;
    private readonly GitProjection _projection;
    private readonly GitReferenceBackendOptions _options;
    private readonly string _remoteName;
    private readonly string _repositoryPath;

    public RemoteTagsRowsSource(
        string repositoryPath,
        string remoteName,
        Func<string, Repository> createRepository,
        SourceExecutionContext executionContext)
        : base(executionContext, "git.remotetags")
    {
        if (string.IsNullOrWhiteSpace(remoteName))
            throw new ArgumentException("A remote name is required.", nameof(remoteName));

        _repositoryPath = repositoryPath;
        _remoteName = remoteName;
        _createRepository = createRepository;
        _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
        _filters = GitSourcePlanner.GetFilters(executionContext.Plan);
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
        _options = GitReferenceBackendOptions.From(executionContext.SourceRuntimeSettings);

        if (_options.Backend == GitHistoryBackend.LibGit2)
            throw new InvalidOperationException(
                "Remote tags require the git-cli reference backend; libgit2 does not provide live remote advertisement support.");
    }

    protected override long CollectRows(DiagnosticChunkWriter<RemoteTagEntity> writer, CancellationToken cancellationToken)
    {
        if (Context.Plan.AcceptedTake == 0)
            return 0;

        var chunk = new List<RemoteTagEntity>(ChunkSize);
        long rowsRead = 0;
        long skipped = 0;
        long rowsExamined = 0;
        long rowsFiltered = 0;
        long rowsSkippedByCursor = 0;
        var earlyStopped = false;
        var maximumBufferedRows = 0;
        var query = new GitRemoteTagReadQuery(
            _filters.CanonicalName,
            _filters.FriendlyName,
            GitSourcePlanner.OrderedExactValues(_filters.CanonicalNames),
            GitSourcePlanner.OrderedExactValues(_filters.FriendlyNames),
            _filters.CanonicalNameAfter,
            _filters.CanonicalNameAfterInclusive,
            () => rowsSkippedByCursor++);
        var reader = GitOperationReaders.RemoteTags;

        reader.Read(
            _repositoryPath,
            _remoteName,
            _options,
            _projection,
            query,
            _createRepository,
            cancellationToken,
            tag =>
            {
                rowsExamined++;
                if (!GitSourcePlanner.Matches(_filters, tag))
                {
                    rowsFiltered++;
                    return true;
                }

                var entity = GitEntitySnapshots.RemoteTag(tag, _projection);
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

        rowsRead += WriteChunk(writer, chunk, rowsRead);
        Context.Diagnostics.AddMetric("Git.RemoteTags.Backend", 1);
        Context.Diagnostics.AddMetric("Git.RemoteTags.RowsExamined", rowsExamined);
        Context.Diagnostics.AddMetric("Git.RemoteTags.RowsEmitted", rowsRead);
        Context.Diagnostics.AddMetric("Git.RemoteTags.RowsFiltered", rowsFiltered);
        Context.Diagnostics.AddMetric("Git.RemoteTags.RowsSkipped", skipped);
        Context.Diagnostics.AddMetric("Git.RemoteTags.RowsSkippedByCursor", rowsSkippedByCursor);
        Context.Diagnostics.AddMetric("Git.RemoteTags.EarlyStop", earlyStopped ? 1 : 0);
        Context.Diagnostics.AddMetric("Git.RemoteTags.DirectLookup", query.ExactCanonicalName is not null ||
                                                                        query.ExactFriendlyName is not null ||
                                                                        query.ExactCanonicalNames is { Count: > 0 } ||
                                                                        query.ExactFriendlyNames is { Count: > 0 } ? 1 : 0);
        Context.Diagnostics.AddMetric("Git.RemoteTags.ProjectionMode", !_projection.IsAccepted ? 0 : _projection.Columns.Count == 0 ? 1 : 2);
        Context.Diagnostics.AddMetric("Git.RemoteTags.MaximumBufferedRows", maximumBufferedRows);
        return rowsRead;
    }
}
