using System;
using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a Git commit entity.
/// </summary>
public class CommitEntity
{
    private readonly Commit? _commit;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitEntity"/> class.
    /// </summary>
    /// <param name="commit">The LibGit2Sharp commit object.</param>
    public CommitEntity(Commit? commit)
    {
        _commit = commit;
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
    /// Gets the underlying LibGit2Sharp commit object.
    /// </summary>
    internal Commit? LibGitCommit => _commit;
}