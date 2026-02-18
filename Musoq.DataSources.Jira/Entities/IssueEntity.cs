using Atlassian.Jira;

namespace Musoq.DataSources.Jira.Entities;

/// <summary>
///     Represents a Jira issue entity for querying.
/// </summary>
public class IssueEntity : IJiraIssue
{
    /// <summary>
    ///     Initializes a new instance of the IssueEntity class.
    /// </summary>
    /// <param name="issue">The underlying Atlassian.SDK issue.</param>
    public IssueEntity(Issue issue)
    {
        UnderlyingIssue = issue;
    }

    /// <summary>
    ///     Gets the underlying issue for custom field access.
    /// </summary>
    internal Issue UnderlyingIssue { get; }

    /// <summary>
    ///     Gets the issue key (e.g., PROJ-123).
    /// </summary>
    public string Key => UnderlyingIssue.Key?.Value ?? string.Empty;

    /// <summary>
    ///     Gets the issue ID (internal Jira ID).
    /// </summary>
    public string Id => UnderlyingIssue.JiraIdentifier ?? string.Empty;

    /// <summary>
    ///     Gets the issue summary/title.
    /// </summary>
    public string Summary => UnderlyingIssue.Summary ?? string.Empty;

    /// <summary>
    ///     Gets the issue description.
    /// </summary>
    public string? Description => UnderlyingIssue.Description;

    /// <summary>
    ///     Gets the issue type name (e.g., Bug, Story, Task).
    /// </summary>
    public string Type => UnderlyingIssue.Type?.Name ?? string.Empty;

    /// <summary>
    ///     Gets the issue status name.
    /// </summary>
    public string Status => UnderlyingIssue.Status?.Name ?? string.Empty;

    /// <summary>
    ///     Gets the issue priority name.
    /// </summary>
    public string? Priority => UnderlyingIssue.Priority?.Name;

    /// <summary>
    ///     Gets the issue resolution name.
    /// </summary>
    public string? Resolution => UnderlyingIssue.Resolution?.Name;

    /// <summary>
    ///     Gets the assignee's username.
    /// </summary>
    public string? Assignee => UnderlyingIssue.Assignee;

    /// <summary>
    ///     Gets the assignee's display name.
    /// </summary>
    public string? AssigneeDisplayName => UnderlyingIssue.AssigneeUser?.DisplayName;

    /// <summary>
    ///     Gets the reporter's username.
    /// </summary>
    public string? Reporter => UnderlyingIssue.Reporter;

    /// <summary>
    ///     Gets the reporter's display name.
    /// </summary>
    public string? ReporterDisplayName => UnderlyingIssue.ReporterUser?.DisplayName;

    /// <summary>
    ///     Gets the project key.
    /// </summary>
    public string ProjectKey => UnderlyingIssue.Project ?? string.Empty;

    /// <summary>
    ///     Gets the creation date.
    /// </summary>
    public DateTimeOffset? CreatedAt => UnderlyingIssue.Created;

    /// <summary>
    ///     Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedAt => UnderlyingIssue.Updated;

    /// <summary>
    ///     Gets the resolution date.
    /// </summary>
    public DateTimeOffset? ResolvedAt => UnderlyingIssue.ResolutionDate;

    /// <summary>
    ///     Gets the due date.
    /// </summary>
    public DateTime? DueDate => UnderlyingIssue.DueDate;

    /// <summary>
    ///     Gets the labels as comma-separated string.
    /// </summary>
    public string Labels => UnderlyingIssue.Labels != null ? string.Join(", ", UnderlyingIssue.Labels) : string.Empty;

    /// <summary>
    ///     Gets the components as comma-separated string.
    /// </summary>
    public string Components => string.Join(", ", UnderlyingIssue.Components?.Select(c => c.Name) ?? []);

    /// <summary>
    ///     Gets the fix versions as comma-separated string.
    /// </summary>
    public string FixVersions => string.Join(", ", UnderlyingIssue.FixVersions?.Select(v => v.Name) ?? []);

    /// <summary>
    ///     Gets the affected versions as comma-separated string.
    /// </summary>
    public string AffectsVersions => string.Join(", ", UnderlyingIssue.AffectsVersions?.Select(v => v.Name) ?? []);

    /// <summary>
    ///     Gets the original time estimate in seconds.
    /// </summary>
    public long? OriginalEstimateSeconds => UnderlyingIssue.TimeTrackingData?.OriginalEstimateInSeconds;

    /// <summary>
    ///     Gets the remaining time estimate in seconds.
    /// </summary>
    public long? RemainingEstimateSeconds => UnderlyingIssue.TimeTrackingData?.RemainingEstimateInSeconds;

    /// <summary>
    ///     Gets the time spent in seconds.
    /// </summary>
    public long? TimeSpentSeconds => UnderlyingIssue.TimeTrackingData?.TimeSpentInSeconds;

    /// <summary>
    ///     Gets the original time estimate as formatted string.
    /// </summary>
    public string? OriginalEstimate => UnderlyingIssue.TimeTrackingData?.OriginalEstimate;

    /// <summary>
    ///     Gets the remaining time estimate as formatted string.
    /// </summary>
    public string? RemainingEstimate => UnderlyingIssue.TimeTrackingData?.RemainingEstimate;

    /// <summary>
    ///     Gets the time spent as formatted string.
    /// </summary>
    public string? TimeSpent => UnderlyingIssue.TimeTrackingData?.TimeSpent;

    /// <summary>
    ///     Gets the parent issue key (for subtasks).
    /// </summary>
    public string? ParentKey => UnderlyingIssue.ParentIssueKey;

    /// <summary>
    ///     Gets the environment description.
    /// </summary>
    public string? Environment => UnderlyingIssue.Environment;

    /// <summary>
    ///     Gets the number of votes.
    /// </summary>
    public long? Votes => UnderlyingIssue.Votes;

    /// <summary>
    ///     Gets the security level name.
    /// </summary>
    public string? SecurityLevel => UnderlyingIssue.SecurityLevel?.Name;

    /// <summary>
    ///     Gets the issue URL.
    /// </summary>
    public string Url => UnderlyingIssue.JiraIdentifier != null
        ? $"{UnderlyingIssue.Jira?.Url}/browse/{UnderlyingIssue.Key?.Value}"
        : string.Empty;
}