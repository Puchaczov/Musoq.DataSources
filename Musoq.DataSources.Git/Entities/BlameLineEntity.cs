using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a single line within a blame hunk.
/// </summary>
public class BlameLineEntity
{
    /// <summary>
    ///     A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    ///     A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<BlameLineEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    ///     An array of schema columns representing the structure of the blame line entity.
    /// </summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(LineNumber), 0, typeof(int)),
        new SchemaColumn(nameof(Content), 1, typeof(string)),
        new SchemaColumn(nameof(Self), 2, typeof(BlameLineEntity))
    ];

    /// <summary>
    ///     Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static BlameLineEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(LineNumber), 0 },
            { nameof(Content), 1 },
            { nameof(Self), 2 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<BlameLineEntity, object?>>
        {
            { 0, entity => entity.LineNumber },
            { 1, entity => entity.Content },
            { 2, entity => entity.Self }
        };
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlameLineEntity" /> class.
    /// </summary>
    /// <param name="lineNumber">The line number (1-based).</param>
    /// <param name="content">The actual line content.</param>
    public BlameLineEntity(int lineNumber, string content)
    {
        LineNumber = lineNumber;
        Content = content;
    }

    /// <summary>
    ///     Gets the line number (1-based).
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    ///     Gets the actual line content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    ///     Gets the line entity itself.
    /// </summary>
    public BlameLineEntity Self => this;
}