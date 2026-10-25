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
    public string Sha => ReadSha(_commit);

    /// <summary>
    ///     Gets the short SHA (first 7 characters).
    /// </summary>
    public string ShortSha => ReadShortSha(_commit);

    /// <summary>
    ///     Gets the commit message.
    /// </summary>
    public string Message => ReadMessage(_commit);

    /// <summary>
    ///     Gets the commit URL.
    /// </summary>
    public string Url => ReadUrl(_commit);

    /// <summary>
    ///     Gets the author's name.
    /// </summary>
    public string? AuthorName => ReadAuthorName(_commit);

    /// <summary>
    ///     Gets the author's email.
    /// </summary>
    public string? AuthorEmail => ReadAuthorEmail(_commit);

    /// <summary>
    ///     Gets the author's login (GitHub user).
    /// </summary>
    public string? AuthorLogin => ReadAuthorLogin(_commit);

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
    public string? CommitterName => ReadCommitterName(_commit);

    /// <summary>
    ///     Gets the committer's email.
    /// </summary>
    public string? CommitterEmail => ReadCommitterEmail(_commit);

    /// <summary>
    ///     Gets the committer's login (GitHub user).
    /// </summary>
    public string? CommitterLogin => ReadCommitterLogin(_commit);

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
    public string ParentShas => ReadParentShas(_commit);

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
    public string? VerificationReason => ReadVerificationReason(_commit);

    /// <summary>
    ///     Gets the files changed in this commit.
    /// </summary>
    public int FilesChanged => _commit.Files?.Count ?? 0;

    internal static string ReadSha(GitHubCommit value) => value.Sha;
    internal static string ReadShortSha(GitHubCommit value) => value.Sha.Length >= 7 ? value.Sha[..7] : value.Sha;
    internal static string ReadMessage(GitHubCommit value) => value.Commit?.Message ?? string.Empty;
    internal static string ReadUrl(GitHubCommit value) => value.HtmlUrl;
    internal static string? ReadAuthorName(GitHubCommit value) => value.Commit?.Author?.Name;
    internal static string? ReadAuthorEmail(GitHubCommit value) => value.Commit?.Author?.Email;
    internal static string? ReadAuthorLogin(GitHubCommit value) => value.Author?.Login;
    internal static string? ReadCommitterName(GitHubCommit value) => value.Commit?.Committer?.Name;
    internal static string? ReadCommitterEmail(GitHubCommit value) => value.Commit?.Committer?.Email;
    internal static string? ReadCommitterLogin(GitHubCommit value) => value.Committer?.Login;
    internal static string ReadParentShas(GitHubCommit value) => string.Join(", ", value.Parents?.Select(p => p.Sha) ?? []);
    internal static string? ReadVerificationReason(GitHubCommit value) => value.Commit?.Verification?.Reason.StringValue;
}