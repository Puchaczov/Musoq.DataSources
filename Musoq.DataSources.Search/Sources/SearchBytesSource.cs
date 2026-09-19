#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Sources;

/// <summary>
///     Streams raw bytes from the resolved Search scope and emits one row per
///     selected non-overlapping byte-pattern occurrence.
/// </summary>
internal sealed class SearchBytesSource : RowSourceBase<SearchByteMatch>
{
    private const string SearchSourceName = "search";

    private readonly string _root;
    private readonly SearchBytePattern _pattern;
    private readonly SourceExecutionContext _executionContext;
    private readonly ScopePolicy _scope;
    private readonly SearchResourceLimits _limits;
    private readonly SearchScopeCounters? _scopeCounters;
    private readonly SearchFileParallelOptions _parallelOptions;

    internal SearchBytesSource(
        string root,
        SearchBytePattern pattern,
        SourceExecutionContext executionContext,
        SearchScopeCounters? scopeCounters = null,
        SearchFileParallelOptions? parallelOptions = null)
        : this(
            root,
            pattern,
            executionContext,
            ScopePolicy.Default,
            SearchResourceLimits.Default,
            scopeCounters,
            parallelOptions)
    {
    }

    internal SearchBytesSource(
        string root,
        SearchBytePattern pattern,
        SourceExecutionContext executionContext,
        ScopePolicy scope,
        SearchResourceLimits limits,
        SearchScopeCounters? scopeCounters = null,
        SearchFileParallelOptions? parallelOptions = null)
    {
        _root = RequireRoot(root);
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _scopeCounters = scopeCounters;
        _parallelOptions = parallelOptions ??
            SearchFileParallelOptions.FromRuntimeSettings(executionContext.SourceRuntimeSettings);
    }

    internal SearchTerminalSummary? LastExecution { get; private set; }

    protected override void CollectChunks(IChunkWriter<SearchByteMatch> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, SearchSourceName);
        progress.Begin();
        var totalRowsProcessed = 0L;
        CancellationTokenSource? linkedCancellation = null;
        var cancellationToken = writer.CancellationToken;
        var scopeCounters = _scopeCounters ?? new SearchScopeCounters();
        var resourceBudget = new SearchResourceBudget(_limits);
        var accounting = new SearchExecutionAccounting(
            Guid.NewGuid().ToString("N"),
            SearchScopeFingerprint.Create(_root, _scope));
        Exception? terminalException = null;

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
                rootPath = Path.GetFullPath(_root);
            }
            catch (ArgumentException exception)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument("root"),
                    exception);
            }

            accounting.SetScopeResolved(
                File.Exists(rootPath) || Directory.Exists(rootPath));
            var relativeRoot = File.Exists(rootPath)
                ? Path.GetDirectoryName(rootPath) ?? rootPath
                : rootPath;
            var projection = SearchProjectionRequirements.From(_executionContext);
            var acceptedPredicate = _executionContext.Plan.AcceptedPredicate;
            Func<SearchByteMatch, bool>? acceptedRow = acceptedPredicate is null
                ? null
                : row => SearchPredicateEvaluator.Matches(
                    acceptedPredicate,
                    row,
                    SearchPredicateValues.GetByteValue);
            var acceptedWindow = SearchSliceWindow.Create(
                _executionContext.Plan.AcceptedSkip,
                _executionContext.Plan.AcceptedTake);

            void ProcessFile(
                string file,
                IChunkWriter<SearchByteMatch> fileWriter,
                CancellationToken fileCancellationToken)
            {
                SearchOutputStagingBudget? outputBudget = resourceBudget.OutputBudget;
                var stagedOutputBytes = 0L;
                List<SearchByteMatch>? rows = null;

                void FlushRows()
                {
                    if (rows is null || rows.Count == 0)
                        return;

                    fileCancellationToken.ThrowIfCancellationRequested();
                    var chunk = rows.ToArray();
                    try
                    {
                        fileWriter.Write(chunk);
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
                    var sourceObservation = SearchSourceObservation.Capture(
                        file,
                        fileCancellationToken);
                    SearchTestHooks.AfterObservation(file);
                    var fileBudget = resourceBudget.BeginFile(file);
                    fileBudget.ReserveBytes(sourceObservation.Length);
                    scopeCounters.IncrementContentOpenAttempts();
                    accounting.RecordFileOpened();
                    var relativePath = SearchTextPath.GetRelativePath(relativeRoot, file);
                    var matchIndex = 0L;
                    var occurrencesObserved = 0L;

                    SearchByteScanner.ScanFile(
                        file,
                        _pattern,
                        projection.RetainMatchedBytes,
                        projection.RetainWindowBytes,
                        (byteOffset, matchedBytes, window) =>
                        {
                            resourceBudget.ObserveMatch();
                            var row = new SearchByteMatch(
                                relativePath,
                                matchIndex,
                                byteOffset,
                                _pattern.Length,
                                matchedBytes,
                                windowBytes: window?.Bytes,
                                windowStartByteOffset: window?.StartByteOffset,
                                windowByteLength: window?.ByteLength,
                                windowComplete: window?.Complete);
                            matchIndex = checked(matchIndex + 1);
                            occurrencesObserved = checked(occurrencesObserved + 1);

                            if (acceptedRow is not null && !acceptedRow(row))
                                return;

                            var rowBytes = outputBudget is null
                                ? 0
                                : SearchRowSizeEstimator.Estimate(row);
                            outputBudget?.Reserve(rowBytes);
                            stagedOutputBytes = checked(stagedOutputBytes + rowBytes);
                            (rows ??= []).Add(row);

                            if (rows.Count >= RowChunking.DefaultChunkSize)
                                FlushRows();
                        },
                        accounting.RecordFileRead,
                        fileCancellationToken);

                    sourceObservation.EnsureCurrent(fileCancellationToken);
                    fileCancellationToken.ThrowIfCancellationRequested();
                    FlushRows();
                    accounting.RecordFileCompleted(
                        fileBudget.ReservedBytes,
                        occurrencesObserved,
                        matchingLines: 0,
                        occurrencesObserved > 0);
                }
                catch (Exception exception)
                {
                    if (exception is not OperationCanceledException)
                    {
                        var failure = SearchTerminalFailure.From(exception);
                        accounting.RecordFileFailed(failure.Code, failure.Path ?? file);
                    }

                    throw;
                }
                finally
                {
                    rows?.Clear();
                    outputBudget?.Release(stagedOutputBytes);
                }
            }

            try
            {
                void ReportRowsRead(long rowsRead)
                {
                    totalRowsProcessed = checked(totalRowsProcessed + rowsRead);
                    accounting.RecordObservedRows(rowsRead);
                    progress.RowsRead(rowsRead);
                }

                SearchFileParallelCoordinator.Run(
                    token => EnumerateEligibleFiles(rootPath, token),
                    writer,
                    ProcessFile,
                    acceptedWindow,
                    ReportRowsRead,
                    cancellationToken,
                    _parallelOptions,
                    estimateChunkBytes: SearchRowSizeEstimator.EstimateChunk<SearchByteMatch>);

                IEnumerable<string> EnumerateEligibleFiles(
                    string searchRoot,
                    CancellationToken token)
                {
                    foreach (var file in SearchScopeTraversal.Enumerate(
                                 searchRoot,
                                 _scope,
                                 token,
                                 scopeCounters))
                    {
                        accounting.RecordEligibleFile();
                        yield return file;
                    }
                }
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
                    SearchDiagnosticCatalog.SourceOpenFailed(rootPath),
                    exception);
            }
        }
        catch (Exception exception)
        {
            terminalException = exception;
            throw;
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(totalRowsProcessed);

            var terminalFailure = terminalException is null
                ? default
                : SearchTerminalFailure.From(terminalException);
            if (terminalException is not null)
            {
                accounting.RecordFailure(
                    terminalFailure.Code,
                    terminalFailure.Path);
            }

            var outcome = terminalException is null
                ? IsQuerySatisfied(_executionContext, accounting)
                    ? SearchOutcome.QuerySatisfied
                    : SearchOutcome.ScopeExhausted
                : SearchOutcome.Failed;
            var terminalReason = outcome switch
            {
                SearchOutcome.QuerySatisfied => "take-reached",
                SearchOutcome.ScopeExhausted => SuccessfulReason(accounting),
                _ => terminalFailure.Reason
            };

            LastExecution = accounting.Finish(
                outcome,
                terminalReason,
                scopeCounters.FilesConsidered,
                terminalFailure.Code,
                terminalFailure.Path);
        }
    }

    private static string RequireRoot(string? root)
    {
        return string.IsNullOrEmpty(root)
            ? throw new SearchRequestException(SearchDiagnosticCatalog.InvalidArgument("root"))
            : root;
    }

    private static bool IsQuerySatisfied(
        SourceExecutionContext executionContext,
        SearchExecutionAccounting accounting)
    {
        return executionContext.Plan.AcceptedTake == 0 ||
               accounting.ObservedRows > 0 &&
               executionContext.Plan.AcceptedTake is not null &&
               accounting.ObservedRows >= executionContext.Plan.AcceptedTake.Value;
    }

    private static string SuccessfulReason(SearchExecutionAccounting accounting)
    {
        if (accounting.EligibleFiles == 0)
            return "no-eligible-files";

        return accounting.Occurrences == 0
            ? "no-match"
            : "completed-scan";
    }
}
