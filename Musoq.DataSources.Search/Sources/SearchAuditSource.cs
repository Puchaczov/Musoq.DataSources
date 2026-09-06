#nullable enable

using System;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Sources;

internal sealed class SearchAuditSource : RowSourceBase<SearchAudit>
{
    private const string SearchSourceName = "search";

    private readonly SearchRequest _request;
    private readonly SourceExecutionContext _executionContext;
    private readonly Func<string, TextReader>? _readerFactory;

    internal SearchAuditSource(
        SearchRequest request,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _readerFactory = readerFactory;
    }

    internal SearchTerminalSummary? LastExecution { get; private set; }

    protected override void CollectChunks(IChunkWriter<SearchAudit> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, SearchSourceName);
        progress.Begin();
        var totalRowsProcessed = 0L;
        CancellationTokenSource? linkedCancellation = null;
        var cancellationToken = writer.CancellationToken;

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

            var scanContext = new SourceExecutionContext(
                _executionContext.QueryId,
                SourceExecutionPlan.Empty(_executionContext.Plan.Identity),
                cancellationToken,
                _executionContext.AllColumns,
                _executionContext.SourceRuntimeSettings,
                _executionContext.Logger,
                sourceDiagnostics: _executionContext.Diagnostics);
            var counts = new SearchCountsSource(
                _request,
                scanContext,
                _readerFactory);
            Exception? scanFailure = null;

            try
            {
                foreach (var _ in counts.Chunks)
                    cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !writer.CancellationToken.IsCancellationRequested)
            {
                scanFailure = exception;
            }

            var summary = counts.LastExecution ??
                throw new InvalidOperationException(
                    "The Search count scan completed without publishing terminal accounting.");
            LastExecution = summary;

            if (scanFailure is OperationCanceledException &&
                writer.CancellationToken.IsCancellationRequested)
            {
                throw scanFailure;
            }

            // Audit is the explicit terminal-reporting surface: a typed inner
            // scan failure becomes one failed summary row. Writer cancellation
            // remains exceptional so abandoned output is never reported as a
            // completed audit.
            var row = new SearchAudit(_request.Root, summary);
            var acceptedWindow = SearchSliceWindow.Create(
                _executionContext.Plan.AcceptedSkip,
                _executionContext.Plan.AcceptedTake);
            if (_executionContext.Plan.AcceptedPredicate is not null &&
                !SearchPredicateEvaluator.Matches(
                    _executionContext.Plan.AcceptedPredicate,
                    row,
                    SearchPredicateValues.GetAuditValue))
            {
                return;
            }

            if (acceptedWindow is not null && !acceptedWindow.TryAccept())
                return;

            cancellationToken = writer.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                writer.Write([row]);
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

            totalRowsProcessed = 1;
            progress.RowsRead(1);
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
                SearchDiagnosticCatalog.SourceOpenFailed(_request.Root),
                exception);
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(totalRowsProcessed);
        }
    }
}
