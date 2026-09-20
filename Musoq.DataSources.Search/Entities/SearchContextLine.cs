#nullable enable

using System;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Search.Entities;

/// <summary>
///     One physical line adjacent to a search match.
/// </summary>
public sealed class SearchContextLine
{
    /// <summary>
    ///     Creates one context line.
    /// </summary>
    /// <param name="relativeLine">Signed line distance from the matching line.</param>
    /// <param name="lineNumber">One-based physical line number.</param>
    /// <param name="lineText">Decoded line text, including its terminator when retained.</param>
    public SearchContextLine(int relativeLine, long lineNumber, string? lineText)
    {
        if (relativeLine == 0)
            throw new ArgumentOutOfRangeException(nameof(relativeLine));
        if (lineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

        RelativeLine = relativeLine;
        LineNumber = lineNumber;
        LineText = lineText;
    }

    /// <summary>
    ///     Gets the signed physical-line distance from the matching line.
    /// </summary>
    [EntityProperty]
    public int RelativeLine { get; }

    /// <summary>
    ///     Gets the one-based physical line number.
    /// </summary>
    [EntityProperty]
    public long LineNumber { get; }

    /// <summary>
    ///     Gets the retained decoded line text, or null when the context byte budget did not retain text.
    /// </summary>
    [EntityProperty]
    public string? LineText { get; }
}
