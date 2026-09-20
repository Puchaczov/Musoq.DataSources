using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Jira.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira.Sources.Comments;

/// <summary>
///     Row source for Jira comments.
/// </summary>
internal class CommentsSource : AsyncRowsSourceBase<IJiraComment>
{
    private const string SourceName = "jira_comments";
    private readonly IJiraApi _api;
    private readonly SourceExecutionContext _executionContext;
    private readonly string? _issueKey;
    private readonly string? _projectKey;

    /// <summary>
    ///     Creates a comments source for a specific issue.
    /// </summary>
    public CommentsSource(IJiraApi api, SourceExecutionContext executionContext, string issueKey)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _issueKey = issueKey;
        _projectKey = null;
    }

    /// <summary>
    ///     Creates a comments source for issues in a project.
    /// </summary>
    public CommentsSource(IJiraApi api, SourceExecutionContext executionContext, string? issueKey, string? projectKey)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _issueKey = issueKey;
        _projectKey = projectKey;
    }

    protected override async Task CollectChunksAsync(
        IChunkWriter<IJiraComment> writer,
        CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            IReadOnlyList<IJiraComment> comments;

            if (!string.IsNullOrEmpty(_issueKey))
            {
                comments = await _api.GetCommentsAsync(_issueKey);
            }
            else if (!string.IsNullOrEmpty(_projectKey))
            {
                comments = await _api.GetCommentsForIssuesAsync($"project = {_projectKey}");
            }
            else
            {
                comments = [];
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (comments.Count > 0)
            {
                writer.Write(comments);
                totalRowsProcessed = comments.Count;
            }

            _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
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
