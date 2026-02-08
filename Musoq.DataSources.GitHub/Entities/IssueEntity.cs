using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
/// Represents a GitHub issue entity for querying.
/// </summary>
public class IssueEntity
{
    private readonly Issue _issue;

    /// <summary>
    /// Initializes a new instance of the IssueEntity class.
    /// </summary>
    /// <param name="issue">The underlying Octokit issue.</param>
    public IssueEntity(Issue issue)
    {
        _issue = issue;
    }

    /// <summary>
    /// Gets the issue ID.
    /// </summary>
    public long Id => _issue.Id;

    /// <summary>
    /// Gets the issue number.
    /// </summary>
    public int Number => _issue.Number;

    /// <summary>
    /// Gets the issue title.
    /// </summary>
    public string Title => _issue.Title;

    /// <summary>
    /// Gets the issue body/description.
    /// </summary>
    public string? Body => _issue.Body;

    /// <summary>
    /// Gets the issue state (open/closed).
    /// </summary>
    public string State => _issue.State.StringValue;

    /// <summary>
    /// Gets the issue URL.
    /// </summary>
    public string Url => _issue.HtmlUrl;

    /// <summary>
    /// Gets the author's login name.
    /// </summary>
    public string AuthorLogin => _issue.User?.Login ?? string.Empty;

    /// <summary>
    /// Gets the author's ID.
    /// </summary>
    public long? AuthorId => _issue.User?.Id;

    /// <summary>
    /// Gets the assignee's login name.
    /// </summary>
    public string? AssigneeLogin => _issue.Assignee?.Login;

    /// <summary>
    /// Gets all assignees' login names as comma-separated string.
    /// </summary>
    public string Assignees => string.Join(", ", _issue.Assignees?.Select(a => a.Login) ?? []);

    /// <summary>
    /// Gets the labels as comma-separated string.
    /// </summary>
    public string Labels => string.Join(", ", _issue.Labels?.Select(l => l.Name) ?? []);

    /// <summary>
    /// Gets the label names.
    /// </summary>
    public IReadOnlyList<string> LabelNames => _issue.Labels?.Select(l => l.Name).ToList() ?? [];

    /// <summary>
    /// Gets the milestone title.
    /// </summary>
    public string? MilestoneTitle => _issue.Milestone?.Title;

    /// <summary>
    /// Gets the milestone number.
    /// </summary>
    public int? MilestoneNumber => _issue.Milestone?.Number;

    /// <summary>
    /// Gets the number of comments.
    /// </summary>
    public int Comments => _issue.Comments;

    /// <summary>
    /// Gets whether the issue is a pull request.
    /// </summary>
    public bool IsPullRequest => _issue.PullRequest != null;

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt => _issue.CreatedAt;

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedAt => _issue.UpdatedAt;

    /// <summary>
    /// Gets the closed date.
    /// </summary>
    public DateTimeOffset? ClosedAt => _issue.ClosedAt;

    /// <summary>
    /// Gets the user who closed the issue.
    /// </summary>
    public string? ClosedByLogin => _issue.ClosedBy?.Login;

    /// <summary>
    /// Gets whether the issue is locked.
    /// </summary>
    public bool Locked => _issue.Locked;

    /// <summary>
    /// Gets the lock reason.
    /// </summary>
    public string? ActiveLockReason => _issue.ActiveLockReason?.StringValue;

    /// <summary>
    /// Gets the repository URL.
    /// </summary>
    public string RepositoryUrl => _issue.Repository?.HtmlUrl ?? string.Empty;

    /// <summary>
    /// Gets the state reason (completed, not_planned, reopened).
    /// </summary>
    public string? StateReason => _issue.StateReason?.StringValue;
}
