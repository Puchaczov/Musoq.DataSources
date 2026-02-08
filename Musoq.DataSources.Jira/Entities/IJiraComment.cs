namespace Musoq.DataSources.Jira.Entities;

/// <summary>
/// Interface representing a Jira comment entity.
/// </summary>
public interface IJiraComment
{
    /// <summary>
    /// Gets the comment ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the issue key this comment belongs to.
    /// </summary>
    string IssueKey { get; }

    /// <summary>
    /// Gets the comment body/content.
    /// </summary>
    string Body { get; }

    /// <summary>
    /// Gets the author's username.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Gets the author's display name.
    /// </summary>
    string? AuthorDisplayName { get; }

    /// <summary>
    /// Gets the update author's username.
    /// </summary>
    string? UpdateAuthor { get; }

    /// <summary>
    /// Gets the update author's display name.
    /// </summary>
    string? UpdateAuthorDisplayName { get; }

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    DateTimeOffset? CreatedAt { get; }

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    DateTimeOffset? UpdatedAt { get; }

    /// <summary>
    /// Gets the visibility group (if restricted).
    /// </summary>
    string? VisibilityGroup { get; }

    /// <summary>
    /// Gets the visibility role (if restricted).
    /// </summary>
    string? VisibilityRole { get; }
}
