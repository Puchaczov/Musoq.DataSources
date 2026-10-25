using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.PullRequests;

internal class PullRequestsSource : AsyncRowsSourceBase<PullRequestEntity>
{
    private const string SourceName = "github_pullrequests";
    private readonly IGitHubApi _api;
    private readonly SourceExecutionContext _executionContext;
    private readonly string _owner;
    private readonly string _repo;

    public PullRequestsSource(IGitHubApi api, SourceExecutionContext executionContext, string owner, string repo)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _owner = owner;
        _repo = repo;
    }

    protected override async Task CollectChunksAsync(
        IChunkWriter<PullRequestEntity> writer,
        CancellationToken cancellationToken)
    {
        if (PatternSourcePlan.HasPatterns(_executionContext))
        {
            var filtered = GitHubPatternApi.Read(_executionContext, _api, "pullrequests", inner => new PullRequestsSource(_api, inner, _owner, _repo));
            foreach (var chunk in filtered.Chunks)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                writer.Write(chunk);
            }
            return;
        }

        var api = GitHubPatternApi.Bind(_api, _executionContext, cancellationToken);
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;
            var plan = _executionContext.Plan;
            var filters = GitHubSourcePlanner.GetFilters(plan);
            var request = new PullRequestRequest();
            GitHubSourcePlanner.ApplyPullRequestFilters(request, filters);
            long skipped = 0;
            long emitted = 0;

            if (_executionContext.Plan.AcceptedTake == 0) return;
            while (!cancellationToken.IsCancellationRequested)
            {
                var pullRequests = await api.GetPullRequestsAsync(_owner, _repo, request, perPage, page);

                if (PatternPage.RawCount(pullRequests) == 0)
                    break;

                var plannedPullRequests =
                    GitHubSourcePlanner.ApplyAcceptedPlan(pullRequests, plan, ref skipped, ref emitted);

                if (plannedPullRequests.Count > 0)
                {
                    writer.Write(plannedPullRequests);

                    totalRowsProcessed += plannedPullRequests.Count;
                    _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                }

                if (PatternPage.RawCount(pullRequests) < perPage || GitHubSourcePlanner.IsTakeSatisfied(plan, emitted))
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
