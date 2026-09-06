#nullable enable

using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One exact count row for a completely processed eligible file.
/// </summary>
public sealed class SearchCount
{
    /// <summary>
    ///     Creates a Search count row.
    /// </summary>
    /// <param name="path">Path relative to the requested search root.</param>
    /// <param name="origin">Optional origin identity for a virtual source.</param>
    /// <param name="patternId">Optional pattern identifier.</param>
    /// <param name="occurrenceCount">Number of non-overlapping literal occurrences in the file.</param>
    /// <param name="matchingLineCount">Number of physical lines containing one or more occurrences.</param>
    /// <param name="bytesScanned">Number of original input bytes scanned for the file.</param>
    /// <param name="complete">Whether the row represents a complete exact scan.</param>
    public SearchCount(
        string path,
        string? origin,
        string? patternId,
        long occurrenceCount,
        long matchingLineCount,
        long bytesScanned,
        bool complete)
    {
        Path = path;
        Origin = origin;
        PatternId = patternId;
        OccurrenceCount = occurrenceCount;
        MatchingLineCount = matchingLineCount;
        BytesScanned = bytesScanned;
        Complete = complete;
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
    ///     Gets the number of non-overlapping literal occurrences in the file.
    /// </summary>
    [EntityProperty]
    public long OccurrenceCount { get; }

    /// <summary>
    ///     Gets the number of physical lines containing one or more occurrences.
    /// </summary>
    [EntityProperty]
    public long MatchingLineCount { get; }

    /// <summary>
    ///     Gets the number of original input bytes scanned for the file.
    /// </summary>
    [EntityProperty]
    public long BytesScanned { get; }

    /// <summary>
    ///     Gets a value indicating whether this row is an exact completed count.
    /// </summary>
    [EntityProperty]
    public bool Complete { get; }
}
