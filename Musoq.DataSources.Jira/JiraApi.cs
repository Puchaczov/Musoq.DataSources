using Musoq.Schema.Diagnostics;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Jira;

/// <summary>
///     Jira API implementation using Atlassian.SDK.
/// </summary>
internal class JiraApi : IJiraApi, IFilteredJiraApi
{
    private const int DefaultMaxResults = 50;
    private readonly Atlassian.Jira.Jira _jira;
    private readonly NativePatternExecution? _patterns;
    private readonly CancellationToken _cancellation;
    private readonly Action<DataSourceDiagnostic>? _warningReporter;
    private JiraApi(Atlassian.Jira.Jira jira, NativePatternExecution? patterns, CancellationToken cancellationToken, Action<DataSourceDiagnostic>? warningReporter)
    { _jira = jira; _patterns = patterns; _cancellation = cancellationToken; _warningReporter = warningReporter; }
    public IJiraApi WithPatterns(NativePatternExecution patterns, CancellationToken cancellationToken) => new JiraApi(_jira, patterns, cancellationToken, _warningReporter);

    internal JiraApi WithReporting(Action<DataSourceDiagnostic>? reporter, CancellationToken token)
        => new(_jira, _patterns, token, reporter);

    /// <summary>
    ///     Initializes a new instance of the JiraApi class.
    /// </summary>
    /// <param name="jiraUrl">Jira instance URL</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="apiToken">API token for authentication</param>
    public JiraApi(string jiraUrl, string username, string apiToken)
    {
        _jira = Atlassian.Jira.Jira.CreateRestClient(jiraUrl, username, apiToken);
    }

    /// <summary>
    ///     Initializes a new instance of the JiraApi class with a pre-configured Jira client.
    ///     For testing purposes.
    /// </summary>
    /// <param name="jira">Pre-configured Jira client</param>
    internal JiraApi(Atlassian.Jira.Jira jira)
    {
        _jira = jira;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraIssue>> GetIssuesAsync(string jql, int maxResults = DefaultMaxResults,
        int startAt = 0)
    {
        var issues = await _jira.Issues.GetIssuesFromJqlAsync(jql, maxResults, startAt, _cancellation);
        return PatternPage.Map(issues, _patterns, JiraNativePatterns.Issue, static value => (IJiraIssue)new IssueEntity(value), _cancellation);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraIssue>> GetIssuesForProjectAsync(
        string projectKey,
        string? additionalJql = null,
        int maxResults = DefaultMaxResults,
        int startAt = 0)
    {
        var jql = $"project = {projectKey}";

        if (!string.IsNullOrEmpty(additionalJql)) jql += $" AND ({additionalJql})";

        return await GetIssuesAsync(jql, maxResults, startAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraProject>> GetProjectsAsync()
    {
        var projects = await _jira.Projects.GetProjectsAsync(_cancellation);
        return PatternPage.Map(projects, _patterns, JiraNativePatterns.Project, static value => (IJiraProject)new ProjectEntity(value), _cancellation);
    }

    /// <inheritdoc />
    public async Task<IJiraProject?> GetProjectAsync(string projectKey)
    {
        try
        {
            var project = await _jira.Projects.GetProjectAsync(projectKey, _cancellation);
            return project != null ? new ProjectEntity(project) : null;
        }
        catch (Exception error) when (JiraSchema.Reports.Warn(_warningReporter, error, "The optional project lookup returned no project.", _cancellation))
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraComment>> GetCommentsAsync(string issueKey)
    {
        var issue = await _jira.Issues.GetIssueAsync(issueKey, _cancellation);
        var comments = await issue.GetCommentsAsync(_cancellation);
        return MapComments(comments, issueKey);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IJiraComment>> GetCommentsForIssuesAsync(string jql, int maxIssues = 100)
    {
        var issues = await _jira.Issues.GetIssuesFromJqlAsync(jql, maxIssues, token: _cancellation);
        var allComments = new List<IJiraComment>();

        foreach (var issue in issues)
        {
            var comments = await issue.GetCommentsAsync(_cancellation);
            allComments.AddRange(MapComments(comments, issue.Key.Value));
        }

        return allComments;
    }

    /// <inheritdoc />
    public async Task<int> GetIssueCountAsync(string jql)
    {
        var result = await _jira.Issues.GetIssuesFromJqlAsync(jql, 0, token: _cancellation);
        return result.TotalItems;
    }
    internal IReadOnlyList<IJiraComment> MapComments(IEnumerable<Atlassian.Jira.Comment> comments, string issueKey)
        => PatternPage.Map(comments.Select(value => new JiraNativePatterns.CommentInput(value, issueKey)), _patterns,
            JiraNativePatterns.Comment, static value => (IJiraComment)new CommentEntity(value.Value, value.IssueKey), _cancellation);
}