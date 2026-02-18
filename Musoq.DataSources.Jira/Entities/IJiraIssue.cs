namespace Musoq.DataSources.Jira.Entities;

/// <summary>
///     Interface representing a Jira issue entity.
/// </summary>
public interface IJiraIssue
{
    /// <summary>
    ///     Gets the issue key (e.g., PROJ-123).
    /// </summary>
    string Key { get; }

    /// <summary>
    ///     Gets the issue ID (internal Jira ID).
    /// </summary>
    string Id { get; }

    /// <summary>
    ///     Gets the issue summary/title.
    /// </summary>
    string Summary { get; }

    /// <summary>
    ///     Gets the issue description.
    /// </summary>
    string? Description { get; }

    /// <summary>
    ///     Gets the issue type name (e.g., Bug, Story, Task).
    /// </summary>
    string Type { get; }

    /// <summary>
    ///     Gets the issue status name.
    /// </summary>
    string Status { get; }

    /// <summary>
    ///     Gets the issue priority name.
    /// </summary>
    string? Priority { get; }

    /// <summary>
    ///     Gets the issue resolution name.
    /// </summary>
    string? Resolution { get; }

    /// <summary>
    ///     Gets the assignee's username.
    /// </summary>
    string? Assignee { get; }

    /// <summary>
    ///     Gets the assignee's display name.
    /// </summary>
    string? AssigneeDisplayName { get; }

    /// <summary>
    ///     Gets the reporter's username.
    /// </summary>
    string? Reporter { get; }

    /// <summary>
    ///     Gets the reporter's display name.
    /// </summary>
    string? ReporterDisplayName { get; }

    /// <summary>
    ///     Gets the project key.
    /// </summary>
    string ProjectKey { get; }

    /// <summary>
    ///     Gets the creation date.
    /// </summary>
    DateTimeOffset? CreatedAt { get; }

    /// <summary>
    ///     Gets the last update date.
    /// </summary>
    DateTimeOffset? UpdatedAt { get; }

    /// <summary>
    ///     Gets the resolution date.
    /// </summary>
    DateTimeOffset? ResolvedAt { get; }

    /// <summary>
    ///     Gets the due date.
    /// </summary>
    DateTime? DueDate { get; }

    /// <summary>
    ///     Gets the labels as comma-separated string.
    /// </summary>
    string Labels { get; }

    /// <summary>
    ///     Gets the components as comma-separated string.
    /// </summary>
    string Components { get; }

    /// <summary>
    ///     Gets the fix versions as comma-separated string.
    /// </summary>
    string FixVersions { get; }

    /// <summary>
    ///     Gets the affected versions as comma-separated string.
    /// </summary>
    string AffectsVersions { get; }

    /// <summary>
    ///     Gets the original time estimate in seconds.
    /// </summary>
    long? OriginalEstimateSeconds { get; }

    /// <summary>
    ///     Gets the remaining time estimate in seconds.
    /// </summary>
    long? RemainingEstimateSeconds { get; }

    /// <summary>
    ///     Gets the time spent in seconds.
    /// </summary>
    long? TimeSpentSeconds { get; }

    /// <summary>
    ///     Gets the original time estimate as formatted string.
    /// </summary>
    string? OriginalEstimate { get; }

    /// <summary>
    ///     Gets the remaining time estimate as formatted string.
    /// </summary>
    string? RemainingEstimate { get; }

    /// <summary>
    ///     Gets the time spent as formatted string.
    /// </summary>
    string? TimeSpent { get; }

    /// <summary>
    ///     Gets the parent issue key (for subtasks).
    /// </summary>
    string? ParentKey { get; }

    /// <summary>
    ///     Gets the environment description.
    /// </summary>
    string? Environment { get; }

    /// <summary>
    ///     Gets the number of votes.
    /// </summary>
    long? Votes { get; }

    /// <summary>
    ///     Gets the security level name.
    /// </summary>
    string? SecurityLevel { get; }

    /// <summary>
    ///     Gets the issue URL.
    /// </summary>
    string Url { get; }
}