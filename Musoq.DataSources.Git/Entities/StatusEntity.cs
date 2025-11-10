using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents the status of a file in the working directory.
/// </summary>
public class StatusEntity
{
    private readonly StatusEntry _entry;

    public StatusEntity(StatusEntry entry)
    {
        _entry = entry;
    }

    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<StatusEntity, object?>> IndexToObjectAccessMap;

    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(FilePath), 0, typeof(string)),
        new SchemaColumn(nameof(State), 1, typeof(string)),
        new SchemaColumn(nameof(IndexStatus), 2, typeof(string)),
        new SchemaColumn(nameof(WorkDirStatus), 3, typeof(string))
    ];

    static StatusEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(FilePath), 0},
            {nameof(State), 1},
            {nameof(IndexStatus), 2},
            {nameof(WorkDirStatus), 3}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<StatusEntity, object?>>
        {
            {0, entity => entity.FilePath},
            {1, entity => entity.State},
            {2, entity => entity.IndexStatus},
            {3, entity => entity.WorkDirStatus}
        };
    }

    public string FilePath => _entry.FilePath;
    public string State => _entry.State.ToString();
    public string IndexStatus => _entry.State.HasFlag(FileStatus.NewInIndex) || _entry.State.HasFlag(FileStatus.ModifiedInIndex) || _entry.State.HasFlag(FileStatus.DeletedFromIndex) || _entry.State.HasFlag(FileStatus.RenamedInIndex) || _entry.State.HasFlag(FileStatus.TypeChangeInIndex) ? "Staged" : "NotStaged";
    public string WorkDirStatus => _entry.State.HasFlag(FileStatus.NewInWorkdir) || _entry.State.HasFlag(FileStatus.ModifiedInWorkdir) || _entry.State.HasFlag(FileStatus.DeletedFromWorkdir) || _entry.State.HasFlag(FileStatus.RenamedInWorkdir) || _entry.State.HasFlag(FileStatus.TypeChangeInWorkdir) ? "Modified" : "Unmodified";
}
