using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a Git commit entity.
/// </summary>
public class CommitEntity
{
    private readonly Commit? _commit;
    
    internal readonly Repository LibGitRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitEntity"/> class.
    /// </summary>
    /// <param name="commit">The LibGit2Sharp commit object.</param>
    /// <param name="repository">The repository the commit belongs to.</param>
    public CommitEntity(Commit? commit, Repository repository)
    {
        _commit = commit;
        LibGitRepository = repository;
    }

    /// <summary>
    /// A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    /// A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<CommitEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    /// An array of schema columns representing the structure of the commit entity.
    /// </summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Sha), 0, typeof(string)),
        new SchemaColumn(nameof(Message), 1, typeof(string)),
        new SchemaColumn(nameof(MessageShort), 2, typeof(string)),
        new SchemaColumn(nameof(Author), 3, typeof(string)),
        new SchemaColumn(nameof(AuthorEmail), 4, typeof(string)),
        new SchemaColumn(nameof(Committer), 5, typeof(string)),
        new SchemaColumn(nameof(CommitterEmail), 6, typeof(string)),
        new SchemaColumn(nameof(CommittedWhen), 7, typeof(DateTimeOffset)),
        new SchemaColumn(nameof(Parents), 8, typeof(IEnumerable<CommitEntity>)),
        new SchemaColumn(nameof(Self), 9, typeof(CommitEntity))
    ];

    /// <summary>
    /// Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static CommitEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(Sha), 0},
            {nameof(Message), 1},
            {nameof(MessageShort), 2},
            {nameof(Author), 3},
            {nameof(AuthorEmail), 4},
            {nameof(Committer), 5},
            {nameof(CommitterEmail), 6},
            {nameof(CommittedWhen), 7},
            {nameof(Parents), 8},
            {nameof(Self), 9}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<CommitEntity, object?>>
        {
            {0, entity => entity.Sha},
            {1, entity => entity.Message},
            {2, entity => entity.MessageShort},
            {3, entity => entity.Author},
            {4, entity => entity.AuthorEmail},
            {5, entity => entity.Committer},
            {6, entity => entity.CommitterEmail},
            {7, entity => entity.CommittedWhen},
            {8, entity => entity.Parents},
            {9, entity => entity.Self}
        };
    }

    /// <summary>
    /// Gets the SHA hash of the commit.
    /// </summary>
    public string? Sha => _commit?.Sha;

    /// <summary>
    /// Gets the full commit message.
    /// </summary>
    public string? Message => _commit?.Message;

    /// <summary>
    /// Gets the short commit message.
    /// </summary>
    public string? MessageShort => _commit?.MessageShort;

    /// <summary>
    /// Gets the name of the author of the commit.
    /// </summary>
    public string? Author => _commit?.Author?.Name;
    
    /// <summary>
    /// Gets the email of the author of the commit.
    /// </summary>
    public string? AuthorEmail => _commit?.Author?.Email;

    /// <summary>
    /// Gets the name of the committer of the commit.
    /// </summary>
    public string? Committer => _commit?.Committer?.Name;

    /// <summary>
    /// Gets the email of the committer.
    /// </summary>
    public string? CommitterEmail => _commit?.Committer?.Email;

    /// <summary>
    /// Gets the date and time when the commit was made.
    /// </summary>
    public DateTimeOffset? CommittedWhen => _commit?.Committer?.When;
    
    /// <summary>
    /// Gets the parent commits of this commit.
    /// </summary>
    public IEnumerable<CommitEntity> Parents => _commit?.Parents?.Select(p => new CommitEntity(p, LibGitRepository)) ?? Enumerable.Empty<CommitEntity>();
    
    /// <summary>
    /// Gets the commit itself.
    /// </summary>
    public CommitEntity Self => this;

    /// <summary>
    /// Gets the underlying LibGit2Sharp commit object.
    /// </summary>
    internal Commit? LibGitCommit => _commit;
}