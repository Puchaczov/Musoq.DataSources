using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents the status of a file in the working directory.
/// </summary>
public class StatusEntity
{
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<StatusEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a status row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(FilePath), 0, typeof(string)),
        new SchemaColumn(nameof(State), 1, typeof(string)),
        new SchemaColumn(nameof(IndexStatus), 2, typeof(string)),
        new SchemaColumn(nameof(WorkDirStatus), 3, typeof(string))
    ];

    private readonly string _filePath;
    private readonly string _state;
    private readonly string _indexStatus;
    private readonly string _workDirStatus;

    static StatusEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(FilePath), 0 },
            { nameof(State), 1 },
            { nameof(IndexStatus), 2 },
            { nameof(WorkDirStatus), 3 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<StatusEntity, object?>>
        {
            { 0, entity => entity.FilePath },
            { 1, entity => entity.State },
            { 2, entity => entity.IndexStatus },
            { 3, entity => entity.WorkDirStatus }
        };
    }

    /// <summary>Creates a detached status snapshot from a LibGit2Sharp status entry.</summary>
    /// <param name="entry">The status entry to copy.</param>
    public StatusEntity(StatusEntry entry)
    {
        _filePath = entry.FilePath;
        _state = entry.State.ToString();
        _indexStatus = entry.State.HasFlag(FileStatus.NewInIndex) ||
                       entry.State.HasFlag(FileStatus.ModifiedInIndex) ||
                       entry.State.HasFlag(FileStatus.DeletedFromIndex) ||
                       entry.State.HasFlag(FileStatus.RenamedInIndex) ||
                       entry.State.HasFlag(FileStatus.TypeChangeInIndex)
            ? "Staged"
            : "NotStaged";
        _workDirStatus = entry.State.HasFlag(FileStatus.NewInWorkdir) ||
                         entry.State.HasFlag(FileStatus.ModifiedInWorkdir) ||
                         entry.State.HasFlag(FileStatus.DeletedFromWorkdir) ||
                         entry.State.HasFlag(FileStatus.RenamedInWorkdir) ||
                         entry.State.HasFlag(FileStatus.TypeChangeInWorkdir)
            ? "Modified"
            : "Unmodified";
    }

    internal StatusEntity(string filePath, string state, string indexStatus, string workDirStatus)
    {
        _filePath = filePath;
        _state = state;
        _indexStatus = indexStatus;
        _workDirStatus = workDirStatus;
    }

    /// <summary>Gets the path reported by Git.</summary>
    public string FilePath => _filePath;

    /// <summary>Gets the combined Git status flags as text.</summary>
    public string State => _state;

    /// <summary>Gets whether the index side is staged or not staged.</summary>
    public string IndexStatus => _indexStatus;

    /// <summary>Gets whether the working-tree side is modified or unmodified.</summary>
    public string WorkDirStatus => _workDirStatus;
}
