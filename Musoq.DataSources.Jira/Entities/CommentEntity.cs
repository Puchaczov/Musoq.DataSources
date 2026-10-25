using Atlassian.Jira;

namespace Musoq.DataSources.Jira.Entities;

/// <summary>
///     Represents a Jira comment entity for querying.
/// </summary>
public class CommentEntity : IJiraComment
{
    private readonly Comment _comment;

    /// <summary>
    ///     Initializes a new instance of the CommentEntity class.
    /// </summary>
    /// <param name="comment">The underlying Atlassian.SDK comment.</param>
    /// <param name="issueKey">The parent issue key.</param>
    public CommentEntity(Comment comment, string issueKey)
    {
        _comment = comment;
        IssueKey = issueKey;
    }

    /// <summary>
    ///     Gets the comment ID.
    /// </summary>
    public string Id => ReadId(_comment);

    /// <summary>
    ///     Gets the parent issue key.
    /// </summary>
    public string IssueKey { get; }

    /// <summary>
    ///     Gets the comment body.
    /// </summary>
    public string Body => ReadBody(_comment);

    /// <summary>
    ///     Gets the author username.
    /// </summary>
    public string Author => ReadAuthor(_comment);

    /// <summary>
    ///     Gets the author display name.
    /// </summary>
    public string? AuthorDisplayName => ReadAuthorDisplayName(_comment);

    /// <summary>
    ///     Gets the update author username.
    /// </summary>
    public string? UpdateAuthor => ReadUpdateAuthor(_comment);

    /// <summary>
    ///     Gets the update author display name.
    /// </summary>
    public string? UpdateAuthorDisplayName => ReadUpdateAuthorDisplayName(_comment);

    /// <summary>
    ///     Gets the creation date.
    /// </summary>
    public DateTimeOffset? CreatedAt => _comment.CreatedDate;

    /// <summary>
    ///     Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedAt => _comment.UpdatedDate;

    /// <summary>
    ///     Gets the visibility group name (if restricted).
    /// </summary>
    public string? VisibilityGroup => ReadVisibilityGroup(_comment);

    /// <summary>
    ///     Gets the visibility role name (if restricted).
    /// </summary>
    public string? VisibilityRole => ReadVisibilityRole(_comment);

    internal static string ReadId(Comment value) => value.Id ?? string.Empty;
    internal static string ReadBody(Comment value) => value.Body ?? string.Empty;
    internal static string ReadAuthor(Comment value) => value.Author ?? string.Empty;
    internal static string? ReadAuthorDisplayName(Comment value) => value.AuthorUser?.DisplayName;
    internal static string? ReadUpdateAuthor(Comment value) => value.UpdateAuthor;
    internal static string? ReadUpdateAuthorDisplayName(Comment value) => value.UpdateAuthorUser?.DisplayName;
    internal static string? ReadVisibilityGroup(Comment value) => value.GroupLevel;
    internal static string? ReadVisibilityRole(Comment value) => value.RoleLevel;
}