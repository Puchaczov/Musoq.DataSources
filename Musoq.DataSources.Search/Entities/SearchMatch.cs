#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One non-overlapping match emitted by the minimal Search source.
/// </summary>
public sealed class SearchMatch
{
    private static readonly IReadOnlyList<SearchCapture> EmptyCaptures =
        Array.AsReadOnly(Array.Empty<SearchCapture>());
    private static readonly IReadOnlyList<SearchContextLine> EmptyContext =
        Array.AsReadOnly(Array.Empty<SearchContextLine>());
    private readonly string? _patternId;
    private readonly SearchEvidenceHandle? _evidence;

    /// <summary>
    ///     Creates a search match.
    /// </summary>
    /// <param name="path">Path relative to the requested search root.</param>
    /// <param name="matchIndex">Zero-based match ordinal within the path.</param>
    /// <param name="lineNumber">One-based physical line containing the match.</param>
    /// <param name="utf16Column">Zero-based UTF-16 column within the line.</param>
    /// <param name="matchText">Matched literal text.</param>
    public SearchMatch(string path, long matchIndex, long lineNumber, long utf16Column, string matchText)
        : this(
            path,
            matchIndex,
            byteOffset: null,
            byteLength: null,
            lineNumber,
            utf16Column,
            utf16Length: matchText?.Length ?? 0,
            matchText!)
    {
    }

    internal SearchMatch(
        string path,
        long matchIndex,
        long? byteOffset,
        long? byteLength,
        long lineNumber,
        long utf16Column,
        long utf16Length,
        string? matchText,
        IReadOnlyList<SearchCapture>? captures = null,
        string? patternId = null,
        IReadOnlyList<SearchContextLine>? context = null,
        SearchEvidenceHandle? evidence = null)
    {
        Path = path;
        _patternId = patternId;
        _evidence = evidence;
        MatchIndex = matchIndex;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        LineNumber = lineNumber;
        Utf16Column = utf16Column;
        Utf16Length = utf16Length;
        MatchText = matchText;
        Captures = captures ?? EmptyCaptures;
        Context = FreezeContext(context);
    }

    /// <summary>
    ///     Gets the path relative to the requested search root.
    /// </summary>
    [EntityProperty]
    public string Path { get; }

    /// <summary>
    ///     Gets the optional pattern identifier. Single-pattern rows leave it null;
    ///     labeled many-pattern rows contain the request's pattern identifier.
    /// </summary>
    [EntityProperty]
    public string? PatternId => _patternId;

    /// <summary>
    ///     Gets the zero-based match ordinal within the path.
    /// </summary>
    [EntityProperty]
    public long MatchIndex { get; }

    /// <summary>
    ///     Gets the original-byte start of the match when losslessly mapped.
    /// </summary>
    [EntityProperty]
    public long? ByteOffset { get; }

    /// <summary>
    ///     Gets the original-byte length of the match when losslessly mapped.
    /// </summary>
    [EntityProperty]
    public long? ByteLength { get; }

    /// <summary>
    ///     Gets the one-based physical line containing the match.
    /// </summary>
    [EntityProperty]
    public long LineNumber { get; }

    /// <summary>
    ///     Gets the zero-based UTF-16 column within the line.
    /// </summary>
    [EntityProperty]
    public long Utf16Column { get; }

    /// <summary>
    ///     Gets the match length in UTF-16 code units.
    /// </summary>
    [EntityProperty]
    public long Utf16Length { get; }

    /// <summary>
    ///     Gets the matched text.
    /// </summary>
    [EntityProperty]
    public string? MatchText { get; }

    /// <summary>
    ///     Gets the typed regular-expression captures for this match.
    ///     Literal matches and projections that do not request captures return an empty collection.
    /// </summary>
    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<SearchCapture> Captures { get; }

    /// <summary>
    ///     Gets the bounded physical lines adjacent to this match.
    ///     The matching line itself is not repeated in the collection.
    /// </summary>
    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<SearchContextLine> Context { get; }

    /// <summary>
    ///     Retrieves another bounded context window after verifying that the source is unchanged.
    ///     This is an internal execution seam; SQL source constructor arguments remain unchanged.
    /// </summary>
    internal IReadOnlyList<SearchContextLine> ExpandContext(
        SearchContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _evidence?.Expand(LineNumber, options, cancellationToken) ??
               throw new InvalidOperationException(
                   "This Search row does not retain a source evidence handle.");
    }

    private static IReadOnlyList<SearchContextLine> FreezeContext(
        IReadOnlyList<SearchContextLine>? context)
    {
        if (context is null || context.Count == 0)
            return EmptyContext;

        var copy = new SearchContextLine[context.Count];
        for (var index = 0; index < context.Count; index++)
            copy[index] = context[index] ?? throw new ArgumentException("Context cannot contain null rows.", nameof(context));

        return Array.AsReadOnly(copy);
    }
}
