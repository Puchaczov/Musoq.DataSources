using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Helpers;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira.Sources.Issues;

/// <summary>
///     Row source for Jira issues.
/// </summary>
internal class IssuesSource : AsyncRowsSourceBase<IJiraIssue>
{
    private const string SourceName = "jira_issues";
    private readonly IJiraApi _api;
    private readonly SourceExecutionContext _executionContext;
    private readonly string? _jql;
    private readonly string? _projectKey;

    /// <summary>
    ///     Creates an issues source for a specific project.
    /// </summary>
    public IssuesSource(IJiraApi api, SourceExecutionContext executionContext, string projectKey)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _projectKey = projectKey;
        _jql = null;
    }

    /// <summary>
    ///     Creates an issues source with a custom JQL query.
    /// </summary>
    public IssuesSource(IJiraApi api, SourceExecutionContext executionContext, string? projectKey, string? jql)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _projectKey = projectKey;
        _jql = jql;
    }

    protected override async Task CollectChunksAsync(IChunkWriter<IJiraIssue> writer, CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var plan = _executionContext.Plan;
            var filters = JiraSourcePlanner.GetFilters(plan);
            var baseJql = !string.IsNullOrEmpty(_projectKey)
                ? $"project = {_projectKey}"
                : _jql ?? string.Empty;

            var finalJql = JqlBuilder.BuildJql(baseJql, filters);

            if (!finalJql.Contains("order by", StringComparison.OrdinalIgnoreCase))
                finalJql += " ORDER BY created DESC";

            var startAt = 0;
            var maxResults = 50;
            long skipped = 0;
            long emitted = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var issues = await _api.GetIssuesAsync(finalJql, maxResults, startAt);

                if (issues.Count == 0)
                    break;

                var plannedIssues = JiraSourcePlanner.ApplyAcceptedPlan(issues, plan, ref skipped, ref emitted);

                if (plannedIssues.Count > 0)
                {
                    writer.Write(plannedIssues);

                    totalRowsProcessed += plannedIssues.Count;
                    _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                }

                startAt += issues.Count;

                if (issues.Count < maxResults || JiraSourcePlanner.IsTakeSatisfied(plan, emitted))
                    break;
            }
        }
        catch (Exception ex)
        {
            _executionContext.Logger.LogError(ex, "Error occurred while collecting {SourceName} data.", SourceName);
            throw;
        }
        finally
        {
            _executionContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
    }
}
