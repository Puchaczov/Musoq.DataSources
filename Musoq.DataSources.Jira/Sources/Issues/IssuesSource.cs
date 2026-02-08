using System.Collections.Concurrent;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Issues;

/// <summary>
/// Row source for Jira issues with predicate pushdown support.
/// Extracts filter conditions from WHERE clause and builds JQL for efficient querying.
/// </summary>
internal class IssuesSource : RowSourceBase<IJiraIssue>
{
    private const string SourceName = "jira_issues";
    private readonly IJiraApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string? _projectKey;
    private readonly string? _jql;

    /// <summary>
    /// Creates an issues source for a specific project.
    /// </summary>
    public IssuesSource(IJiraApi api, RuntimeContext runtimeContext, string projectKey)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _projectKey = projectKey;
        _jql = null;
    }

    /// <summary>
    /// Creates an issues source with a custom JQL query.
    /// </summary>
    public IssuesSource(IJiraApi api, RuntimeContext runtimeContext, string? projectKey, string? jql)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _projectKey = projectKey;
        _jql = jql;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            // Extract filter parameters from WHERE clause for predicate pushdown
            var filterParameters = JqlBuilder.ExtractParameters(_runtimeContext.QuerySourceInfo.WhereNode);
            
            // Build base JQL from project key if specified
            var baseJql = !string.IsNullOrEmpty(_projectKey) 
                ? $"project = {_projectKey}" 
                : _jql;
            
            // Build final JQL with WHERE clause filters
            var finalJql = JqlBuilder.BuildJql(baseJql, filterParameters);
            
            // Apply ordering if not already in JQL
            if (!finalJql.Contains("order by", StringComparison.OrdinalIgnoreCase))
            {
                finalJql += " ORDER BY created DESC";
            }

            var takeValue = _runtimeContext.QueryHints.TakeValue;
            var skipValue = _runtimeContext.QueryHints.SkipValue;

            int startAt = skipValue.HasValue ? (int)skipValue.Value : 0;
            int maxResults = 50;
            var maxRows = takeValue.HasValue ? (int)takeValue.Value : int.MaxValue;
            var fetchedRows = 0;

            // Fetch issues with pagination
            while (fetchedRows < maxRows && !_runtimeContext.EndWorkToken.IsCancellationRequested)
            {
                var issues = _api.GetIssuesAsync(finalJql, maxResults, startAt).Result;

                if (issues.Count == 0)
                    break;

                var resolvers = issues
                    .Take(maxRows - fetchedRows)
                    .Select(i => new EntityResolver<IJiraIssue>(
                        i,
                        IssuesSourceHelper.IssuesNameToIndexMap,
                        IssuesSourceHelper.IssuesIndexToMethodAccessMap))
                    .ToList();

                chunkedSource.Add(resolvers);

                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                startAt += issues.Count;
                
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (issues.Count < maxResults)
                    break;
            }
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
    }
}
