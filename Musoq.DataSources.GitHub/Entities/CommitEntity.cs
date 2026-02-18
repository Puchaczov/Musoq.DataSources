using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
///     Represents a GitHub commit entity for querying.
/// </summary>
public class CommitEntity
{
    private readonly GitHubCommit _commit;

    /// <summary>
    ///     Initializes a new instance of the CommitEntity class.
    /// </summary>
    /// <param name="commit">The underlying Octokit commit.</param>
    public CommitEntity(GitHubCommit commit)
    {
        _commit = commit;
    }

    /// <summary>
    ///     Gets the commit SHA.
    /// </summary>
    public string Sha => _commit.Sha;

    /// <summary>
    ///     Gets the short SHA (first 7 characters).
    /// </summary>
    public string ShortSha => _commit.Sha.Length >= 7 ? _commit.Sha[..7] : _commit.Sha;

    /// <summary>
    ///     Gets the commit message.
    /// </summary>
    public string Message => _commit.Commit?.Message ?? string.Empty;

    /// <summary>
    ///     Gets the commit URL.
    /// </summary>
    public string Url => _commit.HtmlUrl;

    /// <summary>
    ///     Gets the author's name.
    /// </summary>
    public string? AuthorName => _commit.Commit?.Author?.Name;

    /// <summary>
    ///     Gets the author's email.
    /// </summary>
    public string? AuthorEmail => _commit.Commit?.Author?.Email;

    /// <summary>
    ///     Gets the author's login (GitHub user).
    /// </summary>
    public string? AuthorLogin => _commit.Author?.Login;

    /// <summary>
    ///     Gets the author's ID.
    /// </summary>
    public long? AuthorId => _commit.Author?.Id;

    /// <summary>
    ///     Gets the author date.
    /// </summary>
    public DateTimeOffset? AuthorDate => _commit.Commit?.Author?.Date;

    /// <summary>
    ///     Gets the committer's name.
    /// </summary>
    public string? CommitterName => _commit.Commit?.Committer?.Name;

    /// <summary>
    ///     Gets the committer's email.
    /// </summary>
    public string? CommitterEmail => _commit.Commit?.Committer?.Email;

    /// <summary>
    ///     Gets the committer's login (GitHub user).
    /// </summary>
    public string? CommitterLogin => _commit.Committer?.Login;

    /// <summary>
    ///     Gets the committer's ID.
    /// </summary>
    public long? CommitterId => _commit.Committer?.Id;

    /// <summary>
    ///     Gets the committer date.
    /// </summary>
    public DateTimeOffset? CommitterDate => _commit.Commit?.Committer?.Date;

    /// <summary>
    ///     Gets the number of additions.
    /// </summary>
    public int Additions => _commit.Stats?.Additions ?? 0;

    /// <summary>
    ///     Gets the number of deletions.
    /// </summary>
    public int Deletions => _commit.Stats?.Deletions ?? 0;

    /// <summary>
    ///     Gets the total number of changes.
    /// </summary>
    public int Total => _commit.Stats?.Total ?? 0;

    /// <summary>
    ///     Gets the parent SHA(s).
    /// </summary>
    public string ParentShas => string.Join(", ", _commit.Parents?.Select(p => p.Sha) ?? []);

    /// <summary>
    ///     Gets the number of parent commits.
    /// </summary>
    public int ParentCount => _commit.Parents?.Count ?? 0;

    /// <summary>
    ///     Gets the comment count.
    /// </summary>
    public int CommentCount => _commit.Commit?.CommentCount ?? 0;

    /// <summary>
    ///     Gets whether the commit is verified.
    /// </summary>
    public bool? Verified => _commit.Commit?.Verification?.Verified;

    /// <summary>
    ///     Gets the verification reason.
    /// </summary>
    public string? VerificationReason => _commit.Commit?.Verification?.Reason.StringValue;

    /// <summary>
    ///     Gets the files changed in this commit.
    /// </summary>
    public int FilesChanged => _commit.Files?.Count ?? 0;
}