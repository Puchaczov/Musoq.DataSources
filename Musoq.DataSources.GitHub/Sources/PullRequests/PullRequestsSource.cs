using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
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
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;
            var request = new PullRequestRequest();

            while (!cancellationToken.IsCancellationRequested)
            {
                var pullRequests = await _api.GetPullRequestsAsync(_owner, _repo, request, perPage, page);

                if (pullRequests.Count == 0)
                    break;

                writer.Write(pullRequests);

                totalRowsProcessed += pullRequests.Count;
                _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (pullRequests.Count < perPage)
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
