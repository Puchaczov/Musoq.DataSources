using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a file change in Git history.
/// </summary>
public class FileHistoryEntity
{
    private readonly TreeEntryChanges? _change;
    private readonly Commit? _commit;
    private readonly Repository _repository;

    public FileHistoryEntity(Commit? commit, TreeEntryChanges? change, Repository repository)
    {
        _commit = commit;
        _change = change;
        _repository = repository;
    }

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
        new SchemaColumn(nameof(OldPath), 6, typeof(string)),
        new SchemaColumn(nameof(LinesAdded), 7, typeof(int)),
        new SchemaColumn(nameof(LinesDeleted), 8, typeof(int)),
        new SchemaColumn(nameof(Commit), 9, typeof(CommitEntity))
    ];

    static FileHistoryEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(CommitSha), 0},
            {nameof(Author), 1},
            {nameof(AuthorEmail), 2},
            {nameof(CommittedWhen), 3},
            {nameof(FilePath), 4},
            {nameof(ChangeType), 5},
            {nameof(OldPath), 6},
            {nameof(LinesAdded), 7},
            {nameof(LinesDeleted), 8},
            {nameof(Commit), 9}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<FileHistoryEntity, object?>>
        {
            {0, entity => entity.CommitSha},
            {1, entity => entity.Author},
            {2, entity => entity.AuthorEmail},
            {3, entity => entity.CommittedWhen},
            {4, entity => entity.FilePath},
            {5, entity => entity.ChangeType},
            {6, entity => entity.OldPath},
            {7, entity => entity.LinesAdded},
            {8, entity => entity.LinesDeleted},
            {9, entity => entity.Commit}
        };
    }

    public string? CommitSha => _commit?.Sha;
    public string? Author => _commit?.Author?.Name;
    public string? AuthorEmail => _commit?.Author?.Email;
    public DateTimeOffset? CommittedWhen => _commit?.Committer?.When;
    public string? FilePath => _change?.Path;
    public string? ChangeType => _change?.Status.ToString();
    public string? OldPath => _change?.OldPath;
    public int LinesAdded => _change != null ? GetPatchStats()?.LinesAdded ?? 0 : 0;
    public int LinesDeleted => _change != null ? GetPatchStats()?.LinesDeleted ?? 0 : 0;
    public CommitEntity Commit => new(_commit, _repository);

    private PatchStats? GetPatchStats()
    {
        if (_commit == null || _change == null) return null;
        
        try
        {
            var parent = _commit.Parents.FirstOrDefault();
            if (parent == null) return null;
            
            var patch = _repository.Diff.Compare<Patch>(parent.Tree, _commit.Tree);
            var entry = patch[_change.Path];
            return entry != null ? new PatchStats { LinesAdded = entry.LinesAdded, LinesDeleted = entry.LinesDeleted } : null;
        }
        catch
        {
            return null;
        }
    }

    private class PatchStats
    {
        public int LinesAdded { get; set; }
        public int LinesDeleted { get; set; }
    }
}
