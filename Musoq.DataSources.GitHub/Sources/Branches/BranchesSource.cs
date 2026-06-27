using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub.Sources.Branches;

internal class BranchesSource : AsyncRowsSourceBase<BranchEntity>
{
    private const string SourceName = "github_branches";
    private readonly IGitHubApi _api;
    private readonly SourceExecutionContext _executionContext;
    private readonly string _owner;
    private readonly string _repo;

    public BranchesSource(IGitHubApi api, SourceExecutionContext executionContext, string owner, string repo)
        : base(executionContext.EndWorkToken)
    {
        _api = api;
        _executionContext = executionContext;
        _owner = owner;
        _repo = repo;
    }

    protected override async Task CollectChunksAsync(IChunkWriter<BranchEntity> writer, CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;

            while (!cancellationToken.IsCancellationRequested)
            {
                var branches = await _api.GetBranchesAsync(_owner, _repo, perPage, page);

                if (branches.Count == 0)
                    break;

                writer.Write(branches);

                totalRowsProcessed += branches.Count;
                _executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (branches.Count < perPage)
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
