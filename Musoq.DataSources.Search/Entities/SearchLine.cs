#nullable enable

using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One physical line containing one or more non-overlapping literal matches.
/// </summary>
public sealed class SearchLine
{
    /// <summary>
    ///     Creates a matching Search line row.
    /// </summary>
    /// <param name="path">Path relative to the requested search root.</param>
    /// <param name="origin">Optional origin identity for a virtual source.</param>
    /// <param name="patternId">Optional pattern identifier.</param>
    /// <param name="lineNumber">One-based physical line number.</param>
    /// <param name="byteOffset">Original-byte start of the line when mapped.</param>
    /// <param name="lineText">Decoded physical line, including its terminator when available.</param>
    /// <param name="occurrenceCount">Number of matches for the pattern on the line.</param>
    public SearchLine(
        string path,
        string? origin,
        string? patternId,
        long lineNumber,
        long? byteOffset,
        string? lineText,
        long occurrenceCount)
    {
        Path = path;
        Origin = origin;
        PatternId = patternId;
        LineNumber = lineNumber;
        ByteOffset = byteOffset;
        LineText = lineText;
        OccurrenceCount = occurrenceCount;
    }

    /// <summary>
    ///     Gets the path relative to the requested search root.
    /// </summary>
    [EntityProperty]
    public string Path { get; }

    /// <summary>
    ///     Gets the optional origin identity for a virtual or container source.
    /// </summary>
    [EntityProperty]
    public string? Origin { get; }

    /// <summary>
    ///     Gets the optional pattern identifier.
    /// </summary>
    [EntityProperty]
    public string? PatternId { get; }

    /// <summary>
    ///     Gets the one-based physical line number.
    /// </summary>
    [EntityProperty]
    public long LineNumber { get; }

    /// <summary>
    ///     Gets the original-byte start of the line when a lossless mapping exists.
    /// </summary>
    [EntityProperty]
    public long? ByteOffset { get; }

    /// <summary>
    ///     Gets the decoded physical line, including its terminator when available.
    /// </summary>
    [EntityProperty]
    public string? LineText { get; }

    /// <summary>
    ///     Gets the number of matches for the pattern on this line.
    /// </summary>
    [EntityProperty]
    public long OccurrenceCount { get; }
}
