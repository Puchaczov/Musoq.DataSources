using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.Issues;

internal class IssuesSource : AsyncRowsSourceBase<IssueEntity>
{
    private const string SourceName = "github_issues";
    private readonly IGitHubApi _api;
    private readonly SourceExecutionContext _executionContext;
    private readonly string _owner;
    private readonly string _repo;

    public IssuesSource(IGitHubApi api, SourceExecutionContext executionContext, string owner, string repo)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _owner = owner;
        _repo = repo;
    }

    protected override async Task CollectChunksAsync(IChunkWriter<IssueEntity> writer, CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;
            var request = new RepositoryIssueRequest();

            while (!cancellationToken.IsCancellationRequested)
            {
                var issues = await _api.GetIssuesAsync(_owner, _repo, request, perPage, page);

                if (issues.Count == 0)
                    break;

                writer.Write(issues);

                totalRowsProcessed += issues.Count;
                _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (issues.Count < perPage)
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
