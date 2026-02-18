using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Jira.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Jira.Sources.Projects;

/// <summary>
/// Row source for Jira projects.
/// </summary>
internal class ProjectsSource : AsyncRowsSourceBase<IJiraProject>
{
    private const string SourceName = "jira_projects";
    private readonly IJiraApi _api;
    private readonly RuntimeContext _runtimeContext;

    public ProjectsSource(IJiraApi api, RuntimeContext runtimeContext)
        : base(runtimeContext.EndWorkToken)
    {
        _api = api;
        _runtimeContext = runtimeContext;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var takeValue = _runtimeContext.QueryHints.TakeValue;
            var projects = await _api.GetProjectsAsync();

            var maxRows = takeValue.HasValue ? (int)takeValue.Value : int.MaxValue;

            var resolvers = projects
                .Take(maxRows)
                .Select(p => new EntityResolver<IJiraProject>(
                    p,
                    ProjectsSourceHelper.ProjectsNameToIndexMap,
                    ProjectsSourceHelper.ProjectsIndexToMethodAccessMap))
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
