#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;

namespace Musoq.DataSources.Search.Components.Text;

internal sealed class SearchOccurrenceSink : SearchTextRowSink<SearchMatch>
{
    private readonly string _relativePath;
    private readonly string _literal;
    private readonly bool _retainMatchText;
    private readonly bool _retainCaptures;
    private readonly bool _retainContext;
    private readonly SearchContextOptions _context;
    private readonly SearchEvidenceHandle? _evidence;
    private readonly Dictionary<long, List<PendingMatch>> _matchesByLine = [];
    private readonly Queue<PendingMatch> _pendingMatches = [];
    private readonly Queue<ContextLineData> _beforeLines = [];
    private readonly SearchOutputStagingBudget? _pendingOutputBudget;
    private long _pendingOutputBytes;
    private long _matchIndex;
    private long _occurrencesObserved;
    private long _matchingLinesObserved;
    private long? _lastMatchingLine;

    public SearchOccurrenceSink(
        string relativePath,
        string literal,
        bool retainMatchText,
        bool retainCaptures,
        bool retainContext,
        SearchContextOptions context,
        SearchEvidenceHandle? evidence,
        IChunkWriter<SearchMatch> writer,
        Func<SearchMatch, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget)
        : base(
            writer,
            acceptedRow,
            rowsWritten,
            resourceBudget,
            SearchRowSizeEstimator.Estimate)
    {
        _relativePath = relativePath;
        _literal = literal;
        _retainMatchText = retainMatchText;
        _retainCaptures = retainCaptures;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _evidence = evidence;
        _retainContext = retainContext && context.IsEnabled;
        _pendingOutputBudget = _retainContext
            ? resourceBudget.OutputBudget
            : null;
    }

    public override bool NeedsLineText => _retainContext;

    public override bool NeedsMatchText => _retainMatchText;

    public override bool NeedsCaptures => _retainCaptures;

    public override bool NeedsLineCompletion => _retainContext;

    public override bool NeedsEveryLineCompletion => _retainContext;

    public override long OccurrencesObserved => _occurrencesObserved;

    public override long MatchingLinesObserved => _matchingLinesObserved;

    public override int MaxLineTextLength =>
        _retainContext ? _context.MaxBytesPerLine : int.MaxValue;

    public override bool AcceptMatch(MatchSpan span)
    {
        ObserveMatch();
        AcceptMatchCore(span);
        return false;
    }

    public override bool AcceptMatches(
        IReadOnlyList<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        for (var index = 0; index < spans.Count; index++)
        {
            if ((index & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            ObserveMatch();
            AcceptMatchCore(spans[index]);
        }

        return false;
    }

    private void AcceptMatchCore(MatchSpan span)
    {
        _occurrencesObserved = checked(_occurrencesObserved + 1);
        if (_lastMatchingLine != span.LineNumber)
        {
            _matchingLinesObserved = checked(_matchingLinesObserved + 1);
            _lastMatchingLine = span.LineNumber;
        }

        var matchIndex = _matchIndex;
        _matchIndex = checked(_matchIndex + 1);
        if (_retainContext)
        {
            var reservation = _pendingOutputBudget is null
                ? 0
                : SearchRowSizeEstimator.EstimatePotentialMatch(
                    _relativePath,
                    _retainMatchText ? span.MatchText ?? _literal : null,
                    _context,
                    _retainCaptures ? span.Captures : null);
            _pendingOutputBudget?.Reserve(reservation);
            _pendingOutputBytes = checked(_pendingOutputBytes + reservation);
            if (!_matchesByLine.TryGetValue(span.LineNumber, out var matches))
            {
                matches = [];
                _matchesByLine.Add(span.LineNumber, matches);
            }

            matches.Add(new PendingMatch(span, matchIndex)
            {
                ReservationBytes = reservation
            });
        }
        else
        {
            EmitMatch(new PendingMatch(span, matchIndex));
        }
    }

    public override bool HasMatchesOnLine(long lineNumber)
    {
        return _retainContext && _matchesByLine.ContainsKey(lineNumber);
    }

    public override void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
    {
        if (!_retainContext)
            return;

        var currentLine = new ContextLineData(
            lineNumber,
            SearchContextText.Truncate(lineText, _context.MaxBytesPerLine));

        foreach (var pending in _pendingMatches)
        {
            var relativeLine = lineNumber - pending.Span.LineNumber;
            if (relativeLine > 0 && relativeLine <= _context.AfterLines)
            {
                pending.Context.Add(new ContextLineReference(
                    checked((int)relativeLine),
                    currentLine));
            }
        }

        if (_matchesByLine.Remove(lineNumber, out var matches))
        {
            foreach (var pending in matches)
            {
                foreach (var beforeLine in _beforeLines)
                {
                    var relativeLine = beforeLine.LineNumber - lineNumber;
                    pending.Context.Add(new ContextLineReference(
                        checked((int)relativeLine),
                        beforeLine));
                }

                _pendingMatches.Enqueue(pending);
            }
        }

        if (_context.BeforeLines > 0)
        {
            _beforeLines.Enqueue(currentLine);
            while (_beforeLines.Count > _context.BeforeLines)
                _beforeLines.Dequeue();
        }

        FlushReady(lineNumber);
    }

    public override void CompleteFile(string filePath)
    {
        if (!_retainContext)
            return;

        while (_pendingMatches.TryDequeue(out var pending))
            EmitMatch(pending);

        ReleasePendingOutput();
    }

    private void FlushReady(long lineNumber)
    {
        while (_pendingMatches.TryPeek(out var pending))
        {
            var relativeLine = lineNumber - pending.Span.LineNumber;
            if (relativeLine < _context.AfterLines)
                break;

            _pendingMatches.Dequeue();
            EmitMatch(pending);
        }
    }

    private void EmitMatch(PendingMatch pending)
    {
        _pendingOutputBudget?.Release(pending.ReservationBytes);
        _pendingOutputBytes -= pending.ReservationBytes;
        var span = pending.Span;
        Emit(new SearchMatch(
            _relativePath,
            pending.MatchIndex,
            span.ByteOffset,
            span.ByteLength,
            span.LineNumber,
            span.Utf16Column,
            span.Length,
            _retainMatchText ? span.MatchText ?? _literal : null,
            _retainCaptures ? span.Captures : null,
            context: MaterializeContext(pending.Context),
            evidence: _evidence));
    }

    public override void Dispose()
    {
        ReleasePendingOutput();
        base.Dispose();
    }

    private void ReleasePendingOutput()
    {
        if (_pendingOutputBytes == 0)
            return;

        _pendingOutputBudget?.Release(_pendingOutputBytes);
        _pendingOutputBytes = 0;
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

    private sealed class PendingMatch(MatchSpan span, long matchIndex)
    {
        public MatchSpan Span { get; } = span;

        public long MatchIndex { get; } = matchIndex;

        public long ReservationBytes { get; set; }

        public List<ContextLineReference> Context { get; } = [];
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

internal sealed class SearchLineSink : SearchTextRowSink<SearchLine>
{
    private readonly string _relativePath;
    private readonly bool _retainLineText;
    private readonly Dictionary<long, long> _occurrencesByLine = [];
    private long _occurrencesObserved;
    private long _matchingLinesObserved;

    public SearchLineSink(
        string relativePath,
        bool retainLineText,
        IChunkWriter<SearchLine> writer,
        Func<SearchLine, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget)
        : base(
            writer,
            acceptedRow,
            rowsWritten,
            resourceBudget,
            SearchRowSizeEstimator.Estimate)
    {
        _relativePath = relativePath;
        _retainLineText = retainLineText;
    }

    public override bool NeedsLineText => _retainLineText;

    public override bool NeedsLineCompletion => true;

    public override long OccurrencesObserved => _occurrencesObserved;

    public override long MatchingLinesObserved => _matchingLinesObserved;

    public override bool AcceptMatch(MatchSpan span)
    {
        ObserveMatch();
        AcceptMatchCore(span);
        return false;
    }

    public override bool AcceptMatches(
        IReadOnlyList<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        for (var index = 0; index < spans.Count; index++)
        {
            if ((index & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            ObserveMatch();
            AcceptMatchCore(spans[index]);
        }

        return false;
    }

    private void AcceptMatchCore(MatchSpan span)
    {
        _occurrencesObserved = checked(_occurrencesObserved + 1);
        if (!_occurrencesByLine.ContainsKey(span.LineNumber))
            _matchingLinesObserved = checked(_matchingLinesObserved + 1);

        _occurrencesByLine.TryGetValue(span.LineNumber, out var occurrenceCount);
        _occurrencesByLine[span.LineNumber] = checked(occurrenceCount + 1);
    }

    public override bool HasMatchesOnLine(long lineNumber)
    {
        return _occurrencesByLine.ContainsKey(lineNumber);
    }

    public override void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
    {
        if (!_occurrencesByLine.Remove(lineNumber, out var occurrenceCount))
            return;

        Emit(new SearchLine(
            _relativePath,
            null,
            null,
            lineNumber,
            byteOffset,
            _retainLineText ? lineText : null,
            occurrenceCount));
    }
}

internal sealed class SearchFileSink : SearchTextRowSink<SearchFile>
{
    private readonly string _relativePath;
    private long _occurrencesObserved;

    public SearchFileSink(
        string relativePath,
        IChunkWriter<SearchFile> writer,
        Func<SearchFile, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget)
        : base(
            writer,
            acceptedRow,
            rowsWritten,
            resourceBudget,
            SearchRowSizeEstimator.Estimate)
    {
        _relativePath = relativePath;
    }

    public override bool NeedsLineText => false;

    public override bool RequiresImmediateMatchDelivery => true;

    public override long OccurrencesObserved => _occurrencesObserved;

    public override long MatchingLinesObserved => _occurrencesObserved == 0 ? 0 : 1;

    public override bool AcceptMatch(MatchSpan span)
    {
        ObserveMatch();
        _occurrencesObserved = 1;
        Emit(new SearchFile(_relativePath, null, null));
        return true;
    }

    public override bool AcceptMatches(
        IReadOnlyList<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        if (spans.Count == 0)
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        ObserveMatch();
        _occurrencesObserved = 1;
        Emit(new SearchFile(_relativePath, null, null));
        return true;
    }

    public override bool HasMatchesOnLine(long lineNumber) => false;

    public override void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
    {
    }
}

internal sealed class SearchCountSink : SearchTextRowSink<SearchCount>
{
    private readonly string _relativePath;
    private readonly SearchCountAccumulator _counts = new();

    public SearchCountSink(
        string relativePath,
        IChunkWriter<SearchCount> writer,
        Func<SearchCount, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget)
        : base(
            writer,
            acceptedRow,
            rowsWritten,
            resourceBudget,
            SearchRowSizeEstimator.Estimate)
    {
        _relativePath = relativePath;
    }

    public override bool NeedsLineText => false;

    public override long OccurrencesObserved => _counts.OccurrenceCount;

    public override long MatchingLinesObserved => _counts.MatchingLineCount;

    public override bool AcceptMatch(MatchSpan span)
    {
        ObserveMatch();
        AcceptMatchCore(span);
        return false;
    }

    public override bool AcceptMatches(
        IReadOnlyList<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        for (var index = 0; index < spans.Count; index++)
        {
            if ((index & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            ObserveMatch();
            AcceptMatchCore(spans[index]);
        }

        return false;
    }

    private void AcceptMatchCore(MatchSpan span)
    {
        _counts.AddMatch(span.LineNumber);
    }

    public override bool HasMatchesOnLine(long lineNumber) => false;

    public override void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
    {
    }

    public override void CompleteFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var bytesScanned = new FileInfo(filePath).Length;
        _counts.AddBytes(bytesScanned);
        Emit(new SearchCount(
            _relativePath,
            null,
            null,
            _counts.OccurrenceCount,
            _counts.MatchingLineCount,
            _counts.BytesScanned,
            complete: true));
    }
}
