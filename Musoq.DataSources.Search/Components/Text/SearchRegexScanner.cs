#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;

namespace Musoq.DataSources.Search.Components.Text;

internal static class SearchRegexScanner
{
    internal const int MaxRecordLength = 1_048_576;
    internal const int MaxMultilineRecordBytes = 1_048_576;

    public static void ScanFile(
        Regex regex,
        string filePath,
        SearchCharBuffer buffer,
        bool wholeWord,
        SearchEncodingMode encodingMode,
        SearchRecordMode recordMode,
        int maxRecordBytes,
        CancellationToken cancellationToken,
        Func<string, TextReader> readerFactory,
        ISearchTextScanSink sink,
        SearchRecordFraming? recordFraming = null,
        Action? readerOpened = null,
        long recordBudgetBytes = SearchResourceLimits.Unlimited)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(readerFactory);
        ArgumentNullException.ThrowIfNull(sink);

        if (recordFraming is not null && recordMode != SearchRecordMode.BoundedMultiline)
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("recordFraming"));

        if (recordMode == SearchRecordMode.BoundedMultiline)
        {
            ScanBoundedMultiline(
                regex,
                filePath,
                buffer,
                wholeWord,
                encodingMode,
                maxRecordBytes,
                cancellationToken,
                readerFactory,
                sink,
                recordFraming,
                readerOpened,
                recordBudgetBytes);
            return;
        }

        if (recordMode != SearchRecordMode.PhysicalLine)
            throw new ArgumentOutOfRangeException(nameof(recordMode));

        var reader = SearchTextScanner.OpenReader(filePath, readerFactory);
        try
        {
            using (reader)
            {
                readerOpened?.Invoke();
                var bufferArray = buffer.Array;
                var coordinateReader = reader as ISearchTextCoordinateReader;
                var coordinates = coordinateReader is null
                    ? null
                    : new List<SearchCharCoordinate>();
                var captureGroups = sink.NeedsCaptures
                    ? GetCaptureGroups(regex)
                    : null;
                var maxPhysicalRecordLength = MaxRecordLength;
                var physicalRecordBudget = recordBudgetBytes is SearchResourceLimits.Unlimited or >= MaxMultilineRecordBytes
                    ? null
                    : new SearchRecordByteBudget(recordBudgetBytes, encodingMode);
                var line = new StringBuilder();
                var lineNumber = 1L;
                var lineStart = 0L;
                long? lineByteOffset = coordinateReader?.InitialByteOffset;
                var matchSpans = new List<MatchSpan>();

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var charsRead = reader.Read(bufferArray, 0, buffer.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (charsRead == 0)
                        break;

                    var hasCoordinates = coordinateReader is not null &&
                                         coordinateReader.LastReadCoordinateCount == charsRead;
                    var readCoordinates = hasCoordinates
                        ? coordinateReader!.LastReadCoordinates.AsSpan(0, charsRead)
                        : ReadOnlySpan<SearchCharCoordinate>.Empty;

                    physicalRecordBudget?.Observe(
                        bufferArray.AsSpan(0, charsRead),
                        readCoordinates);

                    for (var index = 0; index < charsRead; index++)
                    {
                        if ((index & 255) == 0)
                            cancellationToken.ThrowIfCancellationRequested();

                        var character = bufferArray[index];
                        if (character == '\n')
                        {
                            line.Append(character);
                            coordinates?.Add(hasCoordinates
                                ? readCoordinates[index]
                                : default);

                            if (ProcessRecord(
                                    regex,
                                    line,
                                    coordinates,
                                    lineNumber,
                                    lineStart,
                                    lineByteOffset,
                                    wholeWord,
                                    captureGroups,
                                    terminated: true,
                                    cancellationToken,
                                    sink,
                                    matchSpans))
                            {
                                return;
                            }

                            var lineLength = line.Length;
                            var newlineCoordinate = coordinates is null
                                ? default
                                : coordinates[^1];
                            line.Clear();
                            coordinates?.Clear();
                            lineStart = checked(lineStart + lineLength);
                            lineNumber = checked(lineNumber + 1);
                            lineByteOffset = TryGetByteEnd(
                                newlineCoordinate,
                                out var nextLineByteOffset)
                                ? nextLineByteOffset
                                : null;
                            continue;
                        }

                        if (line.Length >= maxPhysicalRecordLength)
                        {
                            throw new SearchResourceLimitException(
                                SearchDiagnosticCatalog.RegexRecordTooLong(
                                    maxPhysicalRecordLength));
                        }

                        line.Append(character);
                        coordinates?.Add(hasCoordinates
                            ? readCoordinates[index]
                            : default);
                    }
                }

                if (line.Length > 0)
                {
                    _ = ProcessRecord(
                        regex,
                        line,
                        coordinates,
                        lineNumber,
                        lineStart,
                        lineByteOffset,
                        wholeWord,
                        captureGroups,
                        terminated: false,
                        cancellationToken,
                        sink,
                        matchSpans);
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
        catch (Exception exception)
        {
            throw new SearchSourceReadException(
                SearchDiagnosticCatalog.SourceReadFailed(filePath),
                exception);
        }
    }

    private static void ScanBoundedMultiline(
        Regex regex,
        string filePath,
        SearchCharBuffer buffer,
        bool wholeWord,
        SearchEncodingMode encodingMode,
        int maxRecordBytes,
        CancellationToken cancellationToken,
        Func<string, TextReader> readerFactory,
        ISearchTextScanSink sink,
        SearchRecordFraming? recordFraming,
        Action? readerOpened,
        long recordBudgetBytes)
    {
        if (maxRecordBytes <= 0 || maxRecordBytes > MaxMultilineRecordBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "recordBytes",
                    MaxMultilineRecordBytes));
        }

        if (recordFraming is not null)
        {
            ScanFramedMultiline(
                regex,
                filePath,
                buffer,
                wholeWord,
                encodingMode,
                recordFraming,
                maxRecordBytes,
                cancellationToken,
                readerFactory,
                sink,
                readerOpened,
                recordBudgetBytes);
            return;
        }

        var reader = SearchTextScanner.OpenReader(filePath, readerFactory);
        try
        {
            using (reader)
            {
                readerOpened?.Invoke();
                var bufferArray = buffer.Array;
                var coordinateReader = reader as ISearchTextCoordinateReader;
                var coordinates = coordinateReader is null
                    ? null
                    : new List<SearchCharCoordinate>();
                var unmappedEncoder = coordinateReader is null
                    ? CreateUnmappedEncoder(encodingMode)
                    : null;
                var record = new StringBuilder();
                var recordByteCount = 0L;
                long? recordByteOffset = coordinateReader?.InitialByteOffset;
                var matchSpans = new List<MatchSpan>();

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var charsRead = reader.Read(bufferArray, 0, buffer.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (charsRead == 0)
                        break;

                    var hasCoordinates = coordinateReader is not null &&
                                         coordinateReader.LastReadCoordinateCount == charsRead;
                    var readCoordinates = hasCoordinates
                        ? coordinateReader!.LastReadCoordinates.AsSpan(0, charsRead)
                        : ReadOnlySpan<SearchCharCoordinate>.Empty;

                    if (hasCoordinates && HasMappedCoordinates(readCoordinates))
                    {
                        var byteStart = recordByteOffset ?? 0;
                        recordByteCount = checked(
                            readCoordinates[^1].ByteEndExclusive - byteStart);
                    }
                    else if (unmappedEncoder is not null)
                    {
                        recordByteCount = checked(
                            recordByteCount +
                            unmappedEncoder.GetByteCount(
                                bufferArray,
                                0,
                                charsRead,
                                flush: false));
                    }
                    else
                    {
                        recordByteCount = Math.Max(
                            recordByteCount,
                            checked((record.Length + (long)charsRead) * sizeof(char)));
                    }

                    if (recordByteCount > maxRecordBytes)
                    {
                        throw new SearchResourceLimitException(
                            SearchDiagnosticCatalog.RegexMultilineRecordTooLong(maxRecordBytes));
                    }

                    record.Append(bufferArray, 0, charsRead);
                    if (coordinates is not null)
                    {
                        for (var index = 0; index < charsRead; index++)
                        {
                            coordinates.Add(hasCoordinates
                                ? readCoordinates[index]
                                : default);
                        }
                    }
                }

                if (unmappedEncoder is not null)
                {
                    recordByteCount = checked(
                        recordByteCount +
                        unmappedEncoder.GetByteCount(
                            Array.Empty<char>(),
                            0,
                            0,
                            flush: true));
                    if (recordByteCount > maxRecordBytes)
                    {
                        throw new SearchResourceLimitException(
                            SearchDiagnosticCatalog.RegexMultilineRecordTooLong(maxRecordBytes));
                    }
                }

                if (record.Length > 0)
                {
                    _ = ProcessBoundedMultilineRecord(
                        regex,
                        record.ToString(),
                        coordinates,
                        recordByteOffset,
                        wholeWord,
                        cancellationToken,
                        sink,
                        matchSpans,
                        sourceStart: 0,
                        firstLineNumber: 1);
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
        catch (Exception exception)
        {
            throw new SearchSourceReadException(
                SearchDiagnosticCatalog.SourceReadFailed(filePath),
                exception);
        }
    }

    private static void ScanFramedMultiline(
        Regex regex,
        string filePath,
        SearchCharBuffer buffer,
        bool wholeWord,
        SearchEncodingMode encodingMode,
        SearchRecordFraming framing,
        int maxRecordBytes,
        CancellationToken cancellationToken,
        Func<string, TextReader> readerFactory,
        ISearchTextScanSink sink,
        Action? readerOpened,
        long recordBudgetBytes)
    {
        var reader = SearchTextScanner.OpenReader(filePath, readerFactory);
        try
        {
            using (reader)
            {
                readerOpened?.Invoke();
                var coordinateReader = reader as ISearchTextCoordinateReader;
                var matchSpans = new List<MatchSpan>();
                var framer = new SearchRecordFramer(
                    filePath,
                    framing,
                    maxRecordBytes,
                    encodingMode,
                    coordinateReader is not null,
                    cancellationToken,
                    frame =>
                    {
                        return ProcessBoundedMultilineRecord(
                            regex,
                            frame.Content,
                            frame.Coordinates as List<SearchCharCoordinate>,
                            frame.StartByteOffset,
                            wholeWord,
                            cancellationToken,
                            sink,
                            matchSpans,
                            frame.Start,
                            frame.FirstLineNumber);
                    });
                var bufferArray = buffer.Array;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var charsRead = reader.Read(bufferArray, 0, buffer.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (charsRead == 0)
                        break;

                    var hasCoordinates = coordinateReader is not null &&
                                         coordinateReader.LastReadCoordinateCount == charsRead;
                    var readCoordinates = hasCoordinates
                        ? coordinateReader!.LastReadCoordinates.AsSpan(0, charsRead)
                        : ReadOnlySpan<SearchCharCoordinate>.Empty;
                    for (var index = 0; index < charsRead; index++)
                    {
                        if ((index & 255) == 0)
                            cancellationToken.ThrowIfCancellationRequested();

                        if (framer.Append(
                            bufferArray[index],
                            hasCoordinates ? readCoordinates[index] : default,
                            hasCoordinates))
                        {
                            return;
                        }
                    }
                }

                framer.Complete();
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

    private static bool ProcessBoundedMultilineRecord(
        Regex regex,
        string record,
        List<SearchCharCoordinate>? coordinates,
        long? recordByteOffset,
        bool wholeWord,
        CancellationToken cancellationToken,
        ISearchTextScanSink sink,
        List<MatchSpan> matchSpans,
        long sourceStart,
        long firstLineNumber)
    {
        ArgumentNullException.ThrowIfNull(matchSpans);
        var lineStarts = GetLineStarts(record);
        var captureGroups = sink.NeedsCaptures
            ? GetCaptureGroups(regex)
            : null;
        matchSpans.Clear();
        try
        {
            foreach (Match match in regex.Matches(record))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (wholeWord && !HasWholeWordBoundaries(record, match.Index, match.Length))
                    continue;

                var (relativeLineNumber, utf16Column) = GetLinePosition(lineStarts, match.Index);
                var lineNumber = checked(
                    firstLineNumber + relativeLineNumber - 1);
                var span = new MatchSpan(
                    checked(sourceStart + match.Index),
                    match.Length,
                    lineNumber,
                    utf16Column,
                    null,
                    null)
                {
                    MatchText = sink.NeedsMatchText ? match.Value : null
                };
                if (captureGroups is not null)
                {
                    span = span with
                    {
                        Captures = CreateCaptures(
                            captureGroups,
                            match,
                            coordinates,
                            record.Length,
                            recordByteOffset)
                    };
                }
                if (TryCreateByteRange(
                        coordinates,
                        record.Length,
                        match.Index,
                        match.Length,
                        recordByteOffset,
                        out var byteOffset,
                        out var byteLength))
                {
                    span = span with
                    {
                        ByteOffset = byteOffset,
                        ByteLength = byteLength
                    };
                }

                if (sink.RequiresImmediateMatchDelivery)
                {
                    if (sink.AcceptMatch(span))
                        return true;
                }
                else
                {
                    matchSpans.Add(span);
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.RegexMatchTimedOut(
                    checked((int)regex.MatchTimeout.TotalMilliseconds)),
                exception);
        }

        if (matchSpans.Count > 0 &&
            sink.AcceptMatches(matchSpans, cancellationToken))
            return true;

        if (sink.NeedsLineCompletion)
            CompleteMultilineLines(
                record,
                lineStarts,
                coordinates,
                recordByteOffset,
                sink,
                firstLineNumber);

        return false;
    }

    private static Encoder CreateUnmappedEncoder(SearchEncodingMode encodingMode)
    {
        return encodingMode switch
        {
            SearchEncodingMode.Auto or
                SearchEncodingMode.Utf8 or
                SearchEncodingMode.Utf8Bom => new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true).GetEncoder(),
            SearchEncodingMode.Utf16LittleEndianBom => new UnicodeEncoding(
                bigEndian: false,
                byteOrderMark: false,
                throwOnInvalidBytes: true).GetEncoder(),
            SearchEncodingMode.Utf16BigEndianBom => new UnicodeEncoding(
                bigEndian: true,
                byteOrderMark: false,
                throwOnInvalidBytes: true).GetEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(encodingMode))
        };
    }

    private static bool HasMappedCoordinates(ReadOnlySpan<SearchCharCoordinate> coordinates)
    {
        foreach (var coordinate in coordinates)
        {
            if (!coordinate.IsMapped)
                return false;
        }

        return true;
    }

    private static List<int> GetLineStarts(string record)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < record.Length - 1; index++)
        {
            if (record[index] == '\n')
                starts.Add(index + 1);
        }

        return starts;
    }

    private static (long LineNumber, long Utf16Column) GetLinePosition(
        List<int> lineStarts,
        int position)
    {
        var lineIndex = lineStarts.BinarySearch(position);
        if (lineIndex < 0)
            lineIndex = ~lineIndex - 1;

        return (
            checked(lineIndex + 1L),
            checked(position - lineStarts[lineIndex]));
    }

    private static void CompleteMultilineLines(
        string record,
        List<int> lineStarts,
        List<SearchCharCoordinate>? coordinates,
        long? recordByteOffset,
        ISearchTextScanSink sink,
        long firstLineNumber)
    {
        for (var index = 0; index < lineStarts.Count; index++)
        {
            var lineNumber = checked(firstLineNumber + index);
            if (!sink.HasMatchesOnLine(lineNumber) && !sink.NeedsEveryLineCompletion)
                continue;

            var lineStart = lineStarts[index];
            var lineEnd = index + 1 < lineStarts.Count
                ? lineStarts[index + 1]
                : record.Length;
            var byteOffset = TryGetLineByteOffset(
                lineStart,
                coordinates,
                recordByteOffset);
            var lineText = sink.NeedsLineText
                ? GetLineText(
                    record,
                    lineStart,
                    lineEnd - lineStart,
                    sink.MaxLineTextLength)
                : null;
            sink.CompleteLine(lineNumber, lineText, byteOffset);
        }
    }

    private static long? TryGetLineByteOffset(
        int lineStart,
        List<SearchCharCoordinate>? coordinates,
        long? recordByteOffset)
    {
        if (lineStart == 0)
            return recordByteOffset;

        var newlineIndex = lineStart - 1;
        if (coordinates is null || newlineIndex >= coordinates.Count)
            return null;

        var coordinate = coordinates[newlineIndex];
        return coordinate.IsMapped && coordinate.CanEnd
            ? coordinate.ByteEndExclusive
            : null;
    }

    private static bool ProcessRecord(
        Regex regex,
        StringBuilder line,
        List<SearchCharCoordinate>? coordinates,
        long lineNumber,
        long lineStart,
        long? lineByteOffset,
        bool wholeWord,
        CaptureGroup[]? captureGroups,
        bool terminated,
        CancellationToken cancellationToken,
        ISearchTextScanSink sink,
        List<MatchSpan> matchSpans)
    {
        var recordLength = terminated ? line.Length - 1 : line.Length;
        if (terminated && recordLength > 0 && line[recordLength - 1] == '\r')
            recordLength--;

        var record = line.ToString(0, recordLength);
        matchSpans.Clear();
        try
        {
            foreach (Match match in regex.Matches(record))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (wholeWord && !HasWholeWordBoundaries(record, match.Index, match.Length))
                    continue;

                var span = new MatchSpan(
                    checked(lineStart + match.Index),
                    match.Length,
                    lineNumber,
                    match.Index,
                    null,
                    null)
                {
                    MatchText = sink.NeedsMatchText ? match.Value : null
                };
                if (captureGroups is not null)
                {
                    span = span with
                    {
                        Captures = CreateCaptures(
                            captureGroups,
                            match,
                            coordinates,
                            recordLength,
                            lineByteOffset)
                    };
                }
                if (TryCreateByteRange(
                        coordinates,
                        recordLength,
                        match.Index,
                        match.Length,
                        lineByteOffset,
                        out var byteOffset,
                        out var byteLength))
                {
                    span = span with
                    {
                        ByteOffset = byteOffset,
                        ByteLength = byteLength
                    };
                }

                if (sink.RequiresImmediateMatchDelivery)
                {
                    if (sink.AcceptMatch(span))
                        return true;
                }
                else
                {
                    matchSpans.Add(span);
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.RegexMatchTimedOut(
                    checked((int)regex.MatchTimeout.TotalMilliseconds)),
                exception);
        }

        if (matchSpans.Count > 0 &&
            sink.AcceptMatches(matchSpans, cancellationToken))
            return true;

        if (sink.NeedsLineCompletion &&
            (sink.HasMatchesOnLine(lineNumber) || sink.NeedsEveryLineCompletion))
        {
            sink.CompleteLine(
                lineNumber,
                sink.NeedsLineText
                    ? GetLineText(line, sink.MaxLineTextLength)
                    : null,
                lineByteOffset);
        }

        return false;
    }

    private static string? GetLineText(
        string record,
        int start,
        int length,
        int maxLength)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (length == 0)
            return string.Empty;
        if (maxLength == 0)
            return null;

        return record.Substring(start, Math.Min(length, maxLength));
    }

    private static string? GetLineText(StringBuilder line, int maxLength)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (line.Length == 0)
            return string.Empty;
        if (maxLength == 0)
            return null;

        return line.ToString(0, Math.Min(line.Length, maxLength));
    }

    private static CaptureGroup[] GetCaptureGroups(Regex regex)
    {
        var groupNumbers = regex.GetGroupNumbers();
        if (groupNumbers.Length <= 1)
            return [];

        var groups = new CaptureGroup[groupNumbers.Length - 1];
        for (var index = 1; index < groupNumbers.Length; index++)
        {
            var groupIndex = groupNumbers[index];
            var groupName = regex.GroupNameFromNumber(groupIndex);
            if (string.Equals(
                    groupName,
                    groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                groupName = null;
            }

            groups[index - 1] = new CaptureGroup(groupIndex, groupName);
        }

        return groups;
    }

    private static IReadOnlyList<SearchCapture> CreateCaptures(
        CaptureGroup[] captureGroups,
        Match match,
        List<SearchCharCoordinate>? coordinates,
        int recordLength,
        long? lineByteOffset)
    {
        var captures = new List<SearchCapture>();
        foreach (var captureGroup in captureGroups)
        {
            var group = match.Groups[captureGroup.GroupIndex];
            if (group.Captures.Count == 0)
            {
                captures.Add(new SearchCapture(
                    captureGroup.GroupName,
                    captureGroup.GroupIndex,
                    captureIndex: 0,
                    success: false,
                    text: null,
                    byteOffset: null,
                    byteLength: null,
                    utf16Column: null,
                    utf16Length: null));
                continue;
            }

            // The selected non-backtracking profile retains the final capture
            // for a repeated group, not the backtracking engine's capture
            // history. Keep that limitation explicit and stable here.
            var capture = group.Captures[^1];
            TryCreateByteRange(
                coordinates,
                recordLength,
                capture.Index,
                capture.Length,
                lineByteOffset,
                out var byteOffset,
                out var byteLength);
            captures.Add(new SearchCapture(
                captureGroup.GroupName,
                captureGroup.GroupIndex,
                captureIndex: 0,
                success: true,
                text: capture.Value,
                byteOffset,
                byteLength,
                utf16Column: capture.Index,
                utf16Length: capture.Length));
        }

        return captures.Count == 0
            ? Array.Empty<SearchCapture>()
            : Array.AsReadOnly(captures.ToArray());
    }

    private static bool HasWholeWordBoundaries(
        string record,
        int start,
        int length)
    {
        if (length == 0)
            return false;

        var end = checked(start + length);
        if (start < 0 || end > record.Length ||
            (start < record.Length && char.IsLowSurrogate(record[start])) ||
            (end < record.Length && char.IsLowSurrogate(record[end])))
        {
            return false;
        }

        if (!TryGetWordAt(record, start, out var firstIsWord) ||
            !TryGetWordAt(record, end - 1, out var lastIsWord))
        {
            return false;
        }

        if (start > 0 &&
            (!TryGetWordAt(record, start - 1, out var previousIsWord) ||
             previousIsWord == firstIsWord))
        {
            return false;
        }

        if (end < record.Length &&
            (!TryGetWordAt(record, end, out var nextIsWord) ||
             nextIsWord == lastIsWord))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetWordAt(
        string record,
        int position,
        out bool isWord)
    {
        isWord = false;
        if (position < 0 || position >= record.Length)
            return false;

        var current = record[position];
        if (char.IsHighSurrogate(current))
        {
            if (position + 1 >= record.Length ||
                !char.IsLowSurrogate(record[position + 1]))
            {
                return false;
            }

            isWord = SearchWordPolicy.IsWordScalar(current, record[position + 1]);
            return true;
        }

        if (char.IsLowSurrogate(current))
        {
            if (position == 0 || !char.IsHighSurrogate(record[position - 1]))
                return false;

            isWord = SearchWordPolicy.IsWordScalar(record[position - 1], current);
            return true;
        }

        isWord = SearchWordPolicy.IsWordScalar(current);
        return true;
    }

    private static bool TryCreateByteRange(
        List<SearchCharCoordinate>? coordinates,
        int recordLength,
        int start,
        int length,
        long? lineByteOffset,
        out long? byteOffset,
        out long? byteLength)
    {
        byteOffset = null;
        byteLength = null;
        if (coordinates is null || coordinates.Count < recordLength ||
            start < 0 || length < 0 || start > recordLength - length)
        {
            return false;
        }

        if (length == 0)
        {
            if (start == 0 && lineByteOffset.HasValue)
            {
                byteOffset = lineByteOffset;
                byteLength = 0;
                return true;
            }

            if (start == 0 || !coordinates[start - 1].IsMapped ||
                !coordinates[start - 1].CanEnd)
            {
                return false;
            }

            var boundary = coordinates[start - 1].ByteEndExclusive;
            if (start < recordLength &&
                (!coordinates[start].IsMapped ||
                 !coordinates[start].CanStart ||
                 coordinates[start].ByteOffset != boundary))
            {
                return false;
            }

            byteOffset = boundary;
            byteLength = 0;
            return true;
        }

        var first = coordinates[start];
        var last = coordinates[start + length - 1];
        if (!first.IsMapped || !first.CanStart ||
            !last.IsMapped || !last.CanEnd)
        {
            return false;
        }

        var end = first.ByteEndExclusive;
        for (var index = 1; index < length; index++)
        {
            var current = coordinates[start + index];
            if (!current.IsMapped)
                return false;

            var sameScalar = current.ByteOffset == first.ByteOffset &&
                              current.ByteEndExclusive == first.ByteEndExclusive;
            if (current.ByteOffset != end && !sameScalar)
                return false;

            end = Math.Max(end, current.ByteEndExclusive);
        }

        byteOffset = first.ByteOffset;
        byteLength = checked(end - first.ByteOffset);
        return true;
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

    private readonly record struct CaptureGroup(int GroupIndex, string? GroupName);
}
