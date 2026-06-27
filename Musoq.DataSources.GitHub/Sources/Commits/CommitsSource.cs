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
            var request = new CommitRequest();

            if (!string.IsNullOrEmpty(_branchOrSha))
                request.Sha = _branchOrSha;

            while (!cancellationToken.IsCancellationRequested)
            {
                var commits = await _api.GetCommitsAsync(_owner, _repo, request, perPage, page);

                if (commits.Count == 0)
                    break;

                writer.Write(commits);

                totalRowsProcessed += commits.Count;
                _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (commits.Count < perPage)
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
