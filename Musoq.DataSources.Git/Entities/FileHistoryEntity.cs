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
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<FileHistoryEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a file-history row.</summary>
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

    private readonly string? _commitSha;
    private readonly string? _author;
    private readonly string? _authorEmail;
    private readonly DateTimeOffset? _committedWhen;
    private readonly string? _filePath;
    private readonly string? _changeType;
    private readonly string? _oldPath;

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

    /// <summary>Creates a detached file-history snapshot from a commit and tree change.</summary>
    /// <param name="commit">The commit containing the change, or <see langword="null"/> for a cardinality row.</param>
    /// <param name="change">The changed entry, or <see langword="null"/> when no entry is available.</param>
    public FileHistoryEntity(Commit? commit, TreeEntryChanges? change)
    {
        _commitSha = commit?.Sha;
        _author = commit?.Author?.Name;
        _authorEmail = commit?.Author?.Email;
        _committedWhen = commit?.Committer?.When;
        _filePath = change?.Path;
        _changeType = change?.Status.ToString();
        _oldPath = change?.OldPath;
    }

    /// <summary>Creates a detached file-history snapshot from a commit, path, and change kind.</summary>
    /// <param name="commit">The commit containing the change, or <see langword="null"/> for a cardinality row.</param>
    /// <param name="path">The changed path, or <see langword="null"/> when it is unavailable.</param>
    /// <param name="changeKind">The Git change kind.</param>
    public FileHistoryEntity(Commit? commit, string? path, ChangeKind changeKind)
    {
        _commitSha = commit?.Sha;
        _author = commit?.Author?.Name;
        _authorEmail = commit?.Author?.Email;
        _committedWhen = commit?.Committer?.When;
        _filePath = path;
        _changeType = changeKind.ToString();
    }

    internal FileHistoryEntity(
        string commitSha,
        string author,
        string authorEmail,
        DateTimeOffset committedWhen,
        string filePath,
        string changeType,
        string? oldPath)
    {
        _commitSha = commitSha;
        _author = author;
        _authorEmail = authorEmail;
        _committedWhen = committedWhen;
        _filePath = filePath;
        _changeType = changeType;
        _oldPath = oldPath;
    }

    /// <summary>Gets the commit SHA, or <see langword="null"/> when no commit was supplied.</summary>
    public string? CommitSha => _commitSha;

    /// <summary>Gets the commit author's display name.</summary>
    public string? Author => _author;

    /// <summary>Gets the commit author's email address.</summary>
    public string? AuthorEmail => _authorEmail;

    /// <summary>Gets the commit timestamp, or the default value when no commit was supplied.</summary>
    public DateTimeOffset CommittedWhen => _committedWhen ?? default;

    /// <summary>Gets the changed path in the newer tree.</summary>
    public string? FilePath => _filePath;

    /// <summary>Gets the Git change-kind name.</summary>
    public string? ChangeType => _changeType;

    /// <summary>Gets the path in the older tree for a rename or copy, when available.</summary>
    public string? OldPath => _oldPath;
}
