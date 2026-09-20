using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.Repositories;

internal class RepositoriesSource : AsyncRowsSourceBase<RepositoryEntity>
{
    private const string SourceName = "github_repositories";
    private readonly IGitHubApi _api;
    private readonly SourceExecutionContext _executionContext;
    private readonly string? _owner;

    public RepositoriesSource(IGitHubApi api, SourceExecutionContext executionContext, string? owner = null)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _owner = owner;
    }

    protected override async Task CollectChunksAsync(
        IChunkWriter<RepositoryEntity> writer,
        CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;
            var plan = _executionContext.Plan;
            var filters = GitHubSourcePlanner.GetFilters(plan);
            long skipped = 0;
            long emitted = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                IReadOnlyList<RepositoryEntity> repos;

                if (!string.IsNullOrEmpty(_owner))
                {
                    repos = await _api.GetRepositoriesForOwnerAsync(_owner, perPage, page);
                }
                else
                {
                    var request = new RepositoryRequest();
                    GitHubSourcePlanner.ApplyRepositoryFilters(request, filters);
                    repos = await _api.GetUserRepositoriesAsync(request, perPage, page);
                }

                if (repos.Count == 0)
                    break;

                var plannedRepositories =
                    GitHubSourcePlanner.ApplyAcceptedPlan(repos, plan, ref skipped, ref emitted);

                if (plannedRepositories.Count > 0)
                {
                    writer.Write(plannedRepositories);

                    totalRowsProcessed += plannedRepositories.Count;
                    _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                }

                if (repos.Count < perPage || GitHubSourcePlanner.IsTakeSatisfied(plan, emitted))
                    break;

                page++;
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
