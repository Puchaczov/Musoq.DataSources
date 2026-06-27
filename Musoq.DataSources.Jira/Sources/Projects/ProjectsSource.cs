using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Jira.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira.Sources.Projects;

/// <summary>
///     Row source for Jira projects.
/// </summary>
internal class ProjectsSource : AsyncRowsSourceBase<IJiraProject>
{
    private const string SourceName = "jira_projects";
    private readonly IJiraApi _api;
    private readonly SourceExecutionContext _executionContext;

    public ProjectsSource(IJiraApi api, SourceExecutionContext executionContext)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
    }

    protected override async Task CollectChunksAsync(IChunkWriter<IJiraProject> writer, CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var projects = await _api.GetProjectsAsync();

            cancellationToken.ThrowIfCancellationRequested();

            if (projects.Count > 0)
            {
                writer.Write(projects);
                totalRowsProcessed = projects.Count;
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
