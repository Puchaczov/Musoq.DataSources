#nullable enable

using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One eligible file containing at least one non-overlapping literal match.
/// </summary>
public sealed class SearchFile
{
    /// <summary>
    ///     Creates a matching Search file row.
    /// </summary>
    /// <param name="path">Path relative to the requested search root.</param>
    /// <param name="origin">Optional origin identity for a virtual source.</param>
    /// <param name="patternId">Optional pattern identifier.</param>
    public SearchFile(string path, string? origin, string? patternId)
    {
        Path = path;
        Origin = origin;
        PatternId = patternId;
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
}
