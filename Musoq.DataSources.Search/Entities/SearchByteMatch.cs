#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One non-overlapping raw-byte occurrence emitted by <c>search.bytes</c>.
/// </summary>
public sealed class SearchByteMatch
{
    private static readonly IReadOnlyList<SearchCapture> EmptyCaptures =
        Array.AsReadOnly(Array.Empty<SearchCapture>());
    private static readonly IReadOnlyList<SearchContextLine> EmptyContext =
        Array.AsReadOnly(Array.Empty<SearchContextLine>());
    private readonly string? _origin;
    private readonly string? _patternId;

    /// <summary>
    ///     Creates one raw-byte occurrence row.
    /// </summary>
    public SearchByteMatch(
        string path,
        long matchIndex,
        long byteOffset,
        long byteLength,
        byte[]? matchedBytes = null,
        string? origin = null,
        string? patternId = null,
        byte[]? windowBytes = null,
        long? windowStartByteOffset = null,
        long? windowByteLength = null,
        bool? windowComplete = null)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        _origin = origin;
        _patternId = patternId;
        MatchIndex = matchIndex;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        MatchedBytes = matchedBytes is null ? null : (byte[])matchedBytes.Clone();
        if (windowStartByteOffset is < 0)
            throw new ArgumentOutOfRangeException(nameof(windowStartByteOffset));
        if (windowByteLength is < 0)
            throw new ArgumentOutOfRangeException(nameof(windowByteLength));
        if (windowBytes is not null &&
            windowByteLength is not null &&
            windowBytes.LongLength != windowByteLength.Value)
        {
            throw new ArgumentException(
                "Window bytes must have the declared byte length.",
                nameof(windowBytes));
        }

        WindowBytes = windowBytes is null ? null : (byte[])windowBytes.Clone();
        WindowStartByteOffset = windowStartByteOffset;
        WindowByteLength = windowByteLength ?? windowBytes?.LongLength;
        WindowComplete = windowComplete;
    }

    /// <summary>
    ///     Gets the path relative to the requested search root.
    /// </summary>
    [EntityProperty]
    public string Path { get; }

    /// <summary>
    ///     Gets the origin for a virtual source; ordinary local files use null.
    /// </summary>
    [EntityProperty]
    public string? Origin => _origin;

    /// <summary>
    ///     Gets the optional pattern identifier. The single-pattern source uses null.
    /// </summary>
    [EntityProperty]
    public string? PatternId => _patternId;

    /// <summary>
    ///     Gets the zero-based match ordinal within the path.
    /// </summary>
    [EntityProperty]
    public long MatchIndex { get; }

    /// <summary>
    ///     Gets the original-input byte start of the match.
    /// </summary>
    [EntityProperty]
    public long ByteOffset { get; }

    /// <summary>
    ///     Gets the original-input byte length of the match.
    /// </summary>
    [EntityProperty]
    public long ByteLength { get; }

    /// <summary>
    ///     Gets a copy of the matched input bytes when requested by the projection.
    /// </summary>
    [EntityProperty]
    [BindablePropertyAsTable]
    public byte[]? MatchedBytes => field is null ? null : (byte[])field.Clone();

    /// <summary>
    ///     Gets the actual start of the requested bounded window, or null when no window was requested.
    /// </summary>
    [EntityProperty]
    public long? WindowStartByteOffset { get; }

    /// <summary>
    ///     Gets the actual byte length of the bounded window after BOF/EOF clipping.
    /// </summary>
    [EntityProperty]
    public long? WindowByteLength { get; }

    /// <summary>
    ///     Gets whether the requested before/after bounds fit without BOF/EOF truncation.
    /// </summary>
    [EntityProperty]
    public bool? WindowComplete { get; }

    /// <summary>
    ///     Gets a copy of the bounded window bytes when requested by the projection.
    /// </summary>
    [EntityProperty]
    [BindablePropertyAsTable]
    public byte[]? WindowBytes => field is null ? null : (byte[])field.Clone();

    /// <summary>
    ///     Raw byte matches do not infer physical text lines.
    /// </summary>
    [EntityProperty]
    public long? LineNumber => null;

    /// <summary>
    ///     Raw byte matches do not infer UTF-16 columns.
    /// </summary>
    [EntityProperty]
    public long? Utf16Column => null;

    /// <summary>
    ///     Raw byte matches do not infer UTF-16 lengths.
    /// </summary>
    [EntityProperty]
    public long? Utf16Length => null;

    /// <summary>
    ///     Raw byte matches do not decode match text.
    /// </summary>
    [EntityProperty]
    public string? MatchText => null;

    /// <summary>
    ///     Raw byte matches have no regular-expression captures.
    /// </summary>
    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<SearchCapture> Captures => EmptyCaptures;

    /// <summary>
    ///     Raw byte matches have no decoded line context.
    /// </summary>
    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<SearchContextLine> Context => EmptyContext;

    /// <summary>
    ///     Raw byte matches do not retain decoded line text.
    /// </summary>
    [EntityProperty]
    public string? LineText => null;
}
