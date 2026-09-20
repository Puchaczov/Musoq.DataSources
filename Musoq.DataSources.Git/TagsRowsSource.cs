using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

internal sealed class TagsRowsSource : GitDiagnosticRowsSourceBase<TagEntity>
{
    private readonly SourcePredicateExpression? _acceptedPredicate;
    private readonly Func<string, Repository> _createRepository;
    private readonly GitFilterParameters _filters;
    private readonly GitProjection _projection;
    private readonly GitProjection _readerProjection;
    private readonly GitReferenceBackendOptions _options;
    private readonly string _repositoryPath;

    public TagsRowsSource(string repositoryPath, Func<string, Repository> createRepository, SourceExecutionContext executionContext)
        : base(executionContext, "git.tags")
    {
        _repositoryPath = repositoryPath;
        _createRepository = createRepository;
        _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
        _filters = GitSourcePlanner.GetFilters(executionContext.Plan);
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
        _readerProjection = _projection;
        _options = GitReferenceBackendOptions.From(executionContext.SourceRuntimeSettings);
    }

    protected override long CollectRows(DiagnosticChunkWriter<TagEntity> writer, CancellationToken cancellationToken)
    {
        if (Context.Plan.AcceptedTake == 0)
            return 0;

        var chunk = new List<TagEntity>(128);
        long rowsRead = 0;
        long skipped = 0;
        long rowsExamined = 0;
        long rowsFiltered = 0;
        long rowsSkippedByCursor = 0;
        var earlyStopped = false;
        var maximumBufferedRows = 0;
        var reader = _options.Backend == GitHistoryBackend.LibGit2
            ? GitOperationReaders.Tags
            : GitOperationReaders.CliTags;
        var query = new GitTagReadQuery(
            _filters.CanonicalName,
            _filters.FriendlyName,
            GitSourcePlanner.OrderedExactValues(_filters.CanonicalNames),
            GitSourcePlanner.OrderedExactValues(_filters.FriendlyNames),
            _filters.CanonicalNameAfter,
            _filters.CanonicalNameAfterInclusive,
            () => rowsSkippedByCursor++);

        bool ReadWith(IGitTagReader selectedReader)
        {
            selectedReader.Read(_repositoryPath, _options, _readerProjection, query, _createRepository, cancellationToken, tag =>
            {
                rowsExamined++;
                if (!GitSourcePlanner.Matches(_filters, tag))
                {
                    rowsFiltered++;
                    return true;
                }
                var entity = GitEntitySnapshots.Tag(
                    tag,
                    _projection,
                    () => TagEntity.LoadRichSnapshot(_repositoryPath, tag.CanonicalName, tag.FriendlyName, includeMessage: false));
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
                if (chunk.Count == 128)
                    rowsRead += WriteChunk(writer, chunk, rowsRead);
                return true;
            });
            return true;
        }

        try
        {
            ReadWith(reader);
        }
        catch (GitCliUnavailableException) when (_options.Backend == GitHistoryBackend.Auto)
        {
            reader = GitOperationReaders.Tags;
            ReadWith(reader);
        }

        rowsRead += WriteChunk(writer, chunk, rowsRead);
        Context.Diagnostics.AddMetric("Git.Tags.Backend", reader.Backend == "git-cli" ? 1 : 2);
        Context.Diagnostics.AddMetric("Git.Tags.RowsExamined", rowsExamined);
        Context.Diagnostics.AddMetric("Git.Tags.RowsEmitted", rowsRead);
        Context.Diagnostics.AddMetric("Git.Tags.RowsFiltered", rowsFiltered);
        Context.Diagnostics.AddMetric("Git.Tags.RowsSkipped", skipped);
        Context.Diagnostics.AddMetric("Git.Tags.RowsSkippedByCursor", rowsSkippedByCursor);
        Context.Diagnostics.AddMetric("Git.Tags.EarlyStop", earlyStopped ? 1 : 0);
        Context.Diagnostics.AddMetric("Git.Tags.DirectLookup", query.ExactCanonicalName is not null ||
                                                               query.ExactFriendlyName is not null ||
                                                               query.ExactCanonicalNames is { Count: > 0 } ||
                                                               query.ExactFriendlyNames is { Count: > 0 } ? 1 : 0);
        Context.Diagnostics.AddMetric("Git.Tags.ProjectionMode", !_projection.IsAccepted ? 0 : _projection.Columns.Count == 0 ? 1 : 2);
        Context.Diagnostics.AddMetric("Git.Tags.MaximumBufferedRows", maximumBufferedRows);
        return rowsRead;
    }
}
