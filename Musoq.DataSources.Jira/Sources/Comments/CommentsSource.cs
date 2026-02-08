using System.Collections.Concurrent;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Comments;

/// <summary>
/// Row source for Jira comments.
/// </summary>
internal class CommentsSource : RowSourceBase<IJiraComment>
{
    private const string SourceName = "jira_comments";
    private readonly IJiraApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string? _issueKey;
    private readonly string? _projectKey;

    /// <summary>
    /// Creates a comments source for a specific issue.
    /// </summary>
    public CommentsSource(IJiraApi api, RuntimeContext runtimeContext, string issueKey)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _issueKey = issueKey;
        _projectKey = null;
    }

    /// <summary>
    /// Creates a comments source for issues in a project.
    /// </summary>
    public CommentsSource(IJiraApi api, RuntimeContext runtimeContext, string? issueKey, string? projectKey)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _issueKey = issueKey;
        _projectKey = projectKey;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var takeValue = _runtimeContext.QueryHints.TakeValue;
            var maxRows = takeValue.HasValue ? (int)takeValue.Value : int.MaxValue;

            IReadOnlyList<IJiraComment> comments;

            if (!string.IsNullOrEmpty(_issueKey))
            {
                // Fetch comments for a specific issue
                comments = _api.GetCommentsAsync(_issueKey).Result;
            }
            else if (!string.IsNullOrEmpty(_projectKey))
            {
                // Fetch comments for all issues in a project
                var filterParameters = JqlBuilder.ExtractParameters(_runtimeContext.QuerySourceInfo.WhereNode);
                var jql = JqlBuilder.BuildJql($"project = {_projectKey}", filterParameters);
                comments = _api.GetCommentsForIssuesAsync(jql, 100).Result;
            }
            else
            {
                comments = [];
            }

            var resolvers = comments
                .Take(maxRows)
                .Select(c => new EntityResolver<IJiraComment>(
                    c,
                    CommentsSourceHelper.CommentsNameToIndexMap,
                    CommentsSourceHelper.CommentsIndexToMethodAccessMap))
                .ToList();

            if (resolvers.Count > 0)
            {
                chunkedSource.Add(resolvers);
                totalRowsProcessed = resolvers.Count;
            }

            _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
    }
}
