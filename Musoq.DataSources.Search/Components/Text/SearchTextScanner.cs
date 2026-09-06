#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;

namespace Musoq.DataSources.Search.Components.Text;

internal interface ISearchTextMatcher
{
    void Reset();

    void ConsumeBlock(
        ReadOnlySpan<char> block,
        ref long lineNumber,
        ref long utf16Column,
        ICollection<MatchSpan> spans,
        ReadOnlySpan<SearchCharCoordinate> coordinates,
        CancellationToken cancellationToken = default);

    void Complete(
        ICollection<MatchSpan> spans,
        CancellationToken cancellationToken = default);
}

internal interface ISearchTextScanSink
{
    bool NeedsLineText { get; }

    bool NeedsMatchText { get; }

    bool NeedsCaptures { get; }

    bool NeedsLineCompletion { get; }

    bool NeedsEveryLineCompletion => false;

    int MaxLineTextLength => int.MaxValue;

    /// <summary>
    ///     Indicates that the sink must see the first match before the scanner
    ///     evaluates later matches in the same logical record.
    /// </summary>
    bool RequiresImmediateMatchDelivery => false;

    long RowsEmitted { get; }

    /// <summary>
    ///     Gets the number of matcher occurrences observed by this sink for
    ///     the current file, independent of accepted row filtering.
    /// </summary>
    long OccurrencesObserved => 0;

    /// <summary>
    ///     Gets the number of distinct matching physical lines observed by
    ///     this sink for the current file.
    /// </summary>
    long MatchingLinesObserved => 0;

    bool AcceptMatch(MatchSpan span);

    /// <summary>
    ///     Consumes a complete matcher output batch synchronously. The list is
    ///     borrowed and may be cleared or reused as soon as this call returns;
    ///     a consumer that crosses an asynchronous or native boundary must
    ///     copy the spans before returning.
    /// </summary>
    bool AcceptMatches(
        IReadOnlyList<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        for (var index = 0; index < spans.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AcceptMatch(spans[index]))
                return true;
        }

        return false;
    }

    bool HasMatchesOnLine(long lineNumber);

    void CompleteLine(long lineNumber, string? lineText, long? byteOffset);
}

internal abstract class SearchTextRowSink<TRow> : ISearchTextScanSink, IDisposable
{
    private readonly IChunkWriter<TRow> _writer;
    private readonly Func<TRow, bool>? _acceptedRow;
    private readonly Action<long> _rowsWritten;
    private readonly SearchResourceBudget _resourceBudget;
    private readonly Func<TRow, long> _estimateRowBytes;
    private readonly SearchOutputStagingBudget? _outputBudget;
    private List<TRow>? _rows;
    private long _stagedBytes;

    protected SearchTextRowSink(
        IChunkWriter<TRow> writer,
        Func<TRow, bool>? acceptedRow,
        Action<long> rowsWritten,
        SearchResourceBudget resourceBudget,
        Func<TRow, long> estimateRowBytes)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _acceptedRow = acceptedRow;
        _rowsWritten = rowsWritten ?? throw new ArgumentNullException(nameof(rowsWritten));
        _resourceBudget = resourceBudget ?? throw new ArgumentNullException(nameof(resourceBudget));
        _estimateRowBytes = estimateRowBytes ?? throw new ArgumentNullException(nameof(estimateRowBytes));
        _outputBudget = resourceBudget.OutputBudget;
    }

    public abstract bool NeedsLineText { get; }

    public virtual bool NeedsMatchText => false;

    public virtual bool NeedsCaptures => false;

    public virtual bool NeedsLineCompletion => NeedsLineText;

    public virtual bool NeedsEveryLineCompletion => false;

    public virtual int MaxLineTextLength => int.MaxValue;

    public virtual bool RequiresImmediateMatchDelivery => false;

    public long RowsEmitted { get; private set; }

    public virtual long OccurrencesObserved => 0;

    public virtual long MatchingLinesObserved => 0;

    public abstract bool AcceptMatch(MatchSpan span);

    public virtual bool AcceptMatches(
        IReadOnlyList<MatchSpan> spans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spans);
        for (var index = 0; index < spans.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AcceptMatch(spans[index]))
                return true;
        }

        return false;
    }

    public abstract bool HasMatchesOnLine(long lineNumber);

    public abstract void CompleteLine(long lineNumber, string? lineText, long? byteOffset);

    public virtual void CompleteFile(string filePath)
    {
    }

    protected void Emit(TRow row)
    {
        if (_acceptedRow is not null && !_acceptedRow(row))
            return;

        var rowBytes = _outputBudget is null ? 0 : _estimateRowBytes(row);
        _outputBudget?.Reserve(rowBytes);
        var rows = _rows ??= [];
        var rowStaged = false;
        try
        {
            rows.Add(row);
            _stagedBytes = checked(_stagedBytes + rowBytes);
            rowStaged = true;
            RowsEmitted++;
        }
        catch
        {
            if (rowStaged)
                _stagedBytes -= rowBytes;
            _outputBudget?.Release(rowBytes);
            throw;
        }

        if (rows.Count >= RowChunking.DefaultChunkSize)
            Flush();
    }

    public void Flush()
    {
        var rows = _rows;
        if (rows is null || rows.Count == 0)
            return;

        var stagedBytes = _stagedBytes;
        _writer.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            // RowChunk retains the supplied list as its backing storage, so
            // each flush must hand it an owned snapshot before the sink clears
            // its bounded staging list.
            _writer.Write(rows.ToArray());
        }
        catch (OperationCanceledException)
        {
            _outputBudget?.Release(stagedBytes);
            _stagedBytes = 0;
            throw;
        }
        catch (Exception exception)
        {
            _outputBudget?.Release(stagedBytes);
            _stagedBytes = 0;
            throw new SearchOutputException(
                SearchDiagnosticCatalog.OutputFailed(),
                exception);
        }

        var rowsWritten = rows.Count;
        rows.Clear();
        _stagedBytes = 0;
        _outputBudget?.Release(stagedBytes);
        _rowsWritten(rowsWritten);
    }

    public virtual void Dispose()
    {
        _rows?.Clear();
        _rows = null;
        _outputBudget?.Release(_stagedBytes);
        _stagedBytes = 0;
    }

    protected void ObserveMatch()
    {
        _resourceBudget.ObserveMatch();
    }
}

internal sealed class SearchCountAccumulator
{
    private long? _lastMatchingLine;

    public SearchCountAccumulator(
        long occurrenceCount = 0,
        long matchingLineCount = 0,
        long bytesScanned = 0)
    {
        if (occurrenceCount < 0)
            throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
        if (matchingLineCount < 0)
            throw new ArgumentOutOfRangeException(nameof(matchingLineCount));
        if (bytesScanned < 0)
            throw new ArgumentOutOfRangeException(nameof(bytesScanned));

        OccurrenceCount = occurrenceCount;
        MatchingLineCount = matchingLineCount;
        BytesScanned = bytesScanned;
    }

    public long OccurrenceCount { get; private set; }

    public long MatchingLineCount { get; private set; }

    public long BytesScanned { get; private set; }

    public void AddMatch(long lineNumber)
    {
        if (lineNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

        var occurrenceCount = checked(OccurrenceCount + 1);
        var matchingLineCount = MatchingLineCount;
        if (_lastMatchingLine != lineNumber)
            matchingLineCount = checked(matchingLineCount + 1);

        OccurrenceCount = occurrenceCount;
        MatchingLineCount = matchingLineCount;
        _lastMatchingLine = lineNumber;
    }

    public void AddBytes(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        BytesScanned = checked(BytesScanned + bytes);
    }
}

internal static class SearchTextScanner
{
    public static void ScanFile(
        string literal,
        string filePath,
        SearchCharBuffer buffer,
        CancellationToken cancellationToken,
        Func<string, TextReader> readerFactory,
        ISearchTextScanSink sink,
        Action? readerOpened = null,
        long maxRecordBytes = SearchResourceLimits.Unlimited,
        SearchEncodingMode encodingMode = SearchEncodingMode.Auto)
    {
        ArgumentException.ThrowIfNullOrEmpty(literal);
        ScanFile(
            new LiteralMatcher(literal),
            filePath,
            buffer,
            new List<MatchSpan>(),
            cancellationToken,
            readerFactory,
            sink,
            readerOpened,
            maxRecordBytes,
            encodingMode);
    }

    internal static void ScanFile(
        ISearchTextMatcher matcher,
        string filePath,
        SearchCharBuffer buffer,
        List<MatchSpan> spans,
        CancellationToken cancellationToken,
        Func<string, TextReader> readerFactory,
        ISearchTextScanSink sink,
        Action? readerOpened = null,
        long maxRecordBytes = SearchResourceLimits.Unlimited,
        SearchEncodingMode encodingMode = SearchEncodingMode.Auto)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(readerFactory);
        ArgumentNullException.ThrowIfNull(sink);

        matcher.Reset();
        var lineNumber = 1L;
        var utf16Column = 0L;
        spans.Clear();
        var reader = OpenReader(filePath, readerFactory);

        try
        {
            using (reader)
            {
                readerOpened?.Invoke();
                var bufferArray = buffer.Array;
                StringBuilder? lineText = sink.NeedsLineText ? new StringBuilder() : null;
                var textLineNumber = 1L;
                var coordinateReader = reader as ISearchTextCoordinateReader;
                var recordBudget = maxRecordBytes == SearchResourceLimits.Unlimited
                    ? null
                    : new SearchRecordByteBudget(maxRecordBytes, encodingMode);
                long? textLineByteOffset = coordinateReader?.InitialByteOffset;
                var hasTextOnLine = false;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var charsRead = reader.Read(bufferArray, 0, buffer.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (charsRead == 0)
                        break;

                    var coordinates = coordinateReader is not null &&
                                      coordinateReader.LastReadCoordinateCount == charsRead
                        ? coordinateReader.LastReadCoordinates.AsSpan(0, charsRead)
                        : ReadOnlySpan<SearchCharCoordinate>.Empty;

                    recordBudget?.Observe(
                        bufferArray.AsSpan(0, charsRead),
                        coordinates);

                    spans.Clear();
                    matcher.ConsumeBlock(
                        bufferArray.AsSpan(0, charsRead),
                        ref lineNumber,
                        ref utf16Column,
                        spans,
                        coordinates,
                        cancellationToken);

                    if (spans.Count > 0 &&
                        sink.AcceptMatches(spans, cancellationToken))
                        return;

                    if (sink.NeedsLineCompletion)
                    {
                        ConsumeLineText(
                            bufferArray.AsSpan(0, charsRead),
                            coordinates,
                            lineText,
                            ref textLineNumber,
                            ref textLineByteOffset,
                            ref hasTextOnLine,
                            sink);
                    }
                }

                spans.Clear();
                matcher.Complete(spans, cancellationToken);
                if (spans.Count > 0 &&
                    sink.AcceptMatches(spans, cancellationToken))
                    return;

                if (sink.NeedsLineCompletion &&
                    (sink.HasMatchesOnLine(textLineNumber) ||
                     (sink.NeedsEveryLineCompletion && hasTextOnLine)))
                    sink.CompleteLine(
                        textLineNumber,
                        lineText?.ToString(),
                        textLineByteOffset);
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
        catch (Exception exception)
        {
            throw new SearchSourceReadException(
                SearchDiagnosticCatalog.SourceReadFailed(filePath),
                exception);
        }
    }

    private static void ConsumeLineText(
        ReadOnlySpan<char> block,
        ReadOnlySpan<SearchCharCoordinate> coordinates,
        StringBuilder? lineText,
        ref long lineNumber,
        ref long? lineByteOffset,
        ref bool hasTextOnLine,
        ISearchTextScanSink sink)
    {
        var hasCoordinates = coordinates.Length == block.Length;
        var blockOffset = 0;
        while (blockOffset < block.Length)
        {
            hasTextOnLine = true;
            var relativeNewline = block[blockOffset..].IndexOf('\n');
            if (relativeNewline < 0)
            {
                AppendLineText(lineText, block[blockOffset..], sink.MaxLineTextLength);
                return;
            }

            var lengthThroughNewline = relativeNewline + 1;
            AppendLineText(
                lineText,
                block.Slice(blockOffset, lengthThroughNewline),
                sink.MaxLineTextLength);
            if (sink.HasMatchesOnLine(lineNumber) || sink.NeedsEveryLineCompletion)
            {
                sink.CompleteLine(
                    lineNumber,
                    lineText?.ToString(),
                    lineByteOffset);
            }

            lineText?.Clear();
            hasTextOnLine = false;
            lineNumber = checked(lineNumber + 1);
            lineByteOffset = hasCoordinates && TryGetByteEnd(
                coordinates[blockOffset + relativeNewline],
                out var nextLineByteOffset)
                ? nextLineByteOffset
                : null;
            blockOffset += lengthThroughNewline;
        }
    }

    private static void AppendLineText(
        StringBuilder? lineText,
        ReadOnlySpan<char> value,
        int maxLength)
    {
        if (lineText is null || maxLength <= lineText.Length || value.Length == 0)
            return;

        var length = Math.Min(value.Length, maxLength - lineText.Length);
        lineText.Append(value[..length]);
    }

    private static bool TryGetByteEnd(
        SearchCharCoordinate coordinate,
        out long byteEnd)
    {
        byteEnd = 0;
        if (!coordinate.IsMapped)
            return false;

        byteEnd = coordinate.ByteEndExclusive;
        return true;
    }

    internal static TextReader OpenReader(
        string filePath,
        Func<string, TextReader> readerFactory)
    {
        try
        {
            var reader = readerFactory(filePath);
            return reader ?? throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(filePath),
                new InvalidOperationException("The Search reader factory returned null."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(filePath),
                exception);
        }
    }
}
