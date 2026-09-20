#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Bytes;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Components.Many;

/// <summary>
///     Coordinates one scope traversal and one text read per eligible file for
///     a shared literal-set matcher. Public source binding is deliberately
///     outside this internal execution seam.
/// </summary>
internal static class SearchManyLiteralScan
{
    public static void ScanScope(
        string rootPath,
        SearchManyRequest request,
        CancellationToken cancellationToken,
        Action<string, MatchSpan> matchFound,
        Func<string, TextReader>? readerFactory = null,
        SearchScopeCounters? counters = null,
        SearchResourceBudget? resourceBudget = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(matchFound);

        ScanScopeCore(
            rootPath,
            request,
            cancellationToken,
            relativePath => new CallbackScanSink(relativePath, matchFound, null),
            readerFactory,
            counters,
            resourceBudget);
    }

    /// <summary>
    ///     Scans a scope and delivers every matcher output for one read as one
    ///     borrowed batch. The callback must consume the list synchronously;
    ///     code that crosses an asynchronous or native boundary must copy it
    ///     before returning.
    /// </summary>
    internal static void ScanScopeBatched(
        string rootPath,
        SearchManyRequest request,
        CancellationToken cancellationToken,
        Action<string, IReadOnlyList<MatchSpan>> matchBatchFound,
        Func<string, TextReader>? readerFactory = null,
        SearchScopeCounters? counters = null,
        SearchResourceBudget? resourceBudget = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(matchBatchFound);

        ScanScopeCore(
            rootPath,
            request,
            cancellationToken,
            relativePath => new CallbackScanSink(relativePath, null, matchBatchFound),
            readerFactory,
            counters,
            resourceBudget);
    }

    private static void ScanScopeCore(
        string rootPath,
        SearchManyRequest request,
        CancellationToken cancellationToken,
        Func<string, ISearchTextScanSink> createSink,
        Func<string, TextReader>? readerFactory,
        SearchScopeCounters? counters,
        SearchResourceBudget? resourceBudget)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(createSink);

        var resolvedRoot = Path.GetFullPath(rootPath);
        var classifyBinaryFiles = readerFactory is null;
        readerFactory ??= path => SearchTextReader.Open(path, request.Options.EncodingMode);
        var relativeRoot = File.Exists(resolvedRoot)
            ? Path.GetDirectoryName(resolvedRoot) ?? resolvedRoot
            : resolvedRoot;
        var matcher = new SearchManyLiteralMatcher(request);
        var files = SearchScopeTraversal.Enumerate(
            resolvedRoot,
            request.Options.Scope,
            cancellationToken,
            counters);

        using var buffer = SearchCharBuffer.Rent();
        var spans = new List<MatchSpan>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileBudget = resourceBudget?.BeginFile(file);
            if (fileBudget is not null)
                fileBudget.ReserveBytes(new FileInfo(file).Length);

            if (classifyBinaryFiles)
            {
                IncrementContentOpenAttempts(counters);
                if (SearchBinaryPolicy.IsBinaryFile(
                        file,
                        request.Options.EncodingMode,
                        buffer,
                        cancellationToken))
                {
                    counters?.IncrementBinaryFilesSkipped();

                    continue;
                }
            }

            IncrementContentOpenAttempts(counters);
            var relativePath = SearchTextPath.GetRelativePath(relativeRoot, file);
            var sink = createSink(relativePath);
            SearchTextScanner.ScanFile(
                matcher,
                file,
                buffer,
                spans,
                cancellationToken,
                readerFactory,
                sink,
                maxRecordBytes: request.Options.Limits.MaxRecordBytes <
                                SearchRegexScanner.MaxMultilineRecordBytes
                    ? request.Options.Limits.MaxRecordBytes
                    : SearchResourceLimits.Unlimited,
                encodingMode: request.Options.EncodingMode);
        }
    }

    private static void IncrementContentOpenAttempts(SearchScopeCounters? counters)
    {
        counters?.IncrementContentOpenAttempts();
    }

    private sealed class CallbackScanSink : ISearchTextScanSink
    {
        private readonly string _relativePath;
        private readonly Action<string, MatchSpan>? _matchFound;
        private readonly Action<string, IReadOnlyList<MatchSpan>>? _matchBatchFound;

        public CallbackScanSink(
            string relativePath,
            Action<string, MatchSpan>? matchFound,
            Action<string, IReadOnlyList<MatchSpan>>? matchBatchFound)
        {
            _relativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            _matchFound = matchFound;
            _matchBatchFound = matchBatchFound;
            if ((_matchFound is null) == (_matchBatchFound is null))
            {
                throw new ArgumentException(
                    "Exactly one callback shape must be supplied.");
            }
        }

        public bool NeedsLineText => false;

        public bool NeedsMatchText => false;

        public bool NeedsCaptures => false;

        public bool NeedsLineCompletion => false;

        public long RowsEmitted { get; private set; }

        public bool AcceptMatch(MatchSpan span)
        {
            (_matchFound ?? throw new InvalidOperationException(
                    "The callback sink is not configured for single matches."))(
                _relativePath,
                span);
            RowsEmitted = checked(RowsEmitted + 1);
            return false;
        }

        public bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(spans);
            if (_matchBatchFound is null)
                return ISearchTextScanSinkAcceptMatches(spans, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _matchBatchFound(_relativePath, spans);
            RowsEmitted = checked(RowsEmitted + spans.Count);
            return false;
        }

        private bool ISearchTextScanSinkAcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken)
        {
            for (var index = 0; index < spans.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (AcceptMatch(spans[index]))
                    return true;
            }

            return false;
        }

        public bool HasMatchesOnLine(long lineNumber)
        {
            return false;
        }

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
        }
    }
}
