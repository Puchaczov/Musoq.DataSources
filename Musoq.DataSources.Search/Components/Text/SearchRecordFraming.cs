#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Text;

internal sealed record SearchRecordFraming
{
    internal const int MaxDelimiterLength = 4_096;

    public SearchRecordFraming(
        string startDelimiter,
        string endDelimiter,
        bool allowUnterminatedEof = false)
    {
        ValidateDelimiter(startDelimiter, "startDelimiter");
        ValidateDelimiter(endDelimiter, "endDelimiter");
        if (string.Equals(startDelimiter, endDelimiter, StringComparison.Ordinal))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("recordFraming"));
        }

        StartDelimiter = startDelimiter;
        EndDelimiter = endDelimiter;
        AllowUnterminatedEof = allowUnterminatedEof;
    }

    public string StartDelimiter { get; }

    public string EndDelimiter { get; }

    public bool AllowUnterminatedEof { get; }

    private static void ValidateDelimiter(string? delimiter, string argumentName)
    {
        if (string.IsNullOrEmpty(delimiter))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(argumentName));
        }

        if (delimiter.Length > MaxDelimiterLength)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    argumentName,
                    MaxDelimiterLength));
        }

        if (delimiter.Contains('\r') || delimiter.Contains('\n'))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument(argumentName));
        }
    }
}

internal sealed record SearchRecordFrame(
    long RecordIndex,
    string Origin,
    long Start,
    long EndExclusive,
    long FirstLineNumber,
    string Content,
    IReadOnlyList<SearchCharCoordinate>? Coordinates,
    long? StartByteOffset,
    long? EndByteExclusive,
    bool Terminated);

internal sealed class SearchRecordFramer
{
    private readonly string _origin;
    private readonly SearchRecordFraming _framing;
    private readonly int _maxRecordBytes;
    private readonly Encoder? _unmappedEncoder;
    private readonly bool _hasCoordinateReader;
    private readonly CancellationToken _cancellationToken;
    private readonly Func<SearchRecordFrame, bool> _recordCompleted;
    private readonly StringBuilder _line = new();
    private readonly List<SearchCharCoordinate>? _lineCoordinates;

    private StringBuilder? _record;
    private List<SearchCharCoordinate>? _recordCoordinates;
    private long _sourceOffset;
    private long _lineNumber = 1;
    private long _recordIndex;
    private long _recordStart;
    private long _recordFirstLineNumber;
    private long? _recordStartByteOffset;
    private long? _recordEndByteExclusive;
    private long _recordByteCount;
    private bool _recordHasUnmappedCoordinates;
    private bool _insideRecord;
    private bool _completed;

    public SearchRecordFramer(
        string origin,
        SearchRecordFraming framing,
        int maxRecordBytes,
        SearchEncodingMode encodingMode,
        bool hasCoordinateReader,
        CancellationToken cancellationToken,
        Func<SearchRecordFrame, bool> recordCompleted)
    {
        _origin = string.IsNullOrEmpty(origin)
            ? throw new ArgumentException("Value cannot be null or empty.", nameof(origin))
            : origin;
        _framing = framing ?? throw new ArgumentNullException(nameof(framing));
        if (maxRecordBytes <= 0 || maxRecordBytes > SearchRegexScanner.MaxMultilineRecordBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.ResourceLimit(
                    "recordBytes",
                    SearchRegexScanner.MaxMultilineRecordBytes));
        }

        _maxRecordBytes = maxRecordBytes;
        _hasCoordinateReader = hasCoordinateReader;
        _cancellationToken = cancellationToken;
        _unmappedEncoder = hasCoordinateReader
            ? null
            : CreateUnmappedEncoder(encodingMode);
        _recordCompleted = recordCompleted ?? throw new ArgumentNullException(nameof(recordCompleted));
        _lineCoordinates = hasCoordinateReader ? [] : null;
    }

    public bool Append(char character, SearchCharCoordinate coordinate, bool hasCoordinate)
    {
        if (_completed)
            throw new InvalidOperationException("The record framer has already completed.");

        if (_line.Length >= SearchRegexScanner.MaxRecordLength)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.RegexRecordTooLong(SearchRegexScanner.MaxRecordLength));
        }

        _line.Append(character);
        _lineCoordinates?.Add(hasCoordinate ? coordinate : default);
        _sourceOffset = checked(_sourceOffset + 1);

        return character == '\n' && CompleteLine(terminated: true);
    }

    public void Complete()
    {
        if (_completed)
            throw new InvalidOperationException("The record framer has already completed.");

        _completed = true;
        if (_line.Length > 0 && CompleteLine(terminated: false))
            return;

        if (!_insideRecord)
            return;

        if (!_framing.AllowUnterminatedEof)
        {
            throw new SearchRecordFramingException(
                SearchDiagnosticCatalog.InvalidRecordFraming(
                    "an open record reached end-of-file before its end delimiter"));
        }

        _ = CompleteRecord(_sourceOffset, terminated: false);
    }

    private bool CompleteLine(bool terminated)
    {
        var lineStart = checked(_sourceOffset - _line.Length);
        var lineValueLength = terminated ? _line.Length - 1 : _line.Length;
        if (terminated && lineValueLength > 0 && _line[lineValueLength - 1] == '\r')
            lineValueLength--;

        var isStartDelimiter = IsDelimiter(_framing.StartDelimiter, lineValueLength);
        var isEndDelimiter = IsDelimiter(_framing.EndDelimiter, lineValueLength);

        var stopRequested = false;
        if (!_insideRecord)
        {
            if (isEndDelimiter)
            {
                throw new SearchRecordFramingException(
                    SearchDiagnosticCatalog.InvalidRecordFraming(
                        "an end delimiter appeared without an open record"));
            }

            if (isStartDelimiter)
                BeginRecord(terminated);
        }
        else if (isStartDelimiter)
        {
            throw new SearchRecordFramingException(
                SearchDiagnosticCatalog.InvalidRecordFraming(
                    "a start delimiter appeared before the open record was closed"));
        }
        else if (isEndDelimiter)
        {
            stopRequested = CompleteRecord(lineStart, terminated: true);
        }
        else
        {
            AppendRecordLine();
        }

        _line.Clear();
        _lineCoordinates?.Clear();
        if (terminated)
            _lineNumber = checked(_lineNumber + 1);

        return stopRequested;
    }

    private bool IsDelimiter(string delimiter, int valueLength)
    {
        if (valueLength != delimiter.Length)
            return false;

        for (var index = 0; index < valueLength; index++)
        {
            if (_line[index] != delimiter[index])
                return false;
        }

        return true;
    }

    private void BeginRecord(bool startDelimiterTerminated)
    {
        _insideRecord = true;
        _record = new StringBuilder();
        _recordCoordinates = _hasCoordinateReader ? [] : null;
        _recordByteCount = 0;
        _recordHasUnmappedCoordinates = false;
        _recordStart = _sourceOffset;
        _recordFirstLineNumber = checked(_lineNumber + (startDelimiterTerminated ? 1 : 0));
        _recordStartByteOffset = TryGetLineEndByteOffset();
        _recordEndByteExclusive = _recordStartByteOffset;
    }

    private void AppendRecordLine()
    {
        var line = _record ?? throw new InvalidOperationException(
            "A record payload was observed without an open record.");
        for (var index = 0; index < _line.Length; index++)
        {
            if ((index & 255) == 0)
                _cancellationToken.ThrowIfCancellationRequested();

            var coordinate = _lineCoordinates is null
                ? default
                : _lineCoordinates[index];
            line.Append(_line[index]);
            _recordCoordinates?.Add(coordinate);
            UpdateRecordByteCount(_line[index], coordinate);
        }
    }

    private void UpdateRecordByteCount(
        char character,
        SearchCharCoordinate coordinate)
    {
        if (_hasCoordinateReader)
        {
            if (!coordinate.IsMapped)
            {
                _recordHasUnmappedCoordinates = true;
                _recordStartByteOffset = null;
                _recordEndByteExclusive = null;
                _recordByteCount = checked((long)(_record?.Length ?? 0) * sizeof(char));
            }
            else if (!_recordHasUnmappedCoordinates)
            {
                _recordStartByteOffset ??= coordinate.ByteOffset;
                _recordEndByteExclusive = coordinate.ByteEndExclusive;
                _recordByteCount = checked(
                    _recordEndByteExclusive.Value - _recordStartByteOffset!.Value);
            }
        }
        else
        {
            Span<char> value = stackalloc char[1];
            value[0] = character;
            _recordByteCount = checked(
                _recordByteCount +
                _unmappedEncoder!.GetByteCount(value, flush: false));
        }

        if (_recordByteCount > _maxRecordBytes)
        {
            throw new SearchResourceLimitException(
                SearchDiagnosticCatalog.RegexMultilineRecordTooLong(_maxRecordBytes));
        }
    }

    private bool CompleteRecord(long endExclusive, bool terminated)
    {
        var record = _record ?? throw new InvalidOperationException(
            "A record completed without an open record.");

        if (!_hasCoordinateReader)
        {
            _recordByteCount = checked(
                _recordByteCount +
                _unmappedEncoder!.GetByteCount(ReadOnlySpan<char>.Empty, flush: true));
            if (_recordByteCount > _maxRecordBytes)
            {
                throw new SearchResourceLimitException(
                    SearchDiagnosticCatalog.RegexMultilineRecordTooLong(_maxRecordBytes));
            }
        }

        var stopRequested = _recordCompleted(new SearchRecordFrame(
            _recordIndex,
            _origin,
            _recordStart,
            endExclusive,
            _recordFirstLineNumber,
            record.ToString(),
            _recordCoordinates,
            _recordStartByteOffset,
            _recordEndByteExclusive,
            terminated));
        _recordIndex = checked(_recordIndex + 1);
        _record = null;
        _recordCoordinates = null;
        _recordStartByteOffset = null;
        _recordEndByteExclusive = null;
        _recordByteCount = 0;
        _recordHasUnmappedCoordinates = false;
        _insideRecord = false;
        return stopRequested;
    }

    private long? TryGetLineEndByteOffset()
    {
        if (_lineCoordinates is null || _lineCoordinates.Count == 0)
            return null;

        var coordinate = _lineCoordinates[^1];
        return coordinate.IsMapped && coordinate.CanEnd
            ? coordinate.ByteEndExclusive
            : null;
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
}
