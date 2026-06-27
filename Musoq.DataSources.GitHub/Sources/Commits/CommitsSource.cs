using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.Commits;

internal class CommitsSource : AsyncRowsSourceBase<CommitEntity>
{
    private const string SourceName = "github_commits";
    private readonly IGitHubApi _api;
    private readonly string? _branchOrSha;
    private readonly SourceExecutionContext _executionContext;
    private readonly string _owner;
    private readonly string _repo;

    public CommitsSource(
        IGitHubApi api,
        SourceExecutionContext executionContext,
        string owner,
        string repo,
        string? branchOrSha = null)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _owner = owner;
        _repo = repo;
        _branchOrSha = branchOrSha;
    }

    protected override async Task CollectChunksAsync(IChunkWriter<CommitEntity> writer, CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;
            var plan = _executionContext.Plan;
            var filters = GitHubSourcePlanner.GetFilters(plan);
            var request = new CommitRequest();
            GitHubSourcePlanner.ApplyCommitFilters(request, filters);

            if (!string.IsNullOrEmpty(_branchOrSha))
                request.Sha = _branchOrSha;

            long skipped = 0;
            long emitted = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var commits = await _api.GetCommitsAsync(_owner, _repo, request, perPage, page);

                if (commits.Count == 0)
                    break;

                var plannedCommits = GitHubSourcePlanner.ApplyAcceptedPlan(commits, plan, ref skipped, ref emitted);

                if (plannedCommits.Count > 0)
                {
                    writer.Write(plannedCommits);

                    totalRowsProcessed += plannedCommits.Count;
                    _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                }

                if (commits.Count < perPage || GitHubSourcePlanner.IsTakeSatisfied(plan, emitted))
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
