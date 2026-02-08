using Atlassian.Jira;

namespace Musoq.DataSources.Jira.Entities;

/// <summary>
/// Represents a Jira comment entity for querying.
/// </summary>
public class CommentEntity : IJiraComment
{
    private readonly Comment _comment;
    private readonly string _issueKey;

    /// <summary>
    /// Initializes a new instance of the CommentEntity class.
    /// </summary>
    /// <param name="comment">The underlying Atlassian.SDK comment.</param>
    /// <param name="issueKey">The parent issue key.</param>
    public CommentEntity(Comment comment, string issueKey)
    {
        _comment = comment;
        _issueKey = issueKey;
    }

    /// <summary>
    /// Gets the comment ID.
    /// </summary>
    public string Id => _comment.Id ?? string.Empty;

    /// <summary>
    /// Gets the parent issue key.
    /// </summary>
    public string IssueKey => _issueKey;

    /// <summary>
    /// Gets the comment body.
    /// </summary>
    public string Body => _comment.Body ?? string.Empty;

    /// <summary>
    /// Gets the author username.
    /// </summary>
    public string Author => _comment.Author ?? string.Empty;

    /// <summary>
    /// Gets the author display name.
    /// </summary>
    public string? AuthorDisplayName => _comment.AuthorUser?.DisplayName;

    /// <summary>
    /// Gets the update author username.
    /// </summary>
    public string? UpdateAuthor => _comment.UpdateAuthor;

    /// <summary>
    /// Gets the update author display name.
    /// </summary>
    public string? UpdateAuthorDisplayName => _comment.UpdateAuthorUser?.DisplayName;

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset? CreatedAt => _comment.CreatedDate;

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedAt => _comment.UpdatedDate;

    /// <summary>
    /// Gets the visibility group name (if restricted).
    /// </summary>
    public string? VisibilityGroup => _comment.GroupLevel;

    /// <summary>
    /// Gets the visibility role name (if restricted).
    /// </summary>
    public string? VisibilityRole => _comment.RoleLevel;
}
