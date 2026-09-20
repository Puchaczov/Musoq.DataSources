#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Many;

/// <summary>
///     Scans one decoded input stream with the shared literal matcher and all
///     regex patterns. Regex records are completed at physical line
///     boundaries, so the file is opened and decoded only once.
/// </summary>
internal static class SearchManyMixedScan
{
    internal static SearchManyMixedPlan Compile(SearchManyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateExecutionOptions(request.Options);
        return SearchManyMixedPlan.Create(request);
    }

    private static void ValidateExecutionOptions(SearchManyOptions options)
    {
        if (options.CaseMode is not SearchCaseMode.Sensitive and not SearchCaseMode.Insensitive)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyInput(
                    "options.case is not supported by the many matcher",
                    SearchDiagnosticPhase.Argument));
        }

        if (options.Selection != SearchSelectionMode.LeftmostFirstNonOverlapping)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyInput(
                    "options.selection is not supported by the many matcher",
                    SearchDiagnosticPhase.Argument));
        }

        if (options.Take is not null ||
            options.PartialPolicy != SearchPartialPolicy.Reject ||
            options.Validation != SearchValidationMode.FullInput)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidManyInput(
                    "options.take, options.partialPolicy=allow and options.validation=observed-prefix require the later completion surface",
                    SearchDiagnosticPhase.Argument));
        }
    }

    internal static void ScanFile(
        string filePath,
        SearchManyMixedPlan plan,
        CancellationToken cancellationToken,
        Action<IReadOnlyList<MatchSpan>> matchBatchFound,
        bool retainMatchText,
        bool retainCaptures,
        Func<string, TextReader> readerFactory,
        SearchCharBuffer buffer,
        SearchContextOptions? context,
        bool retainContext)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(matchBatchFound);
        ArgumentNullException.ThrowIfNull(readerFactory);
        ArgumentNullException.ThrowIfNull(buffer);

        var matcher = new MixedMatcher(
            plan,
            retainMatchText,
            retainCaptures);
        var sink = new CallbackSink(
            matchBatchFound,
            retainContext && context is { IsEnabled: true },
            context ?? SearchContextOptions.Disabled);
        SearchTextScanner.ScanFile(
            matcher,
            filePath,
            buffer,
            new List<MatchSpan>(),
            cancellationToken,
            readerFactory,
            sink,
            readerOpened: null,
            maxRecordBytes: plan.Options.HasExplicitMaxRecordBytes ||
                            plan.RegexPatterns.Length > 0
                ? plan.Options.Limits.MaxRecordBytes
                : SearchResourceLimits.Unlimited,
            encodingMode: plan.Options.EncodingMode);
        sink.CompleteFile();
    }

    private sealed class CallbackSink(
        Action<IReadOnlyList<MatchSpan>> callback,
        bool retainContext,
        SearchContextOptions context) : ISearchTextScanSink
    {
        private readonly Dictionary<long, List<MatchSpan>> _matchesByLine = [];
        private readonly Queue<PendingMatch> _pendingMatches = [];
        private readonly Queue<ContextLineData> _beforeLines = [];

        public bool NeedsLineText => retainContext;

        public bool NeedsMatchText => false;

        public bool NeedsCaptures => false;

        public bool NeedsLineCompletion => retainContext;

        public bool NeedsEveryLineCompletion => retainContext;

        public int MaxLineTextLength => retainContext ? context.MaxBytesPerLine : int.MaxValue;

        public long RowsEmitted { get; private set; }

        public bool AcceptMatch(MatchSpan span)
        {
            AcceptMatchCore(span);
            RowsEmitted = checked(RowsEmitted + 1);
            return false;
        }

        public bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (spans.Count == 0)
                return false;

            for (var index = 0; index < spans.Count; index++)
                AcceptMatchCore(spans[index]);

            RowsEmitted = checked(RowsEmitted + spans.Count);
            return false;
        }

        public bool HasMatchesOnLine(long lineNumber) =>
            retainContext && _matchesByLine.ContainsKey(lineNumber);

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
            if (!retainContext)
                return;

            var currentLine = new ContextLineData(
                lineNumber,
                SearchContextText.Truncate(lineText, context.MaxBytesPerLine));

            foreach (var pending in _pendingMatches)
            {
                var relativeLine = lineNumber - pending.Span.LineNumber;
                if (relativeLine > 0 && relativeLine <= context.AfterLines)
                {
                    pending.Context.Add(new ContextLineReference(
                        checked((int)relativeLine),
                        currentLine));
                }
            }

            if (_matchesByLine.Remove(lineNumber, out var matches))
            {
                foreach (var span in matches)
                {
                    var references = new List<ContextLineReference>();
                    foreach (var beforeLine in _beforeLines)
                    {
                        var relativeLine = beforeLine.LineNumber - lineNumber;
                        references.Add(new ContextLineReference(
                            checked((int)relativeLine),
                            beforeLine));
                    }

                    var pending = new PendingMatch(span);
                    pending.Context.AddRange(references);
                    _pendingMatches.Enqueue(pending);
                }
            }

            if (context.BeforeLines > 0)
            {
                _beforeLines.Enqueue(currentLine);
                while (_beforeLines.Count > context.BeforeLines)
                    _beforeLines.Dequeue();
            }

            FlushReady(lineNumber);
        }

        public void CompleteFile()
        {
            if (!retainContext)
                return;

            while (_pendingMatches.TryDequeue(out var pending))
                callback([pending.Materialize()]);
        }

        private void AcceptMatchCore(MatchSpan span)
        {
            if (!retainContext)
            {
                callback([span]);
                return;
            }

            if (!_matchesByLine.TryGetValue(span.LineNumber, out var matches))
            {
                matches = [];
                _matchesByLine.Add(span.LineNumber, matches);
            }

            matches.Add(span);
        }

        private void FlushReady(long lineNumber)
        {
            while (_pendingMatches.TryPeek(out var pending))
            {
                var relativeLine = lineNumber - pending.Span.LineNumber;
                if (relativeLine < context.AfterLines)
                    break;

                _pendingMatches.Dequeue();
                callback([pending.Materialize()]);
            }
        }

        private static IReadOnlyList<SearchContextLine> MaterializeContext(
            List<ContextLineReference> references)
        {
            if (references.Count == 0)
                return Array.Empty<SearchContextLine>();

            var context = new SearchContextLine[references.Count];
            for (var index = 0; index < references.Count; index++)
            {
                var reference = references[index];
                context[index] = new SearchContextLine(
                    reference.RelativeLine,
                    reference.Line.LineNumber,
                    reference.Line.LineText);
            }

            return context;
        }

        private sealed class PendingMatch(MatchSpan span)
        {
            public MatchSpan Span { get; } = span;

            public List<ContextLineReference> Context { get; } = [];

            public MatchSpan Materialize()
            {
                return Span with { Context = MaterializeContext(Context) };
            }
        }

        private sealed class ContextLineData(long lineNumber, string? lineText)
        {
            public long LineNumber { get; } = lineNumber;

            public string? LineText { get; } = lineText;
        }

        private readonly record struct ContextLineReference(
            int RelativeLine,
            ContextLineData Line);
    }

    private sealed class MixedMatcher : ISearchTextMatcher
    {
        private const int MaximumRegexPhysicalRecordLength = SearchRegexScanner.MaxRecordLength;

        private readonly SearchManyLiteralMatcher? _literalMatcher;
        private readonly SearchManyMixedPlan.RegexPattern[] _regexPatterns;
        private readonly bool _retainMatchText;
        private readonly bool _retainCaptures;
        private readonly StringBuilder _line = new();
        private readonly List<SearchCharCoordinate> _lineCoordinates = [];
        private readonly List<MatchSpan> _regexSpans = [];
        private long _lineNumber;
        private long _lineStart;
        private long? _lineByteOffset;

        public MixedMatcher(
            SearchManyMixedPlan plan,
            bool retainMatchText,
            bool retainCaptures)
        {
            _literalMatcher = plan.LiteralPatterns.Count == 0
                ? null
                : new SearchManyLiteralMatcher(
                    plan.LiteralPlan!,
                    retainMatchText);
            _regexPatterns = plan.RegexPatterns;
            _retainMatchText = retainMatchText;
            _retainCaptures = retainCaptures;
            _wholeWord = plan.Options.WholeWord;
        }

        public void Reset()
        {
            _literalMatcher?.Reset();
            _line.Clear();
            _lineCoordinates.Clear();
            _regexSpans.Clear();
            _lineNumber = 1;
            _lineStart = 0;
            _lineByteOffset = null;
        }

        public void ConsumeBlock(
            ReadOnlySpan<char> block,
            ref long lineNumber,
            ref long utf16Column,
            ICollection<MatchSpan> spans,
            ReadOnlySpan<SearchCharCoordinate> coordinates,
            CancellationToken cancellationToken = default)
        {
            if (_literalMatcher is not null)
            {
                _literalMatcher.ConsumeBlock(
                    block,
                    ref lineNumber,
                    ref utf16Column,
                    spans,
                    coordinates,
                    cancellationToken);
            }

            for (var index = 0; index < block.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var character = block[index];
                var coordinate = coordinates.Length == block.Length
                    ? coordinates[index]
                    : default;
                if (_line.Length == 0)
                {
                    _lineByteOffset = coordinate.IsMapped
                        ? coordinate.ByteOffset
                        : null;
                }

                if (character == '\n')
                {
                    CompleteRegexLine(spans, cancellationToken);
                    _line.Clear();
                    _lineCoordinates.Clear();
                    _lineNumber = checked(_lineNumber + 1);
                    _lineStart = checked(_lineStart + 1);
                    _lineByteOffset = coordinate.IsMapped && coordinate.CanEnd
                        ? coordinate.ByteEndExclusive
                        : null;
                    continue;
                }

                if (_line.Length >= MaximumRegexPhysicalRecordLength)
                {
                    throw new SearchResourceLimitException(
                        SearchDiagnosticCatalog.RegexRecordTooLong(
                            MaximumRegexPhysicalRecordLength));
                }

                _line.Append(character);
                _lineCoordinates.Add(coordinate);
                _lineStart = checked(_lineStart + 1);
            }

            // SearchTextScanner owns the authoritative line coordinates for
            // literal spans. The mixed cursor only needs to keep its own line
            // number and record state for regex spans.
            lineNumber = _literalMatcher is null ? _lineNumber : lineNumber;
        }

        public void Complete(
            ICollection<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            CompleteRegexLine(spans, cancellationToken, includeEmpty: false);
            _literalMatcher?.Complete(spans, cancellationToken);
        }

        private void CompleteRegexLine(
            ICollection<MatchSpan> spans,
            CancellationToken cancellationToken,
            bool includeEmpty = true)
        {
            if (_regexPatterns.Length == 0 || (!includeEmpty && _line.Length == 0))
                return;

            var record = _line.ToString();
            if (record.EndsWith('\r'))
            {
                record = record[..^1];
                if (_lineCoordinates.Count > record.Length)
                    _lineCoordinates.RemoveRange(record.Length, _lineCoordinates.Count - record.Length);
            }

            _regexSpans.Clear();
            foreach (var pattern in _regexPatterns)
            {
                SearchRegexScanner.AppendPhysicalRecordMatches(
                    pattern.Regex,
                    pattern.Id,
                    record,
                    _lineCoordinates,
                    checked(_lineStart - _line.Length),
                    _lineNumber,
                    _lineByteOffset,
                    _wholeWord,
                    _retainMatchText,
                    _retainCaptures,
                    _regexSpans,
                    cancellationToken);
            }

            // Delivering after each physical line keeps memory bounded.
            for (var index = 0; index < _regexSpans.Count; index++)
                spans.Add(_regexSpans[index]);
        }

        private readonly bool _wholeWord;
    }
}

internal sealed class SearchManyMixedPlan
{
    private SearchManyMixedPlan(
        SearchManyOptions options,
        IReadOnlyList<SearchManyPattern> literalPatterns,
        RegexPattern[] regexPatterns,
        SearchManyLiteralMatcher.LiteralPlan? literalPlan)
    {
        Options = options;
        LiteralPatterns = literalPatterns;
        RegexPatterns = regexPatterns;
        LiteralPlan = literalPlan;
    }

    public SearchManyOptions Options { get; }

    public IReadOnlyList<SearchManyPattern> LiteralPatterns { get; }

    public RegexPattern[] RegexPatterns { get; }

    public SearchManyLiteralMatcher.LiteralPlan? LiteralPlan { get; }

    public static SearchManyMixedPlan Create(SearchManyRequest request)
    {
        var literalPatterns = new List<SearchManyPattern>();
        var regexPatterns = new List<RegexPattern>();
        for (var index = 0; index < request.Patterns.Count; index++)
        {
            var pattern = request.Patterns[index];
            if (pattern.Mode == SearchPatternMode.Literal)
            {
                literalPatterns.Add(pattern);
                continue;
            }

            regexPatterns.Add(new RegexPattern(
                pattern.Id,
                SearchRegexBackend.Compile(
                    pattern.Pattern,
                    request.Options.CaseMode,
                    request.Options.WholeWord,
                    request.Options.Limits.MaxPatternLength,
                    request.Options.Limits.MaxPatternCompilationMilliseconds)));
        }

        var literalPlan = literalPatterns.Count == 0
            ? null
            : SearchManyLiteralMatcher.LiteralPlan.Create(
                new SearchManyRequest(literalPatterns, request.Options));

        return new SearchManyMixedPlan(
            request.Options,
            literalPatterns.AsReadOnly(),
            regexPatterns.ToArray(),
            literalPlan);
    }

    internal readonly record struct RegexPattern(string Id, Regex Regex);
}
