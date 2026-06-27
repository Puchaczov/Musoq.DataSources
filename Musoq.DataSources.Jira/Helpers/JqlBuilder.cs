using System.Globalization;

namespace Musoq.DataSources.Jira.Helpers;

/// <summary>
///     Parameters used to build Jira API queries.
/// </summary>
internal class JiraFilterParameters
{
    /// <summary>
    ///     Gets or sets the status filter.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    ///     Gets or sets the issue type filter.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     Gets or sets the priority filter.
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    ///     Gets or sets the resolution filter.
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    ///     Gets or sets the assignee filter.
    /// </summary>
    public string? Assignee { get; set; }

    /// <summary>
    ///     Gets or sets the assignee display-name filter.
    /// </summary>
    public string? AssigneeDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets the reporter filter.
    /// </summary>
    public string? Reporter { get; set; }

    /// <summary>
    ///     Gets or sets the reporter display-name filter.
    /// </summary>
    public string? ReporterDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets the project key filter.
    /// </summary>
    public string? ProjectKey { get; set; }

    /// <summary>
    ///     Gets or sets the issue key filter.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    ///     Gets or sets labels to filter by.
    /// </summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>
    ///     Gets or sets components to filter by.
    /// </summary>
    public List<string> Components { get; set; } = [];

    /// <summary>
    ///     Gets or sets the created date range start.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the created date range start is inclusive.
    /// </summary>
    public bool CreatedAfterInclusive { get; set; } = true;

    /// <summary>
    ///     Gets or sets the created date range end.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the created date range end is inclusive.
    /// </summary>
    public bool CreatedBeforeInclusive { get; set; } = true;

    /// <summary>
    ///     Gets or sets the updated date range start.
    /// </summary>
    public DateTimeOffset? UpdatedAfter { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the updated date range start is inclusive.
    /// </summary>
    public bool UpdatedAfterInclusive { get; set; } = true;

    /// <summary>
    ///     Gets or sets the updated date range end.
    /// </summary>
    public DateTimeOffset? UpdatedBefore { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the updated date range end is inclusive.
    /// </summary>
    public bool UpdatedBeforeInclusive { get; set; } = true;

    /// <summary>
    ///     Gets or sets the parent issue key (for subtasks).
    /// </summary>
    public string? ParentKey { get; set; }

    /// <summary>
    ///     Gets or sets the fix version filter.
    /// </summary>
    public string? FixVersion { get; set; }

    /// <summary>
    ///     Gets or sets the text search query.
    /// </summary>
    public string? TextSearch { get; set; }

    /// <summary>
    ///     Gets or sets the summary search query.
    /// </summary>
    public string? SummaryContains { get; set; }

    public void SetCreatedAfter(DateTimeOffset value, bool inclusive)
    {
        (CreatedAfter, CreatedAfterInclusive) = SelectLower(CreatedAfter, CreatedAfterInclusive, value, inclusive);
    }

    public void SetCreatedBefore(DateTimeOffset value, bool inclusive)
    {
        (CreatedBefore, CreatedBeforeInclusive) = SelectUpper(CreatedBefore, CreatedBeforeInclusive, value, inclusive);
    }

    public void SetUpdatedAfter(DateTimeOffset value, bool inclusive)
    {
        (UpdatedAfter, UpdatedAfterInclusive) = SelectLower(UpdatedAfter, UpdatedAfterInclusive, value, inclusive);
    }

    public void SetUpdatedBefore(DateTimeOffset value, bool inclusive)
    {
        (UpdatedBefore, UpdatedBeforeInclusive) = SelectUpper(UpdatedBefore, UpdatedBeforeInclusive, value, inclusive);
    }

    private static (DateTimeOffset? Value, bool Inclusive) SelectLower(
        DateTimeOffset? target,
        bool targetInclusive,
        DateTimeOffset value,
        bool inclusive)
    {
        if (target is null || value > target.Value || value == target.Value && !inclusive)
            return (value, inclusive);

        return (target, targetInclusive);
    }

    private static (DateTimeOffset? Value, bool Inclusive) SelectUpper(
        DateTimeOffset? target,
        bool targetInclusive,
        DateTimeOffset value,
        bool inclusive)
    {
        if (target is null || value < target.Value || value == target.Value && !inclusive)
            return (value, inclusive);

        return (target, targetInclusive);
    }
}

/// <summary>
///     Helper class to build JQL queries from filter parameters.
/// </summary>
internal static class JqlBuilder
{
    /// <summary>
    ///     Builds a JQL query string from filter parameters.
    /// </summary>
    /// <param name="baseJql">Optional base JQL to extend (e.g., "project = PROJ")</param>
    /// <param name="parameters">Filter parameters to include in the JQL query.</param>
    /// <returns>Complete JQL query string</returns>
    public static string BuildJql(string? baseJql, JiraFilterParameters parameters)
    {
        var conditions = new List<string>();
        var orderBy = string.Empty;

        if (!string.IsNullOrWhiteSpace(baseJql))
        {
            var orderByIndex = CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                baseJql,
                "order by",
                CompareOptions.IgnoreCase);

            if (orderByIndex >= 0)
            {
                orderBy = baseJql[orderByIndex..].Trim();
                baseJql = baseJql[..orderByIndex].Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(baseJql)) conditions.Add(baseJql);

        if (!string.IsNullOrEmpty(parameters.Status)) conditions.Add($"status = \"{EscapeJql(parameters.Status)}\"");

        if (!string.IsNullOrEmpty(parameters.Type)) conditions.Add($"issuetype = \"{EscapeJql(parameters.Type)}\"");

        if (!string.IsNullOrEmpty(parameters.Priority))
            conditions.Add($"priority = \"{EscapeJql(parameters.Priority)}\"");

        if (!string.IsNullOrEmpty(parameters.Resolution))
            conditions.Add($"resolution = \"{EscapeJql(parameters.Resolution)}\"");

        if (!string.IsNullOrEmpty(parameters.Assignee))
        {
            if (parameters.Assignee.Equals("unassigned", StringComparison.OrdinalIgnoreCase) ||
                parameters.Assignee.Equals("null", StringComparison.OrdinalIgnoreCase))
                conditions.Add("assignee is EMPTY");
            else
                conditions.Add($"assignee = \"{EscapeJql(parameters.Assignee)}\"");
        }

        if (!string.IsNullOrEmpty(parameters.AssigneeDisplayName))
            conditions.Add($"assignee = \"{EscapeJql(parameters.AssigneeDisplayName)}\"");

        if (!string.IsNullOrEmpty(parameters.Reporter))
            conditions.Add($"reporter = \"{EscapeJql(parameters.Reporter)}\"");

        if (!string.IsNullOrEmpty(parameters.ReporterDisplayName))
            conditions.Add($"reporter = \"{EscapeJql(parameters.ReporterDisplayName)}\"");

        if (!string.IsNullOrEmpty(parameters.ProjectKey)) conditions.Add($"project = {parameters.ProjectKey}");

        if (!string.IsNullOrEmpty(parameters.Key)) conditions.Add($"key = {parameters.Key}");

        if (!string.IsNullOrEmpty(parameters.ParentKey)) conditions.Add($"parent = {parameters.ParentKey}");

        if (!string.IsNullOrEmpty(parameters.FixVersion))
            conditions.Add($"fixVersion = \"{EscapeJql(parameters.FixVersion)}\"");

        foreach (var label in parameters.Labels) conditions.Add($"labels = \"{EscapeJql(label)}\"");

        foreach (var component in parameters.Components) conditions.Add($"component = \"{EscapeJql(component)}\"");

        if (parameters.CreatedAfter.HasValue)
            conditions.Add(
                $"created {FormatLowerOperator(parameters.CreatedAfterInclusive)} \"{FormatDate(parameters.CreatedAfter.Value)}\"");

        if (parameters.CreatedBefore.HasValue)
            conditions.Add(
                $"created {FormatUpperOperator(parameters.CreatedBeforeInclusive)} \"{FormatDate(parameters.CreatedBefore.Value)}\"");

        if (parameters.UpdatedAfter.HasValue)
            conditions.Add(
                $"updated {FormatLowerOperator(parameters.UpdatedAfterInclusive)} \"{FormatDate(parameters.UpdatedAfter.Value)}\"");

        if (parameters.UpdatedBefore.HasValue)
            conditions.Add(
                $"updated {FormatUpperOperator(parameters.UpdatedBeforeInclusive)} \"{FormatDate(parameters.UpdatedBefore.Value)}\"");

        if (!string.IsNullOrEmpty(parameters.SummaryContains))
            conditions.Add($"summary ~ \"{EscapeJql(parameters.SummaryContains)}\"");

        if (!string.IsNullOrEmpty(parameters.TextSearch))
            conditions.Add($"text ~ \"{EscapeJql(parameters.TextSearch)}\"");

        var conditionsJql = conditions.Count > 0
            ? string.Join(" AND ", conditions)
            : string.Empty;

        return (conditionsJql, orderBy) switch
        {
            ("", "") => "order by created DESC",
            ("", _) => orderBy,
            (_, "") => conditionsJql,
            _ => $"{conditionsJql} {orderBy}"
        };
    }

    private static string EscapeJql(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string FormatDate(DateTimeOffset date)
    {
        return date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatLowerOperator(bool inclusive)
    {
        return inclusive ? ">=" : ">";
    }

    private static string FormatUpperOperator(bool inclusive)
    {
        return inclusive ? "<=" : "<";
    }
}
