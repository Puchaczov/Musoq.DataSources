#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Components.Text;

internal abstract class SearchTextSourceBase<TRow> : RowSourceBase<TRow>
{
    private const string SearchSourceName = "search";
    private const long SmallFileSnapshotBytes = 4L * 1024 * 1024;

    private readonly SearchRequest _request;
    private readonly SourceExecutionContext _executionContext;
    private readonly Func<string, TextReader> _readerFactory;
    private readonly bool _classifyBinaryFiles;
    private readonly SearchScopeCounters? _scopeCounters;
    private readonly SearchFileParallelOptions _parallelOptions;

    internal SearchTerminalSummary? LastExecution { get; private set; }

    protected SearchTextSourceBase(
        SearchRequest request,
        SourceExecutionContext executionContext,
        Func<string, TextReader>? readerFactory = null,
        SearchScopeCounters? scopeCounters = null)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _classifyBinaryFiles = readerFactory is null;
        _readerFactory = readerFactory ?? (path => SearchTextReader.Open(path, _request.EncodingMode));
        _scopeCounters = scopeCounters;
        _parallelOptions = readerFactory is null
            ? SearchFileParallelOptions.FromRuntimeSettings(executionContext.SourceRuntimeSettings)
            : SearchFileParallelOptions.Sequential;
    }

    protected abstract SearchTextRowSink<TRow> CreateSink(
        string relativePath,
        string literal,
        IChunkWriter<TRow> writer,
        SearchProjectionRequirements projection,
        SearchContextOptions context,
        SearchEvidenceHandle? evidence,
        Func<TRow, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget);

    protected abstract object? GetPredicateValue(TRow row, string columnName);

    protected override void CollectChunks(IChunkWriter<TRow> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, SearchSourceName);
        progress.Begin();
        var totalRowsProcessed = 0L;
        CancellationTokenSource? linkedCancellation = null;
        var cancellationToken = writer.CancellationToken;
        var scopeCounters = _scopeCounters ?? new SearchScopeCounters();
        var resourceBudget = new SearchResourceBudget(_request.Limits);
        var accounting = new SearchExecutionAccounting(
            Guid.NewGuid().ToString("N"),
            SearchScopeFingerprint.Create(_request.Root, _request.Scope));
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
                rootPath = Path.GetFullPath(_request.Root);
            }
            catch (ArgumentException exception)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument("root"),
                    exception);
            }

            accounting.SetScopeResolved(
                File.Exists(rootPath) || Directory.Exists(rootPath));

            var regex = _request.PatternMode == SearchPatternMode.Regex
                ? SearchRegexBackend.Compile(
                    _request.Literal,
                    _request.CaseMode,
                    _request.WholeWord,
                    _request.Limits.MaxPatternLength,
                    _request.Limits.MaxPatternCompilationMilliseconds)
                : null;
            var relativeRoot = File.Exists(rootPath)
                ? Path.GetDirectoryName(rootPath) ?? rootPath
                : rootPath;
            var projection = SearchProjectionRequirements.From(_executionContext);
            var acceptedPredicate = _executionContext.Plan.AcceptedPredicate;
            Func<TRow, bool>? acceptedRow = acceptedPredicate is null
                ? null
                : row => SearchPredicateEvaluator.Matches(
                    acceptedPredicate,
                    row,
                    GetPredicateValue);
            var acceptedWindow = SearchSliceWindow.Create(
                _executionContext.Plan.AcceptedSkip,
                _executionContext.Plan.AcceptedTake);

            void ProcessFile(
                string file,
                IChunkWriter<TRow> fileWriter,
                CancellationToken fileCancellationToken)
            {
                try
                {
                    var sourceObservation = SearchSourceObservation.Capture(
                        file,
                        fileCancellationToken);
                    SearchTestHooks.AfterObservation(file);
                    var fileBudget = resourceBudget.BeginFile(file);
                    fileBudget.ReserveBytes(sourceObservation.Length);
                    using var buffer = SearchCharBuffer.Rent();
                    using var smallInput = _classifyBinaryFiles &&
                                           sourceObservation.Length <= SmallFileSnapshotBytes
                        ? SearchSmallFileBuffer.Read(
                            file,
                            sourceObservation.Length,
                            fileCancellationToken)
                        : null;
                    Func<string, TextReader> readerFactory = _readerFactory;
                    if (_classifyBinaryFiles)
                    {
                        scopeCounters.IncrementContentOpenAttempts();
                        var isBinary = smallInput is not null
                            ? SearchBinaryPolicy.IsBinaryBytes(
                                smallInput.Array,
                                smallInput.Length,
                                _request.EncodingMode,
                                buffer,
                                fileCancellationToken)
                            : SearchBinaryPolicy.IsBinaryFile(
                                file,
                                _request.EncodingMode,
                                buffer,
                                fileCancellationToken);
                        if (isBinary)
                        {
                            scopeCounters.IncrementBinaryFilesSkipped();
                            accounting.RecordBinaryFileSkipped();
                            sourceObservation.EnsureCurrent(fileCancellationToken);
                            accounting.RecordFileCompleted(
                                bytesScanned: 0,
                                occurrences: 0,
                                matchingLines: 0,
                                matched: false);
                            return;
                        }

                        if (smallInput is not null)
                        {
                            readerFactory = _ => SearchTextReader.Open(
                                smallInput.Array,
                                smallInput.Length,
                                _request.EncodingMode);
                        }
                    }

                    if (smallInput is null)
                        scopeCounters.IncrementContentOpenAttempts();
                    accounting.RecordFileOpened();
                    var relativePath = SearchTextPath.GetRelativePath(relativeRoot, file);
                    var evidence = projection.RetainContext &&
                                   _request.Context.IsEnabled &&
                                   _classifyBinaryFiles
                        ? smallInput is null
                            ? SearchEvidenceHandle.Capture(
                                file,
                                _request.EncodingMode,
                                fileCancellationToken)
                            : SearchEvidenceHandle.Capture(
                                smallInput.Array,
                                smallInput.Length,
                                sourceObservation,
                                _request.EncodingMode)
                        : null;
                    using var sink = CreateSink(
                        relativePath,
                        _request.Literal,
                        fileWriter,
                        projection,
                        projection.RetainContext
                            ? _request.Context
                            : SearchContextOptions.Disabled,
                        evidence,
                        acceptedRow,
                        static _ => { },
                        resourceBudget);

                    if (_request.PatternMode == SearchPatternMode.Literal)
                    {
                        var literalMatcher = new LiteralMatcher(
                            _request.Literal,
                            _request.CaseMode,
                            _request.WholeWord);
                        SearchTextScanner.ScanFile(
                            literalMatcher,
                            file,
                            buffer,
                            [],
                            fileCancellationToken,
                            readerFactory,
                            sink,
                            accounting.RecordFileRead,
                            _request.HasExplicitMaxRecordBytes
                                ? _request.MaxRecordBytes
                                : SearchResourceLimits.Unlimited,
                            _request.EncodingMode);
                    }
                    else
                    {
                        SearchRegexScanner.ScanFile(
                            regex ?? throw new InvalidOperationException(
                                "The Search regex was not compiled for regex mode."),
                            file,
                            buffer,
                            _request.WholeWord,
                            _request.EncodingMode,
                            _request.RecordMode,
                            _request.MaxRecordBytes,
                            fileCancellationToken,
                            readerFactory,
                            sink,
                            _request.RecordFraming,
                            accounting.RecordFileRead,
                            _request.MaxRecordBytes);
                    }

                    sourceObservation.EnsureCurrent(fileCancellationToken);
                    fileCancellationToken.ThrowIfCancellationRequested();
                    sink.CompleteFile(file);
                    fileCancellationToken.ThrowIfCancellationRequested();
                    sink.Flush();
                    accounting.RecordFileCompleted(
                        fileBudget.ReservedBytes,
                        sink.OccurrencesObserved,
                        sink.MatchingLinesObserved,
                        sink.OccurrencesObserved > 0);
                }
                catch (Exception exception)
                {
                    if (exception is not OperationCanceledException &&
                        exception is not ISearchDiagnosticException &&
                        (exception is IOException ||
                         exception is UnauthorizedAccessException ||
                         exception is NotSupportedException))
                    {
                        exception = new SearchSourceReadException(
                            SearchDiagnosticCatalog.SourceReadFailed(file),
                            exception);
                    }

                    if (exception is not OperationCanceledException)
                    {
                        var failure = SearchTerminalFailure.From(exception);
                        accounting.RecordFileFailed(failure.Code, failure.Path ?? file);
                    }

                    throw;
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
                    estimateChunkBytes: SearchRowSizeEstimator.EstimateChunk<TRow>);

                IEnumerable<string> EnumerateEligibleFiles(
                    string searchRoot,
                    CancellationToken token)
                {
                    foreach (var file in SearchScopeTraversal.Enumerate(
                                 searchRoot,
                                 _request.Scope,
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
                : CanRetainPartialPrefix(terminalException, accounting)
                    ? SearchOutcome.Partial
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

    private static bool IsQuerySatisfied(
        SourceExecutionContext executionContext,
        SearchExecutionAccounting accounting)
    {
        return executionContext.Plan.AcceptedTake == 0 ||
               accounting.ObservedRows > 0 &&
               executionContext.Plan.AcceptedTake is not null &&
               accounting.ObservedRows >= executionContext.Plan.AcceptedTake.Value;
    }

    private bool CanRetainPartialPrefix(
        Exception terminalException,
        SearchExecutionAccounting accounting)
    {
        return _request.PartialPolicy == SearchPartialPolicy.Allow &&
               accounting.ObservedRows > 0 &&
               terminalException is not SearchRequestException &&
               terminalException is not SearchPatternException &&
               terminalException is not SearchEncodingException &&
               terminalException is not SearchRecordFramingException;
    }

    private static string SuccessfulReason(SearchExecutionAccounting accounting)
    {
        if (accounting.EligibleFiles == 0)
            return "no-eligible-files";

        if (accounting.BinaryFilesSkipped == accounting.EligibleFiles &&
            accounting.BinaryFilesSkipped > 0)
        {
            return "binary-skipped-by-policy";
        }

        return accounting.Occurrences == 0
            ? "no-match"
            : "completed-scan";
    }
}

internal static class SearchTextPath
{
    public static string GetRelativePath(string relativeRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(relativeRoot, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
            relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, '/');

        return relativePath;
    }
}
