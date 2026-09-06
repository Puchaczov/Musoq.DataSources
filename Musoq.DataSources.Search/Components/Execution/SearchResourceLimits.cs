#nullable enable

using System;
using System.Text;
using System.Threading;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Execution;

/// <summary>
///     Per-request resource limits for the Search execution seam.
/// </summary>
internal sealed record SearchResourceLimits
{
    public const long Unlimited = long.MaxValue;

    private const long DefaultPatternBytes = 8L * 1024 * 1024;

    public static SearchResourceLimits Default { get; } = new();

    public SearchResourceLimits(
        long maxTotalBytes = Unlimited,
        long maxFileBytes = Unlimited,
        long maxFiles = Unlimited,
        int maxPatternCount = SearchManyRequestParser.MaxPatternCount,
        int maxPatternLength = SearchRegexBackend.MaxPatternLength,
        long maxPatternBytes = DefaultPatternBytes,
        int maxPatternCompilationMilliseconds = 5_000,
        long maxRecordBytes = SearchRegexScanner.MaxMultilineRecordBytes,
        long maxContextBytes = SearchContextOptions.MaxContextBytes,
        long maxInFlightOutputBytes = Unlimited,
        long maxMatchCount = Unlimited)
    {
        ValidateNonNegative(maxTotalBytes, nameof(maxTotalBytes));
        ValidateNonNegative(maxFileBytes, nameof(maxFileBytes));
        ValidateNonNegative(maxFiles, nameof(maxFiles));
        ValidateBounded(
            maxPatternCount,
            1,
            SearchManyRequestParser.MaxPatternCount,
            nameof(maxPatternCount));
        ValidateBounded(
            maxPatternLength,
            1,
            SearchRegexBackend.MaxPatternLength,
            nameof(maxPatternLength));
        ValidateNonNegative(maxPatternBytes, nameof(maxPatternBytes));
        ValidateBounded(
            maxPatternCompilationMilliseconds,
            0,
            5_000,
            nameof(maxPatternCompilationMilliseconds));
        ValidateBounded(
            maxRecordBytes,
            1,
            SearchRegexScanner.MaxMultilineRecordBytes,
            nameof(maxRecordBytes));
        ValidateBounded(
            maxContextBytes,
            0,
            SearchContextOptions.MaxContextBytes,
            nameof(maxContextBytes));
        ValidateNonNegative(maxInFlightOutputBytes, nameof(maxInFlightOutputBytes));
        ValidateNonNegative(maxMatchCount, nameof(maxMatchCount));

        MaxTotalBytes = maxTotalBytes;
        MaxFileBytes = maxFileBytes;
        MaxFiles = maxFiles;
        MaxPatternCount = maxPatternCount;
        MaxPatternLength = maxPatternLength;
        MaxPatternBytes = maxPatternBytes;
        MaxPatternCompilationMilliseconds = maxPatternCompilationMilliseconds;
        MaxRecordBytes = maxRecordBytes;
        MaxContextBytes = maxContextBytes;
        MaxInFlightOutputBytes = maxInFlightOutputBytes;
        MaxMatchCount = maxMatchCount;
    }

    public long MaxTotalBytes { get; }

    public long MaxFileBytes { get; }

    public long MaxFiles { get; }

    public int MaxPatternCount { get; }

    public int MaxPatternLength { get; }

    public long MaxPatternBytes { get; }

    public int MaxPatternCompilationMilliseconds { get; }

    public long MaxRecordBytes { get; }

    public long MaxContextBytes { get; }

    public long MaxInFlightOutputBytes { get; }

    public long MaxMatchCount { get; }

    public void ValidatePattern(string pattern, int patternCount = 1)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (patternCount < 1 || patternCount > MaxPatternCount)
        {
            throw Exhausted(
                "pattern-count",
                MaxPatternCount,
                "pattern-count");
        }

        if (pattern.Length == 0 || pattern.Length > MaxPatternLength)
        {
            throw Exhausted(
                "pattern-size",
                MaxPatternLength,
                "pattern-size");
        }

        var bytes = Utf8.GetByteCount(pattern);
        if (bytes > MaxPatternBytes)
        {
            throw Exhausted(
                "pattern-size",
                MaxPatternBytes,
                "pattern-size");
        }
    }

    public void ValidatePatternSet(
        System.Collections.Generic.IReadOnlyList<SearchManyPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count == 0 || patterns.Count > MaxPatternCount)
        {
            throw Exhausted(
                "pattern-count",
                MaxPatternCount,
                "pattern-count");
        }

        long totalBytes = 0;
        for (var index = 0; index < patterns.Count; index++)
        {
            var pattern = patterns[index] ??
                          throw new ArgumentException(
                              "A Search pattern cannot be null.",
                              nameof(patterns));
            ValidatePattern(pattern.Pattern, patterns.Count);
            totalBytes = checked(totalBytes + Utf8.GetByteCount(pattern.Pattern));
            if (totalBytes > MaxPatternBytes)
            {
                throw Exhausted(
                    "pattern-size",
                    MaxPatternBytes,
                    "pattern-size");
            }
        }
    }

    public SearchResourceLimitException Exhausted(
        string diagnosticName,
        long limit,
        string budgetCode)
    {
        return new SearchResourceLimitException(
            SearchDiagnosticCatalog.ResourceLimit(diagnosticName, limit),
            budgetCode: budgetCode);
    }

    private static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static void ValidateNonNegative(long value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateBounded(
        long value,
        long minimum,
        long maximum,
        string parameterName)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}

/// <summary>
///     Shared accounting for one Search execution.
/// </summary>
internal sealed class SearchResourceBudget
{
    private readonly SearchResourceLimits _limits;
    private long _reservedBytes;
    private long _reservedFiles;
    private long _observedMatches;
    private readonly SearchOutputStagingBudget? _outputBudget;

    public SearchResourceBudget(SearchResourceLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _outputBudget = _limits.MaxInFlightOutputBytes == SearchResourceLimits.Unlimited
            ? null
            : new SearchOutputStagingBudget(_limits.MaxInFlightOutputBytes);
    }

    public SearchResourceLimits Limits => _limits;

    public SearchOutputStagingBudget? OutputBudget => _outputBudget;

    public SearchFileBudget BeginFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (_limits.MaxFiles != SearchResourceLimits.Unlimited)
        {
            Reserve(
                ref _reservedFiles,
                1,
                _limits.MaxFiles,
                "files",
                "file-count");
        }
        return new SearchFileBudget(this, path);
    }

    public void ObserveMatch()
    {
        if (_limits.MaxMatchCount == SearchResourceLimits.Unlimited)
            return;

        Reserve(
            ref _observedMatches,
            1,
            _limits.MaxMatchCount,
            "match-count",
            "match-count");
    }

    internal void ReserveFileBytes(SearchFileBudget file, long bytes)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        if (_limits.MaxFileBytes == SearchResourceLimits.Unlimited &&
            _limits.MaxTotalBytes == SearchResourceLimits.Unlimited)
        {
            file.SetReservedBytes(bytes);
            return;
        }

        if (bytes > _limits.MaxFileBytes)
        {
            throw _limits.Exhausted(
                "file-bytes",
                _limits.MaxFileBytes,
                "file-bytes");
        }

        Reserve(
            ref _reservedBytes,
            bytes,
            _limits.MaxTotalBytes,
            "read-bytes",
            "read-bytes");
        file.SetReservedBytes(bytes);
    }

    private static void Reserve(
        ref long current,
        long amount,
        long limit,
        string diagnosticName,
        string budgetCode)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        while (true)
        {
            var observed = Interlocked.Read(ref current);
            if (amount > limit - observed)
            {
                throw new SearchResourceLimitException(
                    SearchDiagnosticCatalog.ResourceLimit(diagnosticName, limit),
                    budgetCode: budgetCode);
            }

            var next = checked(observed + amount);
            if (Interlocked.CompareExchange(ref current, next, observed) == observed)
                return;
        }
    }
}

internal sealed class SearchFileBudget
{
    private readonly SearchResourceBudget _owner;

    internal SearchFileBudget(SearchResourceBudget owner, string path)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public string Path { get; }

    public long ReservedBytes { get; private set; }

    internal void ReserveBytes(long bytes)
    {
        _owner.ReserveFileBytes(this, bytes);
    }

    internal void SetReservedBytes(long bytes)
    {
        ReservedBytes = bytes;
    }
}

internal sealed class SearchOutputStagingBudget
{
    private readonly long _limit;
    private long _reservedBytes;

    public SearchOutputStagingBudget(long limit)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit));

        _limit = limit;
    }

    public void Reserve(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        while (true)
        {
            var observed = Interlocked.Read(ref _reservedBytes);
            if (bytes > _limit - observed)
            {
                throw new SearchResourceLimitException(
                    SearchDiagnosticCatalog.ResourceLimit("output-cap", _limit),
                    budgetCode: "output-cap");
            }

            var next = checked(observed + bytes);
            if (Interlocked.CompareExchange(ref _reservedBytes, next, observed) == observed)
                return;
        }
    }

    public void Release(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        if (bytes == 0)
            return;

        while (true)
        {
            var observed = Interlocked.Read(ref _reservedBytes);
            if (bytes > observed)
                throw new InvalidOperationException(
                    "The Search output budget release exceeded its reservation.");

            var next = observed - bytes;
            if (Interlocked.CompareExchange(ref _reservedBytes, next, observed) == observed)
                return;
        }
    }
}

internal static class SearchRowSizeEstimator
{
    private static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    public static long Estimate(SearchMatch row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var bytes = 128L;
        bytes = Add(bytes, row.Path);
        bytes = Add(bytes, row.PatternId);
        bytes = Add(bytes, row.MatchText);
        foreach (var capture in row.Captures)
        {
            bytes = checked(bytes + 64);
            bytes = Add(bytes, capture.GroupName);
            bytes = Add(bytes, capture.Text);
        }

        foreach (var context in row.Context)
        {
            bytes = checked(bytes + 48);
            bytes = Add(bytes, context.LineText);
        }

        return bytes;
    }

    public static long Estimate(SearchByteMatch row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var bytes = 96L;
        bytes = Add(bytes, row.Path);
        var matchedBytes = row.MatchedBytes;
        if (matchedBytes is not null)
            bytes = checked(bytes + matchedBytes.LongLength);

        var windowBytes = row.WindowBytes;
        if (windowBytes is not null)
            bytes = checked(bytes + windowBytes.LongLength);

        return bytes;
    }

    public static long EstimatePotentialMatch(
        string path,
        string? matchText,
        SearchContextOptions context,
        System.Collections.Generic.IReadOnlyList<SearchCapture>? captures = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(context);

        var bytes = 128L;
        bytes = Add(bytes, path);
        bytes = Add(bytes, matchText);
        if (captures is not null)
        {
            foreach (var capture in captures)
            {
                bytes = checked(bytes + 64);
                bytes = Add(bytes, capture.GroupName);
                bytes = Add(bytes, capture.Text);
            }
        }

        if (context.IsEnabled)
        {
            bytes = checked(bytes + context.MaxBytes);
            bytes = checked(bytes +
                            (context.BeforeLines + context.AfterLines) * 48L);
        }

        return bytes;
    }

    public static long Estimate(SearchLine row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var bytes = 96L;
        bytes = Add(bytes, row.Path);
        bytes = Add(bytes, row.Origin);
        bytes = Add(bytes, row.PatternId);
        return Add(bytes, row.LineText);
    }

    public static long Estimate(SearchFile row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var bytes = 64L;
        bytes = Add(bytes, row.Path);
        bytes = Add(bytes, row.Origin);
        return Add(bytes, row.PatternId);
    }

    public static long Estimate(SearchCount row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var bytes = 96L;
        bytes = Add(bytes, row.Path);
        bytes = Add(bytes, row.Origin);
        return Add(bytes, row.PatternId);
    }

    private static long Add(long current, string? value)
    {
        return value is null
            ? current
            : checked(current + Utf8.GetByteCount(value));
    }
}

internal sealed class SearchRecordByteBudget
{
    private readonly long _limit;
    private readonly SearchEncodingMode _encodingMode;
    private long _currentLineBytes;

    public SearchRecordByteBudget(long limit, SearchEncodingMode encodingMode)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));

        _limit = limit;
        _encodingMode = encodingMode;
    }

    public void Observe(
        ReadOnlySpan<char> characters,
        ReadOnlySpan<SearchCharCoordinate> coordinates)
    {
        var hasCoordinates = coordinates.Length == characters.Length;
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (character == '\n')
            {
                _currentLineBytes = 0;
                continue;
            }

            var byteCount = hasCoordinates && coordinates[index].IsMapped
                ? coordinates[index].CanStart
                    ? coordinates[index].ByteLength
                    : 0
                : CountFallbackBytes(characters, ref index);
            _currentLineBytes = checked(_currentLineBytes + byteCount);
            if (_currentLineBytes > _limit)
            {
                throw new SearchResourceLimitException(
                    SearchDiagnosticCatalog.ResourceLimit("record-bytes", _limit),
                    budgetCode: "record-bytes");
            }
        }
    }

    private long CountFallbackBytes(ReadOnlySpan<char> characters, ref int index)
    {
        var length = 1;
        if (char.IsHighSurrogate(characters[index]) &&
            index + 1 < characters.Length &&
            char.IsLowSurrogate(characters[index + 1]))
        {
            length = 2;
            index++;
        }

        return _encodingMode is SearchEncodingMode.Utf16LittleEndianBom or
            SearchEncodingMode.Utf16BigEndianBom
            ? checked(length * sizeof(char))
            : Encoding.UTF8.GetByteCount(characters.Slice(index - length + 1, length));
    }
}
