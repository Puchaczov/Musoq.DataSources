using Musoq.DataSources.Jira.Entities;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Jira;

/// <summary>
///     Helper methods for use in Jira queries.
/// </summary>
public class JiraLibrary : LibraryBase
{
    /// <summary>
    ///     Checks if labels contain a specific label.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <param name="label">Label to search for</param>
    /// <returns>True if the label exists</returns>
    [BindableMethod]
    public bool HasLabel([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity, string label)
    {
        if (string.IsNullOrEmpty(entity.Labels))
            return false;

        var labelList =
            entity.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return labelList.Contains(label, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Checks if components contain a specific component.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <param name="component">Component to search for</param>
    /// <returns>True if the component exists</returns>
    [BindableMethod]
    public bool HasComponent([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity, string component)
    {
        if (string.IsNullOrEmpty(entity.Components))
            return false;

        var componentList =
            entity.Components.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return componentList.Contains(component, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Checks if fix versions contain a specific version.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <param name="version">Version to search for</param>
    /// <returns>True if the version exists</returns>
    [BindableMethod]
    public bool HasFixVersion([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity, string version)
    {
        if (string.IsNullOrEmpty(entity.FixVersions))
            return false;

        var versionList =
            entity.FixVersions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return versionList.Contains(version, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets a custom field value from an issue.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <param name="fieldName">Custom field name</param>
    /// <returns>Field value as string, or null if not found</returns>
    [BindableMethod]
    public string? GetCustomField([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity, string fieldName)
    {
        try
        {
            var value = entity.UnderlyingIssue[fieldName];
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Checks if an issue is a subtask.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <returns>True if the issue is a subtask</returns>
    [BindableMethod]
    public bool IsSubtask([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity)
    {
        return !string.IsNullOrEmpty(entity.ParentKey);
    }

    /// <summary>
    ///     Checks if an issue is overdue.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <returns>True if the issue is overdue</returns>
    [BindableMethod]
    public bool IsOverdue([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity)
    {
        if (!entity.DueDate.HasValue)
            return false;


        return entity.DueDate.Value < DateTime.Today && string.IsNullOrEmpty(entity.Resolution);
    }

    /// <summary>
    ///     Gets the age of an issue in days.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <returns>Age in days</returns>
    [BindableMethod]
    public int GetAgeInDays([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity)
    {
        if (!entity.CreatedAt.HasValue)
            return 0;

        return (int)(DateTimeOffset.UtcNow - entity.CreatedAt.Value).TotalDays;
    }

    /// <summary>
    ///     Gets the time to resolution in days.
    /// </summary>
    /// <param name="entity">Issue entity</param>
    /// <returns>Days to resolution, or null if not resolved</returns>
    [BindableMethod]
    public int? GetTimeToResolutionDays([InjectSpecificSource(typeof(IssueEntity))] IssueEntity entity)
    {
        if (!entity.CreatedAt.HasValue || !entity.ResolvedAt.HasValue)
            return null;

        return (int)(entity.ResolvedAt.Value - entity.CreatedAt.Value).TotalDays;
    }

    /// <summary>
    ///     Converts time in seconds to a formatted duration string.
    /// </summary>
    /// <param name="seconds">Time in seconds</param>
    /// <returns>Formatted duration (e.g., "2h 30m")</returns>
    [BindableMethod]
    public string? FormatDuration(long? seconds)
    {
        if (!seconds.HasValue)
            return null;

        var timeSpan = TimeSpan.FromSeconds(seconds.Value);

        if (timeSpan.TotalDays >= 1) return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";

        if (timeSpan.TotalHours >= 1) return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";

        return $"{(int)timeSpan.TotalMinutes}m";
    }

    /// <summary>
    ///     Extracts the project key from an issue key.
    /// </summary>
    /// <param name="issueKey">Issue key (e.g., PROJ-123)</param>
    /// <returns>Project key (e.g., PROJ)</returns>
    [BindableMethod]
    public string? ExtractProjectKey(string? issueKey)
    {
        if (string.IsNullOrEmpty(issueKey))
            return null;

        var dashIndex = issueKey.IndexOf('-');
        return dashIndex > 0 ? issueKey[..dashIndex] : null;
    }

    /// <summary>
    ///     Extracts the issue number from an issue key.
    /// </summary>
    /// <param name="issueKey">Issue key (e.g., PROJ-123)</param>
    /// <returns>Issue number (e.g., 123)</returns>
    [BindableMethod]
    public int? ExtractIssueNumber(string? issueKey)
    {
        if (string.IsNullOrEmpty(issueKey))
            return null;

        var dashIndex = issueKey.IndexOf('-');
        if (dashIndex < 0 || dashIndex >= issueKey.Length - 1)
            return null;

        return int.TryParse(issueKey[(dashIndex + 1)..], out var number) ? number : null;
    }
}