using Atlassian.Jira;
using Musoq.DataSources.Jira.Entities;

namespace Musoq.DataSources.Jira;

/// <summary>
/// Jira API implementation using Atlassian.SDK.
/// </summary>
internal class JiraApi : IJiraApi
{
    private readonly Atlassian.Jira.Jira _jira;
    private const int DefaultMaxResults = 50;

    /// <summary>
    /// Initializes a new instance of the JiraApi class.
    /// </summary>
    /// <param name="jiraUrl">Jira instance URL</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="apiToken">API token for authentication</param>
    public JiraApi(string jiraUrl, string username, string apiToken)
    {
        _jira = Atlassian.Jira.Jira.CreateRestClient(jiraUrl, username, apiToken);
    }

    /// <summary>
    /// Initializes a new instance of the JiraApi class with a pre-configured Jira client.
    /// For testing purposes.
    /// </summary>
    /// <param name="jira">Pre-configured Jira client</param>
    internal JiraApi(Atlassian.Jira.Jira jira)
    {
        _jira = jira;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraIssue>> GetIssuesAsync(string jql, int maxResults = DefaultMaxResults, int startAt = 0)
    {
        var issues = await _jira.Issues.GetIssuesFromJqlAsync(jql, maxResults, startAt);
        return issues.Select(i => (IJiraIssue)new IssueEntity(i)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraIssue>> GetIssuesForProjectAsync(
        string projectKey, 
        string? additionalJql = null, 
        int maxResults = DefaultMaxResults, 
        int startAt = 0)
    {
        var jql = $"project = {projectKey}";
        
        if (!string.IsNullOrEmpty(additionalJql))
        {
            jql += $" AND ({additionalJql})";
        }

        return await GetIssuesAsync(jql, maxResults, startAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraProject>> GetProjectsAsync()
    {
        var projects = await _jira.Projects.GetProjectsAsync();
        return projects.Select(p => (IJiraProject)new ProjectEntity(p)).ToList();
    }

    /// <inheritdoc />
    public async Task<IJiraProject?> GetProjectAsync(string projectKey)
    {
        try
        {
            var project = await _jira.Projects.GetProjectAsync(projectKey);
            return project != null ? new ProjectEntity(project) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraComment>> GetCommentsAsync(string issueKey)
    {
        var issue = await _jira.Issues.GetIssueAsync(issueKey);
        var comments = await issue.GetCommentsAsync();
        return comments.Select(c => (IJiraComment)new CommentEntity(c, issueKey)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraComment>> GetCommentsForIssuesAsync(string jql, int maxIssues = 100)
    {
        var issues = await _jira.Issues.GetIssuesFromJqlAsync(jql, maxIssues);
        var allComments = new List<IJiraComment>();

        foreach (var issue in issues)
        {
            var comments = await issue.GetCommentsAsync();
            allComments.AddRange(comments.Select(c => (IJiraComment)new CommentEntity(c, issue.Key.Value)));
        }

        return allComments;
    }

    /// <inheritdoc />
    public async Task<int> GetIssueCountAsync(string jql)
    {
        var result = await _jira.Issues.GetIssuesFromJqlAsync(jql, 0);
        return result.TotalItems;
    }
}
