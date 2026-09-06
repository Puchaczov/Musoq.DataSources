#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Sources;

/// <summary>
///     Emits labeled literal occurrences from one bounded many-pattern
///     request. Per-file summaries are intentionally composed with ordinary
///     SQL aggregation over the typed occurrence rows.
/// </summary>
internal sealed class SearchManySource : RowSourceBase<SearchMatch>
{
    private const string SearchSourceName = "search";

    private readonly SearchManyRequest _request;
    private readonly SourceExecutionContext _executionContext;
    private readonly Func<string, TextReader>? _readerFactory;
    private readonly SearchScopeCounters? _scopeCounters;

    public SearchManySource(
        string root,
        string requestJson,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null)
        : this(
            root,
            SearchManyRequestParser.Parse(requestJson),
            executionContext,
            readerFactory,
            scopeCounters: null)
    {
    }

    internal SearchManySource(
        string root,
        SearchManyRequest request,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null,
        SearchScopeCounters? scopeCounters = null)
    {
        if (string.IsNullOrEmpty(root))
            throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("root"));

        _request = request ?? throw new ArgumentNullException(nameof(request));
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _readerFactory = readerFactory;
        _scopeCounters = scopeCounters;
        Root = root;
    }

    private string Root { get; }

    protected override void CollectChunks(IChunkWriter<SearchMatch> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, SearchSourceName);
        progress.Begin();
        var totalRowsProcessed = 0L;
        CancellationTokenSource? linkedCancellation = null;
        var cancellationToken = writer.CancellationToken;
        var rows = new List<SearchMatch>();
        var resourceBudget = new SearchResourceBudget(_request.Options.Limits);
        var outputBudget = resourceBudget.OutputBudget;
        var stagedOutputBytes = 0L;

        try
        {
            if (_executionContext.EndWorkToken.CanBeCanceled &&
                !_executionContext.EndWorkToken.Equals(writer.CancellationToken))
            {
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    writer.CancellationToken,
                    _executionContext.EndWorkToken);
                cancellationToken = linkedCancellation.Token;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string rootPath;
            try
            {
                rootPath = Path.GetFullPath(Root);
            }
            catch (ArgumentException exception)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument("root"),
                    exception);
            }

            var currentPath = string.Empty;
            var matchIndexes = new Dictionary<string, long>(StringComparer.Ordinal);
            var projection = SearchProjectionRequirements.From(_executionContext);
            var acceptedPredicate = _executionContext.Plan.AcceptedPredicate;

            void FlushRows()
            {
                if (rows.Count == 0)
                    return;

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    writer.Write(rows.ToArray());
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new SearchOutputException(
                        SearchDiagnosticCatalog.OutputFailed(),
                        exception);
                }

                var written = rows.Count;
                rows.Clear();
                outputBudget?.Release(stagedOutputBytes);
                stagedOutputBytes = 0;
                totalRowsProcessed = checked(totalRowsProcessed + written);
                progress.RowsRead(written);
            }

            SearchManyLiteralScan.ScanScopeBatched(
                rootPath,
                _request,
                cancellationToken,
                (relativePath, spans) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.Equals(currentPath, relativePath, StringComparison.Ordinal))
                    {
                        currentPath = relativePath;
                        matchIndexes.Clear();
                    }

                    for (var index = 0; index < spans.Count; index++)
                    {
                        if ((index & 255) == 0)
                            cancellationToken.ThrowIfCancellationRequested();
                        var span = spans[index];
                        resourceBudget.ObserveMatch();
                        var patternId = span.PatternId ??
                            throw new SearchSourceReadException(
                                SearchDiagnosticCatalog.SourceReadFailed(relativePath),
                                new InvalidOperationException(
                                    "The many matcher emitted a span without a pattern identifier."));
                        matchIndexes.TryGetValue(patternId, out var matchIndex);
                        matchIndexes[patternId] = checked(matchIndex + 1);

                        var row = new SearchMatch(
                            relativePath,
                            matchIndex,
                            span.ByteOffset,
                            span.ByteLength,
                            span.LineNumber,
                            span.Utf16Column,
                            span.Length,
                            projection.RetainMatchText ? span.MatchText : null,
                            patternId: patternId);

                        if (acceptedPredicate is not null &&
                            !SearchPredicateEvaluator.Matches(
                                acceptedPredicate,
                                row,
                                SearchPredicateValues.GetMatchValue))
                            continue;

                        var rowBytes = outputBudget is null
                            ? 0
                            : SearchRowSizeEstimator.Estimate(row);
                        outputBudget?.Reserve(rowBytes);
                        stagedOutputBytes = checked(stagedOutputBytes + rowBytes);
                        rows.Add(row);

                        if (rows.Count >= RowChunking.DefaultChunkSize)
                            FlushRows();
                    }
                },
                _readerFactory,
                _scopeCounters,
                resourceBudget);

            FlushRows();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(Root),
                exception);
        }
        finally
        {
            rows.Clear();
            outputBudget?.Release(stagedOutputBytes);
            stagedOutputBytes = 0;
            linkedCancellation?.Dispose();
            progress.End(totalRowsProcessed);
        }
    }
}
