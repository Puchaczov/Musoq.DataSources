using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub.Sources.BranchCommits;

internal class BranchCommitsSource : AsyncRowsSourceBase<CommitEntity>
{
    private const string SourceName = "github_branch_commits";
    private readonly IGitHubApi _api;
    private readonly string _base;
    private readonly SourceExecutionContext _executionContext;
    private readonly string _head;
    private readonly string _owner;
    private readonly string _repo;

    public BranchCommitsSource(
        IGitHubApi api,
        SourceExecutionContext executionContext,
        string owner,
        string repo,
        string @base,
        string head)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _owner = owner;
        _repo = repo;
        _base = @base;
        _head = head;
    }

    protected override async Task CollectChunksAsync(IChunkWriter<CommitEntity> writer, CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var commits = await _api.GetBranchSpecificCommitsAsync(_owner, _repo, _base, _head);

            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(commits);

            totalRowsProcessed = commits.Count;
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
