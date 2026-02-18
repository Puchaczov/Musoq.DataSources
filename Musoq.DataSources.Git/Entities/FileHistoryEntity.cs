using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a file change in Git history.
/// </summary>
public class FileHistoryEntity
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<FileHistoryEntity, object?>> IndexToObjectAccessMap;

    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(CommitSha), 0, typeof(string)),
        new SchemaColumn(nameof(Author), 1, typeof(string)),
        new SchemaColumn(nameof(AuthorEmail), 2, typeof(string)),
        new SchemaColumn(nameof(CommittedWhen), 3, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(FilePath), 4, typeof(string)),
        new SchemaColumn(nameof(ChangeType), 5, typeof(string)),
        new SchemaColumn(nameof(OldPath), 6, typeof(string))
    ];

    private readonly TreeEntryChanges? _change;
    private readonly ChangeKind? _changeKind;
    private readonly Commit? _commit;
    private readonly string? _path;

    static FileHistoryEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(CommitSha), 0 },
            { nameof(Author), 1 },
            { nameof(AuthorEmail), 2 },
            { nameof(CommittedWhen), 3 },
            { nameof(FilePath), 4 },
            { nameof(ChangeType), 5 },
            { nameof(OldPath), 6 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<FileHistoryEntity, object?>>
        {
            { 0, entity => entity.CommitSha },
            { 1, entity => entity.Author },
            { 2, entity => entity.AuthorEmail },
            { 3, entity => entity.CommittedWhen },
            { 4, entity => entity.FilePath },
            { 5, entity => entity.ChangeType },
            { 6, entity => entity.OldPath }
        };
    }

    public FileHistoryEntity(Commit? commit, TreeEntryChanges? change)
    {
        _commit = commit;
        _change = change;
    }

    public FileHistoryEntity(Commit? commit, string? path, ChangeKind changeKind)
    {
        _commit = commit;
        _path = path;
        _changeKind = changeKind;
    }

    public string? CommitSha => _commit?.Sha;
    public string? Author => _commit?.Author?.Name;
    public string? AuthorEmail => _commit?.Author?.Email;
    public DateTimeOffset? CommittedWhen => _commit?.Committer?.When;
    public string? FilePath => _change?.Path ?? _path;
    public string? ChangeType => _change?.Status.ToString() ?? _changeKind?.ToString();
    public string? OldPath => _change?.OldPath;
}