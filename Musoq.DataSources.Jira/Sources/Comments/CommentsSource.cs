using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Comments;

/// <summary>
///     Row source for Jira comments.
/// </summary>
internal class CommentsSource : AsyncRowsSourceBase<IJiraComment>
{
    private const string SourceName = "jira_comments";
    private readonly IJiraApi _api;
    private readonly string? _issueKey;
    private readonly string? _projectKey;
    private readonly RuntimeContext _runtimeContext;

    /// <summary>
    ///     Creates a comments source for a specific issue.
    /// </summary>
    public CommentsSource(IJiraApi api, RuntimeContext runtimeContext, string issueKey)
        : base(runtimeContext.EndWorkToken)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _issueKey = issueKey;
        _projectKey = null;
    }

    /// <summary>
    ///     Creates a comments source for issues in a project.
    /// </summary>
    public CommentsSource(IJiraApi api, RuntimeContext runtimeContext, string? issueKey, string? projectKey)
        : base(runtimeContext.EndWorkToken)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _issueKey = issueKey;
        _projectKey = projectKey;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
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
                comments = await _api.GetCommentsAsync(_issueKey);
            }
            else if (!string.IsNullOrEmpty(_projectKey))
            {
                var filterParameters = JqlBuilder.ExtractParameters(_runtimeContext.QuerySourceInfo.WhereNode);
                var jql = JqlBuilder.BuildJql($"project = {_projectKey}", filterParameters);
                comments = await _api.GetCommentsForIssuesAsync(jql);
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
        catch (Exception ex)
        {
            _runtimeContext.Logger.LogError(ex, "Error occurred while collecting {SourceName} data.", SourceName);
            throw;
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
    }
}