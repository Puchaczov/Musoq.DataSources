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
    public string Key => ReadKey(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue ID (internal Jira ID).
    /// </summary>
    public string Id => ReadId(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue summary/title.
    /// </summary>
    public string Summary => ReadSummary(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue description.
    /// </summary>
    public string? Description => ReadDescription(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue type name (e.g., Bug, Story, Task).
    /// </summary>
    public string Type => ReadType(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue status name.
    /// </summary>
    public string Status => ReadStatus(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue priority name.
    /// </summary>
    public string? Priority => ReadPriority(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue resolution name.
    /// </summary>
    public string? Resolution => ReadResolution(UnderlyingIssue);

    /// <summary>
    ///     Gets the assignee's username.
    /// </summary>
    public string? Assignee => ReadAssignee(UnderlyingIssue);

    /// <summary>
    ///     Gets the assignee's display name.
    /// </summary>
    public string? AssigneeDisplayName => ReadAssigneeDisplayName(UnderlyingIssue);

    /// <summary>
    ///     Gets the reporter's username.
    /// </summary>
    public string? Reporter => ReadReporter(UnderlyingIssue);

    /// <summary>
    ///     Gets the reporter's display name.
    /// </summary>
    public string? ReporterDisplayName => ReadReporterDisplayName(UnderlyingIssue);

    /// <summary>
    ///     Gets the project key.
    /// </summary>
    public string ProjectKey => ReadProjectKey(UnderlyingIssue);

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
    public string Labels => ReadLabels(UnderlyingIssue);

    /// <summary>
    ///     Gets the components as comma-separated string.
    /// </summary>
    public string Components => ReadComponents(UnderlyingIssue);

    /// <summary>
    ///     Gets the fix versions as comma-separated string.
    /// </summary>
    public string FixVersions => ReadFixVersions(UnderlyingIssue);

    /// <summary>
    ///     Gets the affected versions as comma-separated string.
    /// </summary>
    public string AffectsVersions => ReadAffectsVersions(UnderlyingIssue);

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
    public string? OriginalEstimate => ReadOriginalEstimate(UnderlyingIssue);

    /// <summary>
    ///     Gets the remaining time estimate as formatted string.
    /// </summary>
    public string? RemainingEstimate => ReadRemainingEstimate(UnderlyingIssue);

    /// <summary>
    ///     Gets the time spent as formatted string.
    /// </summary>
    public string? TimeSpent => ReadTimeSpent(UnderlyingIssue);

    /// <summary>
    ///     Gets the parent issue key (for subtasks).
    /// </summary>
    public string? ParentKey => ReadParentKey(UnderlyingIssue);

    /// <summary>
    ///     Gets the environment description.
    /// </summary>
    public string? Environment => ReadEnvironment(UnderlyingIssue);

    /// <summary>
    ///     Gets the number of votes.
    /// </summary>
    public long? Votes => UnderlyingIssue.Votes;

    /// <summary>
    ///     Gets the security level name.
    /// </summary>
    public string? SecurityLevel => ReadSecurityLevel(UnderlyingIssue);

    /// <summary>
    ///     Gets the issue URL.
    /// </summary>
    public string Url => ReadUrl(UnderlyingIssue);

    internal static string ReadKey(Issue value) => value.Key?.Value ?? string.Empty;
    internal static string ReadId(Issue value) => value.JiraIdentifier ?? string.Empty;
    internal static string ReadSummary(Issue value) => value.Summary ?? string.Empty;
    internal static string? ReadDescription(Issue value) => value.Description;
    internal static string ReadType(Issue value) => value.Type?.Name ?? string.Empty;
    internal static string ReadStatus(Issue value) => value.Status?.Name ?? string.Empty;
    internal static string? ReadPriority(Issue value) => value.Priority?.Name;
    internal static string? ReadResolution(Issue value) => value.Resolution?.Name;
    internal static string? ReadAssignee(Issue value) => value.Assignee;
    internal static string? ReadAssigneeDisplayName(Issue value) => value.AssigneeUser?.DisplayName;
    internal static string? ReadReporter(Issue value) => value.Reporter;
    internal static string? ReadReporterDisplayName(Issue value) => value.ReporterUser?.DisplayName;
    internal static string ReadProjectKey(Issue value) => value.Project ?? string.Empty;
    internal static string ReadLabels(Issue value) => value.Labels != null ? string.Join(", ", value.Labels) : string.Empty;
    internal static string ReadComponents(Issue value) => string.Join(", ", value.Components?.Select(c => c.Name) ?? []);
    internal static string ReadFixVersions(Issue value) => string.Join(", ", value.FixVersions?.Select(v => v.Name) ?? []);
    internal static string ReadAffectsVersions(Issue value) => string.Join(", ", value.AffectsVersions?.Select(v => v.Name) ?? []);
    internal static string? ReadOriginalEstimate(Issue value) => value.TimeTrackingData?.OriginalEstimate;
    internal static string? ReadRemainingEstimate(Issue value) => value.TimeTrackingData?.RemainingEstimate;
    internal static string? ReadTimeSpent(Issue value) => value.TimeTrackingData?.TimeSpent;
    internal static string? ReadParentKey(Issue value) => value.ParentIssueKey;
    internal static string? ReadEnvironment(Issue value) => value.Environment;
    internal static string? ReadSecurityLevel(Issue value) => value.SecurityLevel?.Name;
    internal static string ReadUrl(Issue value) => value.JiraIdentifier != null
        ? $"{value.Jira?.Url}/browse/{value.Key?.Value}"
        : string.Empty;
}