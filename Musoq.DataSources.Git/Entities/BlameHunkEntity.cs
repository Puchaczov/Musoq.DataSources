using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Musoq.DataSources.Git;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Detached metadata for a contiguous blame hunk. Accessing <see cref="Lines"/> opens a short-lived repository scope
/// only when the column is projected, so metadata-only blame never reads file content or retains a native handle.
/// </summary>
public class BlameHunkEntity
{
    /// <summary>Maps SQL-visible column names to their zero-based row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    /// <summary>Maps row indexes to the corresponding property accessors used by the Musoq runtime.</summary>
    public static readonly IReadOnlyDictionary<int, Func<BlameHunkEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a blame-hunk row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(StartLineNumber), 0, typeof(int)),
        new SchemaColumn(nameof(EndLineNumber), 1, typeof(int)),
        new SchemaColumn(nameof(LineCount), 2, typeof(int)),
        new SchemaColumn(nameof(CommitSha), 3, typeof(string)),
        new SchemaColumn(nameof(Author), 4, typeof(string)),
        new SchemaColumn(nameof(AuthorEmail), 5, typeof(string)),
        new SchemaColumn(nameof(AuthorDate), 6, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(Committer), 7, typeof(string)),
        new SchemaColumn(nameof(CommitterEmail), 8, typeof(string)),
        new SchemaColumn(nameof(CommitterDate), 9, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(Summary), 10, typeof(string)),
        new SchemaColumn(nameof(OriginalStartLineNumber), 11, typeof(int)),
        new SchemaColumn(nameof(OriginalFilePath), 12, typeof(string)),
        new SchemaColumn(nameof(Lines), 13, typeof(IEnumerable<BlameLineEntity>)),
        new SchemaColumn(nameof(Self), 14, typeof(BlameHunkEntity))
    ];

    private readonly string _author;
    private readonly DateTimeOffset _authorDate;
    private readonly string _authorEmail;
    private readonly string _commitSha;
    private readonly string _committer;
    private readonly DateTimeOffset _committerDate;
    private readonly string _committerEmail;
    private readonly string _contentCommitSha;
    private readonly string _filePath;
    private readonly int _finalStartLineNumber;
    private readonly int _lineCount;
    private readonly string? _originalFilePath;
    private readonly int? _originalStartLineNumber;
    private readonly string _repositoryPath;
    private readonly string _summary;
    private readonly GitNestedSnapshot<IReadOnlyList<BlameLineEntity>> _lines = new();

    static BlameHunkEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(StartLineNumber), 0 }, { nameof(EndLineNumber), 1 }, { nameof(LineCount), 2 },
            { nameof(CommitSha), 3 }, { nameof(Author), 4 }, { nameof(AuthorEmail), 5 },
            { nameof(AuthorDate), 6 }, { nameof(Committer), 7 }, { nameof(CommitterEmail), 8 },
            { nameof(CommitterDate), 9 }, { nameof(Summary), 10 }, { nameof(OriginalStartLineNumber), 11 },
            { nameof(OriginalFilePath), 12 }, { nameof(Lines), 13 }, { nameof(Self), 14 }
        };
        IndexToObjectAccessMap = new Dictionary<int, Func<BlameHunkEntity, object?>>
        {
            { 0, entity => entity.StartLineNumber }, { 1, entity => entity.EndLineNumber },
            { 2, entity => entity.LineCount }, { 3, entity => entity.CommitSha }, { 4, entity => entity.Author },
            { 5, entity => entity.AuthorEmail }, { 6, entity => entity.AuthorDate }, { 7, entity => entity.Committer },
            { 8, entity => entity.CommitterEmail }, { 9, entity => entity.CommitterDate },
            { 10, entity => entity.Summary }, { 11, entity => entity.OriginalStartLineNumber },
            { 12, entity => entity.OriginalFilePath }, { 13, entity => entity.Lines }, { 14, entity => entity.Self }
        };
    }

    /// <summary>Compatibility constructor. Returned rows no longer retain the supplied repository.</summary>
    public BlameHunkEntity(BlameHunk hunk, Repository repository, string filePath)
        : this(hunk, repository.Info.WorkingDirectory ?? repository.Info.Path, filePath)
    {
    }

    internal BlameHunkEntity(BlameHunk hunk, string repositoryPath, string filePath)
        : this(hunk, repositoryPath, filePath, new GitProjection(true, Columns.Select(column => column.ColumnName)))
    {
    }

    internal BlameHunkEntity(BlameHunk hunk, string repositoryPath, string filePath, GitProjection projection)
    {
        ArgumentNullException.ThrowIfNull(hunk);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var all = projection.Includes(nameof(Self));
        var needsLines = all || projection.Includes(nameof(Lines));
        var needsGeometry = all || needsLines || projection.Includes(nameof(StartLineNumber)) ||
                            projection.Includes(nameof(EndLineNumber)) || projection.Includes(nameof(LineCount));
        var needsCommit = all || needsLines || projection.Includes(nameof(CommitSha)) ||
                          projection.Includes(nameof(Author)) || projection.Includes(nameof(AuthorEmail)) ||
                          projection.Includes(nameof(AuthorDate)) || projection.Includes(nameof(Committer)) ||
                          projection.Includes(nameof(CommitterEmail)) || projection.Includes(nameof(CommitterDate)) ||
                          projection.Includes(nameof(Summary));
        var commit = needsCommit ? hunk.FinalCommit : null;
        var finalStart = needsGeometry ? hunk.FinalStartLineNumber : 0;

        _repositoryPath = needsLines ? repositoryPath : string.Empty;
        _filePath = needsLines ? filePath : string.Empty;
        _contentCommitSha = needsLines ? commit!.Sha : string.Empty;
        _finalStartLineNumber = finalStart;
        _lineCount = needsGeometry ? hunk.LineCount : 0;
        _commitSha = projection.Includes(nameof(CommitSha)) || all ? commit!.Sha : string.Empty;
        _author = projection.Includes(nameof(Author)) || all ? commit!.Author.Name : string.Empty;
        _authorEmail = projection.Includes(nameof(AuthorEmail)) || all ? commit!.Author.Email : string.Empty;
        _authorDate = projection.Includes(nameof(AuthorDate)) || all ? commit!.Author.When : default;
        _committer = projection.Includes(nameof(Committer)) || all ? commit!.Committer.Name : string.Empty;
        _committerEmail = projection.Includes(nameof(CommitterEmail)) || all ? commit!.Committer.Email : string.Empty;
        _committerDate = projection.Includes(nameof(CommitterDate)) || all ? commit!.Committer.When : default;
        _summary = projection.Includes(nameof(Summary)) || all ? commit!.MessageShort : string.Empty;
        _originalStartLineNumber = (projection.Includes(nameof(OriginalStartLineNumber)) || all) &&
                                  hunk.InitialStartLineNumber != hunk.FinalStartLineNumber
            ? hunk.InitialStartLineNumber + 1
            : null;
        _originalFilePath = (projection.Includes(nameof(OriginalFilePath)) || all) && hunk.InitialPath != filePath
            ? hunk.InitialPath
            : null;
    }

    /// <summary>Gets the one-based first line in the blamed file covered by this hunk.</summary>
    public int StartLineNumber => _finalStartLineNumber + 1;
    /// <summary>Gets the one-based last line in the blamed file covered by this hunk.</summary>
    public int EndLineNumber => _finalStartLineNumber + _lineCount;
    /// <summary>Gets the number of lines covered by this hunk.</summary>
    public int LineCount => _lineCount;
    /// <summary>Gets the SHA of the commit that supplied this hunk.</summary>
    public string CommitSha => _commitSha;
    /// <summary>Gets the author name recorded on the supplying commit.</summary>
    public string Author => _author;
    /// <summary>Gets the author email recorded on the supplying commit.</summary>
    public string AuthorEmail => _authorEmail;
    /// <summary>Gets the author timestamp recorded on the supplying commit.</summary>
    public DateTimeOffset AuthorDate => _authorDate;
    /// <summary>Gets the committer name recorded on the supplying commit.</summary>
    public string Committer => _committer;
    /// <summary>Gets the committer email recorded on the supplying commit.</summary>
    public string CommitterEmail => _committerEmail;
    /// <summary>Gets the committer timestamp recorded on the supplying commit.</summary>
    public DateTimeOffset CommitterDate => _committerDate;
    /// <summary>Gets the short commit message for the supplying commit.</summary>
    public string Summary => _summary;
    /// <summary>Gets the one-based original line when Git reports a moved hunk; otherwise <see langword="null"/>.</summary>
    public int? OriginalStartLineNumber => _originalStartLineNumber;
    /// <summary>Gets the original path when Git reports a moved hunk; otherwise <see langword="null"/>.</summary>
    public string? OriginalFilePath => _originalFilePath;

    /// <summary>Gets the blamed lines, loading and caching file content only on first access; binary files return no lines.</summary>
    public IEnumerable<BlameLineEntity> Lines => _lines.GetOrCreate(ReadLines) ?? Array.Empty<BlameLineEntity>();

    /// <summary>Gets this row instance.</summary>
    public BlameHunkEntity Self => this;

    private IReadOnlyList<BlameLineEntity> ReadLines()
    {
        using var repository = new Repository(_repositoryPath);
        var commit = repository.Lookup<Commit>(_contentCommitSha) ??
                     throw new InvalidOperationException($"Blame commit '{_contentCommitSha}' is no longer available.");
        var treeEntry = commit[_filePath] ??
                        throw new FileNotFoundException($"Blamed file '{_filePath}' is no longer available in '{_contentCommitSha}'.");
        if (treeEntry.TargetType != TreeEntryTargetType.Blob || ((Blob)treeEntry.Target).IsBinary)
            return [];

        var blob = (Blob)treeEntry.Target;
        using var stream = blob.GetContentStream();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        var allLines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var endIndex = Math.Min(_finalStartLineNumber + _lineCount, allLines.Length);
        var lines = new List<BlameLineEntity>(Math.Max(0, endIndex - _finalStartLineNumber));
        for (var index = _finalStartLineNumber; index < endIndex; index++)
            lines.Add(new BlameLineEntity(index + 1, allLines[index]));
        return lines;
    }
}
