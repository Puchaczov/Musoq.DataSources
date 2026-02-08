using Atlassian.Jira;

namespace Musoq.DataSources.Jira.Entities;

/// <summary>
/// Represents a Jira issue entity for querying.
/// </summary>
public class IssueEntity : IJiraIssue
{
    private readonly Issue _issue;

    /// <summary>
    /// Initializes a new instance of the IssueEntity class.
    /// </summary>
    /// <param name="issue">The underlying Atlassian.SDK issue.</param>
    public IssueEntity(Issue issue)
    {
        _issue = issue;
    }

    /// <summary>
    /// Gets the issue key (e.g., PROJ-123).
    /// </summary>
    public string Key => _issue.Key?.Value ?? string.Empty;

    /// <summary>
    /// Gets the issue ID (internal Jira ID).
    /// </summary>
    public string Id => _issue.JiraIdentifier ?? string.Empty;

    /// <summary>
    /// Gets the issue summary/title.
    /// </summary>
    public string Summary => _issue.Summary ?? string.Empty;

    /// <summary>
    /// Gets the issue description.
    /// </summary>
    public string? Description => _issue.Description;

    /// <summary>
    /// Gets the issue type name (e.g., Bug, Story, Task).
    /// </summary>
    public string Type => _issue.Type?.Name ?? string.Empty;

    /// <summary>
    /// Gets the issue status name.
    /// </summary>
    public string Status => _issue.Status?.Name ?? string.Empty;

    /// <summary>
    /// Gets the issue priority name.
    /// </summary>
    public string? Priority => _issue.Priority?.Name;

    /// <summary>
    /// Gets the issue resolution name.
    /// </summary>
    public string? Resolution => _issue.Resolution?.Name;

    /// <summary>
    /// Gets the assignee's username.
    /// </summary>
    public string? Assignee => _issue.Assignee;

    /// <summary>
    /// Gets the assignee's display name.
    /// </summary>
    public string? AssigneeDisplayName => _issue.AssigneeUser?.DisplayName;

    /// <summary>
    /// Gets the reporter's username.
    /// </summary>
    public string? Reporter => _issue.Reporter;

    /// <summary>
    /// Gets the reporter's display name.
    /// </summary>
    public string? ReporterDisplayName => _issue.ReporterUser?.DisplayName;

    /// <summary>
    /// Gets the project key.
    /// </summary>
    public string ProjectKey => _issue.Project ?? string.Empty;

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset? CreatedAt => _issue.Created;

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedAt => _issue.Updated;

    /// <summary>
    /// Gets the resolution date.
    /// </summary>
    public DateTimeOffset? ResolvedAt => _issue.ResolutionDate;

    /// <summary>
    /// Gets the due date.
    /// </summary>
    public DateTime? DueDate => _issue.DueDate;

    /// <summary>
    /// Gets the labels as comma-separated string.
    /// </summary>
    public string Labels => _issue.Labels != null ? string.Join(", ", _issue.Labels) : string.Empty;

    /// <summary>
    /// Gets the components as comma-separated string.
    /// </summary>
    public string Components => string.Join(", ", _issue.Components?.Select(c => c.Name) ?? []);

    /// <summary>
    /// Gets the fix versions as comma-separated string.
    /// </summary>
    public string FixVersions => string.Join(", ", _issue.FixVersions?.Select(v => v.Name) ?? []);

    /// <summary>
    /// Gets the affected versions as comma-separated string.
    /// </summary>
    public string AffectsVersions => string.Join(", ", _issue.AffectsVersions?.Select(v => v.Name) ?? []);

    /// <summary>
    /// Gets the original time estimate in seconds.
    /// </summary>
    public long? OriginalEstimateSeconds => _issue.TimeTrackingData?.OriginalEstimateInSeconds;

    /// <summary>
    /// Gets the remaining time estimate in seconds.
    /// </summary>
    public long? RemainingEstimateSeconds => _issue.TimeTrackingData?.RemainingEstimateInSeconds;

    /// <summary>
    /// Gets the time spent in seconds.
    /// </summary>
    public long? TimeSpentSeconds => _issue.TimeTrackingData?.TimeSpentInSeconds;

    /// <summary>
    /// Gets the original time estimate as formatted string.
    /// </summary>
    public string? OriginalEstimate => _issue.TimeTrackingData?.OriginalEstimate;

    /// <summary>
    /// Gets the remaining time estimate as formatted string.
    /// </summary>
    public string? RemainingEstimate => _issue.TimeTrackingData?.RemainingEstimate;

    /// <summary>
    /// Gets the time spent as formatted string.
    /// </summary>
    public string? TimeSpent => _issue.TimeTrackingData?.TimeSpent;

    /// <summary>
    /// Gets the parent issue key (for subtasks).
    /// </summary>
    public string? ParentKey => _issue.ParentIssueKey;

    /// <summary>
    /// Gets the environment description.
    /// </summary>
    public string? Environment => _issue.Environment;

    /// <summary>
    /// Gets the number of votes.
    /// </summary>
    public long? Votes => _issue.Votes;

    /// <summary>
    /// Gets the security level name.
    /// </summary>
    public string? SecurityLevel => _issue.SecurityLevel?.Name;

    /// <summary>
    /// Gets the issue URL.
    /// </summary>
    public string Url => _issue.JiraIdentifier != null 
        ? $"{_issue.Jira?.Url}/browse/{_issue.Key?.Value}" 
        : string.Empty;

    /// <summary>
    /// Gets the underlying issue for custom field access.
    /// </summary>
    internal Issue UnderlyingIssue => _issue;
}
