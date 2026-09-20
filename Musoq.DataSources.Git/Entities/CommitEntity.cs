using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a Git commit entity.
/// </summary>
public class CommitEntity
{
    /// <summary>
    ///     A read-only dictionary mapping column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    ///     A read-only dictionary mapping column indices to functions that access the corresponding properties.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<CommitEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    ///     An array of schema columns representing the structure of the commit entity.
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

    private readonly string? _repositoryPath;
    private readonly string? _sha;
    private readonly string? _message;
    private readonly string? _messageShort;
    private readonly string? _author;
    private readonly string? _authorEmail;
    private readonly string? _committer;
    private readonly string? _committerEmail;
    private readonly DateTimeOffset _committedWhen;
    private readonly GitNestedSnapshot<IReadOnlyList<CommitEntity>> _parents = new();

    /// <summary>
    ///     Static constructor to initialize the static read-only dictionaries.
    /// </summary>
    static CommitEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(Sha), 0 },
            { nameof(Message), 1 },
            { nameof(MessageShort), 2 },
            { nameof(Author), 3 },
            { nameof(AuthorEmail), 4 },
            { nameof(Committer), 5 },
            { nameof(CommitterEmail), 6 },
            { nameof(CommittedWhen), 7 },
            { nameof(Parents), 8 },
            { nameof(Self), 9 }
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<CommitEntity, object?>>
        {
            { 0, entity => entity.Sha },
            { 1, entity => entity.Message },
            { 2, entity => entity.MessageShort },
            { 3, entity => entity.Author },
            { 4, entity => entity.AuthorEmail },
            { 5, entity => entity.Committer },
            { 6, entity => entity.CommitterEmail },
            { 7, entity => entity.CommittedWhen },
            { 8, entity => entity.Parents },
            { 9, entity => entity.Self }
        };
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommitEntity" /> class.
    /// </summary>
    /// <param name="commit">The LibGit2Sharp commit object.</param>
    /// <param name="repository">The repository the commit belongs to.</param>
    public CommitEntity(Commit? commit, Repository repository)
    {
        _repositoryPath = repository.Info.Path;
        _sha = commit?.Sha;
        _message = commit?.Message;
        _messageShort = commit?.MessageShort;
        _author = commit?.Author?.Name;
        _authorEmail = commit?.Author?.Email;
        _committer = commit?.Committer?.Name;
        _committerEmail = commit?.Committer?.Email;
        _committedWhen = commit?.Committer?.When ?? default;
    }

    internal CommitEntity(
        string? repositoryPath,
        string? sha,
        string? message,
        string? messageShort,
        string? author,
        string? authorEmail,
        string? committer,
        string? committerEmail,
        DateTimeOffset committedWhen)
    {
        _repositoryPath = repositoryPath;
        _sha = sha;
        _message = message;
        _messageShort = messageShort;
        _author = author;
        _authorEmail = authorEmail;
        _committer = committer;
        _committerEmail = committerEmail;
        _committedWhen = committedWhen;
    }

    /// <summary>
    ///     Gets the SHA hash of the commit.
    /// </summary>
    public string? Sha => _sha;

    /// <summary>
    ///     Gets the full commit message.
    /// </summary>
    public string? Message => _message;

    /// <summary>
    ///     Gets the short commit message.
    /// </summary>
    public string? MessageShort => _messageShort;

    /// <summary>
    ///     Gets the name of the author of the commit.
    /// </summary>
    public string? Author => _author;

    /// <summary>
    ///     Gets the email of the author of the commit.
    /// </summary>
    public string? AuthorEmail => _authorEmail;

    /// <summary>
    ///     Gets the name of the committer of the commit.
    /// </summary>
    public string? Committer => _committer;

    /// <summary>
    ///     Gets the email of the committer.
    /// </summary>
    public string? CommitterEmail => _committerEmail;

    /// <summary>
    ///     Gets the date and time when the commit was made.
    /// </summary>
    public DateTimeOffset CommittedWhen => _committedWhen;

    /// <summary>
    ///     Gets the parent commits of this commit.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<CommitEntity> Parents
    {
        get
        {
            return _parents.GetOrCreate(() =>
            {
                if (string.IsNullOrWhiteSpace(_repositoryPath) || string.IsNullOrWhiteSpace(_sha))
                    return Array.Empty<CommitEntity>();

                using var repository = new Repository(_repositoryPath);
                return repository.Lookup<Commit>(_sha)?.Parents
                    .Select(parent => new CommitEntity(parent, repository))
                    .ToArray() ?? Array.Empty<CommitEntity>();
            }) ?? Array.Empty<CommitEntity>();
        }
    }

    /// <summary>
    ///     Gets the commit itself.
    /// </summary>
    public CommitEntity Self => this;

    internal string? RepositoryPath => _repositoryPath;
}
