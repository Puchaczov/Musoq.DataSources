#nullable enable

using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One eligible regular-file path discovered by the Search scope source.
/// </summary>
public sealed class SearchPath
{
    /// <summary>
    ///     Creates an eligible Search path row.
    /// </summary>
    /// <param name="path">Path relative to the requested search root.</param>
    /// <param name="origin">Optional origin identity for a virtual source.</param>
    /// <param name="entryKind">The stable entry kind; local path rows use <c>file</c>.</param>
    public SearchPath(string path, string? origin, string entryKind)
    {
        Path = path;
        Origin = origin;
        EntryKind = entryKind;
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
    ///     Gets the entry kind. Local eligible path rows use <c>file</c>.
    /// </summary>
    [EntityProperty]
    public string EntryKind { get; }
}
