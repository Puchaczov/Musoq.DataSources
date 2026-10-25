using Octokit;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
///     Represents a GitHub issue entity for querying.
/// </summary>
public class IssueEntity
{
    private readonly Issue _issue;

    /// <summary>
    ///     Initializes a new instance of the IssueEntity class.
    /// </summary>
    /// <param name="issue">The underlying Octokit issue.</param>
    public IssueEntity(Issue issue)
    {
        _issue = issue;
    }

    /// <summary>
    ///     Gets the issue ID.
    /// </summary>
    public long Id => _issue.Id;

    /// <summary>
    ///     Gets the issue number.
    /// </summary>
    public int Number => _issue.Number;

    /// <summary>
    ///     Gets the issue title.
    /// </summary>
    public string Title => ReadTitle(_issue);

    /// <summary>
    ///     Gets the issue body/description.
    /// </summary>
    public string? Body => ReadBody(_issue);

    /// <summary>
    ///     Gets the issue state (open/closed).
    /// </summary>
    public string State => ReadState(_issue);

    /// <summary>
    ///     Gets the issue URL.
    /// </summary>
    public string Url => ReadUrl(_issue);

    /// <summary>
    ///     Gets the author's login name.
    /// </summary>
    public string AuthorLogin => ReadAuthorLogin(_issue);

    /// <summary>
    ///     Gets the author's ID.
    /// </summary>
    public long? AuthorId => _issue.User?.Id;

    /// <summary>
    ///     Gets the assignee's login name.
    /// </summary>
    public string? AssigneeLogin => ReadAssigneeLogin(_issue);

    /// <summary>
    ///     Gets all assignees' login names as comma-separated string.
    /// </summary>
    public string Assignees => ReadAssignees(_issue);

    /// <summary>
    ///     Gets the labels as comma-separated string.
    /// </summary>
    public string Labels => ReadLabels(_issue);

    /// <summary>
    ///     Gets the label names.
    /// </summary>
    [BindablePropertyAsTable]
    public IReadOnlyList<string> LabelNames => _issue.Labels?.Select(l => l.Name).ToList() ?? [];

    /// <summary>
    ///     Gets the milestone title.
    /// </summary>
    public string? MilestoneTitle => ReadMilestoneTitle(_issue);

    /// <summary>
    ///     Gets the milestone number.
    /// </summary>
    public int? MilestoneNumber => _issue.Milestone?.Number;

    /// <summary>
    ///     Gets the number of comments.
    /// </summary>
    public int Comments => _issue.Comments;

    /// <summary>
    ///     Gets whether the issue is a pull request.
    /// </summary>
    public bool IsPullRequest => _issue.PullRequest != null;

    /// <summary>
    ///     Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt => _issue.CreatedAt;

    /// <summary>
    ///     Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedAt => _issue.UpdatedAt;

    /// <summary>
    ///     Gets the closed date.
    /// </summary>
    public DateTimeOffset? ClosedAt => _issue.ClosedAt;

    /// <summary>
    ///     Gets the user who closed the issue.
    /// </summary>
    public string? ClosedByLogin => ReadClosedByLogin(_issue);

    /// <summary>
    ///     Gets whether the issue is locked.
    /// </summary>
    public bool Locked => _issue.Locked;

    /// <summary>
    ///     Gets the lock reason.
    /// </summary>
    public string? ActiveLockReason => ReadActiveLockReason(_issue);

    /// <summary>
    ///     Gets the repository URL.
    /// </summary>
    public string RepositoryUrl => ReadRepositoryUrl(_issue);

    /// <summary>
    ///     Gets the state reason (completed, not_planned, reopened).
    /// </summary>
    public string? StateReason => ReadStateReason(_issue);

    internal static string ReadTitle(Issue value) => value.Title;
    internal static string? ReadBody(Issue value) => value.Body;
    internal static string ReadState(Issue value) => value.State.StringValue;
    internal static string ReadUrl(Issue value) => value.HtmlUrl;
    internal static string ReadAuthorLogin(Issue value) => value.User?.Login ?? string.Empty;
    internal static string? ReadAssigneeLogin(Issue value) => value.Assignee?.Login;
    internal static string ReadAssignees(Issue value) => string.Join(", ", value.Assignees?.Select(a => a.Login) ?? []);
    internal static string ReadLabels(Issue value) => string.Join(", ", value.Labels?.Select(l => l.Name) ?? []);
    internal static string? ReadMilestoneTitle(Issue value) => value.Milestone?.Title;
    internal static string? ReadClosedByLogin(Issue value) => value.ClosedBy?.Login;
    internal static string? ReadActiveLockReason(Issue value) => value.ActiveLockReason?.StringValue;
    internal static string ReadRepositoryUrl(Issue value) => value.Repository?.HtmlUrl ?? string.Empty;
    internal static string? ReadStateReason(Issue value) => value.StateReason?.StringValue;
}
