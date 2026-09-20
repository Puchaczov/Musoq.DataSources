#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Sources;

/// <summary>
///     Emits labeled literal or portable-regex occurrences from one bounded
///     many-pattern request. Per-file summaries are intentionally composed
///     with ordinary SQL aggregation over the typed occurrence rows.
/// </summary>
internal sealed class SearchManySource : RowSourceBase<SearchMatch>
{
    private const string SearchSourceName = "search";

    private readonly SearchManyRequest _request;
    private readonly SourceExecutionContext _executionContext;
    private readonly Func<string, TextReader>? _readerFactory;
    private readonly SearchScopeCounters? _scopeCounters;
    private readonly SearchFileParallelOptions _parallelOptions;
    private readonly SearchManyMixedPlan _compiledPlan;

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
        _compiledPlan = SearchManyMixedScan.Compile(request);
        _parallelOptions = readerFactory is null
            ? SearchFileParallelOptions.FromRuntimeSettings(executionContext.SourceRuntimeSettings)
            : SearchFileParallelOptions.Sequential;
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
        var resourceBudget = new SearchResourceBudget(_request.Options.Limits);
        var outputBudget = resourceBudget.OutputBudget;
        var projection = SearchProjectionRequirements.From(_executionContext);
        var acceptedPredicate = _executionContext.Plan.AcceptedPredicate;
        var acceptedWindow = SearchSliceWindow.Create(
            _executionContext.Plan.AcceptedSkip,
            _executionContext.Plan.AcceptedTake);
        var readerFactory = _readerFactory ??
                            (path => SearchTextReader.Open(path, _request.Options.EncodingMode));
        var classifyBinaryFiles = _readerFactory is null;
        var workerBuffers = new ThreadLocal<SearchCharBuffer>(
            SearchCharBuffer.Rent,
            trackAllValues: true);

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

            void ProcessFile(
                string file,
                IChunkWriter<SearchMatch> fileWriter,
                CancellationToken fileCancellationToken)
            {
                var buffer = workerBuffers.Value ?? throw new InvalidOperationException(
                    "The Search many worker did not acquire its input buffer.");
                var rows = new List<SearchMatch>();
                var matchIndexes = new Dictionary<string, long>(StringComparer.Ordinal);
                var stagedOutputBytes = 0L;
                var sourceObservation = SearchSourceObservation.Capture(
                    file,
                    fileCancellationToken);
                SearchTestHooks.AfterObservation(file);
                var fileBudget = resourceBudget.BeginFile(file);
                fileBudget.ReserveBytes(sourceObservation.Length);
                using var smallInput = classifyBinaryFiles &&
                                       sourceObservation.Length <= 4L * 1024 * 1024
                    ? SearchSmallFileBuffer.Read(
                        file,
                        sourceObservation.Length,
                        fileCancellationToken)
                    : null;

                void FlushRows()
                {
                    if (rows.Count == 0)
                        return;

                    fileCancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        fileWriter.Write(rows.ToArray());
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is SearchOutputException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new SearchOutputException(
                            SearchDiagnosticCatalog.OutputFailed(),
                            exception);
                    }

                    rows.Clear();
                    outputBudget?.Release(stagedOutputBytes);
                    stagedOutputBytes = 0;
                }

                try
                {
                    if (classifyBinaryFiles)
                    {
                        _scopeCounters?.IncrementContentOpenAttempts();
                        var isBinary = smallInput is not null
                            ? SearchBinaryPolicy.IsBinaryBytes(
                                smallInput.Array,
                                smallInput.Length,
                                _request.Options.EncodingMode,
                                buffer,
                                fileCancellationToken)
                            : SearchBinaryPolicy.IsBinaryFile(
                                file,
                                _request.Options.EncodingMode,
                                buffer,
                                fileCancellationToken);
                        if (isBinary)
                        {
                            _scopeCounters?.IncrementBinaryFilesSkipped();
                            sourceObservation.EnsureCurrent(fileCancellationToken);
                            return;
                        }
                    }

                    if (smallInput is null)
                        _scopeCounters?.IncrementContentOpenAttempts();
                    var scanReaderFactory = readerFactory;
                    if (smallInput is not null)
                    {
                        scanReaderFactory = _ => SearchTextReader.Open(
                            smallInput.Array,
                            smallInput.Length,
                            _request.Options.EncodingMode);
                    }
                    var relativeRoot = File.Exists(rootPath)
                        ? Path.GetDirectoryName(rootPath) ?? rootPath
                        : rootPath;
                    var relativePath = SearchTextPath.GetRelativePath(relativeRoot, file);
                    SearchManyMixedScan.ScanFile(
                        file,
                        _compiledPlan,
                        fileCancellationToken,
                        spans =>
                        {
                            fileCancellationToken.ThrowIfCancellationRequested();
                            for (var index = 0; index < spans.Count; index++)
                            {
                                if ((index & 255) == 0)
                                    fileCancellationToken.ThrowIfCancellationRequested();

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
                                    projection.RetainCaptures ? span.Captures : null,
                                    patternId: patternId,
                                    context: projection.RetainContext
                                        ? span.Context
                                        : null);

                                if (acceptedPredicate is not null &&
                                    !SearchPredicateEvaluator.Matches(
                                        acceptedPredicate,
                                        row,
                                        SearchPredicateValues.GetMatchValue))
                                {
                                    continue;
                                }

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
                        projection.RetainMatchText,
                        projection.RetainCaptures,
                        scanReaderFactory,
                        buffer,
                        _request.Options.Context,
                        projection.RetainContext);

                    sourceObservation.EnsureCurrent(fileCancellationToken);
                    fileCancellationToken.ThrowIfCancellationRequested();
                    FlushRows();
                }
                finally
                {
                    rows.Clear();
                    outputBudget?.Release(stagedOutputBytes);
                }
            }

            SearchFileParallelCoordinator.Run(
                token => SearchScopeTraversal.Enumerate(
                    rootPath,
                    _request.Options.Scope,
                    token,
                    _scopeCounters),
                writer,
                ProcessFile,
                acceptedWindow,
                rows =>
                {
                    progress.RowsRead(rows);
                    totalRowsProcessed = checked(totalRowsProcessed + rows);
                },
                cancellationToken,
                _parallelOptions,
                estimateChunkBytes: SearchRowSizeEstimator.EstimateChunk<SearchMatch>);
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
            workerBuffers.Dispose();
            linkedCancellation?.Dispose();
            progress.End(totalRowsProcessed);
        }
    }
}
