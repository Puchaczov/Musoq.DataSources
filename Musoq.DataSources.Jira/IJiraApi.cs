using Musoq.DataSources.Jira.Entities;

namespace Musoq.DataSources.Jira;

/// <summary>
/// Interface for Jira API operations. Abstracted for testability.
/// </summary>
internal interface IJiraApi
{
    /// <summary>
    /// Gets issues using a JQL query.
    /// </summary>
    /// <param name="jql">JQL query string</param>
    /// <param name="maxResults">Maximum results per page</param>
    /// <param name="startAt">Starting index for pagination</param>
    /// <returns>List of issue entities</returns>
    Task<IReadOnlyList<IJiraIssue>> GetIssuesAsync(string jql, int maxResults = 50, int startAt = 0);

    /// <summary>
    /// Gets issues for a specific project.
    /// </summary>
    /// <param name="projectKey">Project key</param>
    /// <param name="additionalJql">Additional JQL filters to apply</param>
    /// <param name="maxResults">Maximum results per page</param>
    /// <param name="startAt">Starting index for pagination</param>
    /// <returns>List of issue entities</returns>
    Task<IReadOnlyList<IJiraIssue>> GetIssuesForProjectAsync(
        string projectKey, 
        string? additionalJql = null, 
        int maxResults = 50, 
        int startAt = 0);

    /// <summary>
    /// Gets all projects.
    /// </summary>
    /// <returns>List of project entities</returns>
    Task<IReadOnlyList<IJiraProject>> GetProjectsAsync();

    /// <summary>
    /// Gets a specific project by key.
    /// </summary>
    /// <param name="projectKey">Project key</param>
    /// <returns>Project entity</returns>
    Task<IJiraProject?> GetProjectAsync(string projectKey);

    /// <summary>
    /// Gets comments for a specific issue.
    /// </summary>
    /// <param name="issueKey">Issue key</param>
    /// <returns>List of comment entities</returns>
    Task<IReadOnlyList<IJiraComment>> GetCommentsAsync(string issueKey);

    /// <summary>
    /// Gets comments for issues matching a JQL query.
    /// </summary>
    /// <param name="jql">JQL query string</param>
    /// <param name="maxIssues">Maximum number of issues to fetch comments for</param>
    /// <returns>List of comment entities</returns>
    Task<IReadOnlyList<IJiraComment>> GetCommentsForIssuesAsync(string jql, int maxIssues = 100);

    /// <summary>
    /// Gets the total number of issues matching a JQL query.
    /// </summary>
    /// <param name="jql">JQL query string</param>
    /// <returns>Total count of matching issues</returns>
    Task<int> GetIssueCountAsync(string jql);
}
