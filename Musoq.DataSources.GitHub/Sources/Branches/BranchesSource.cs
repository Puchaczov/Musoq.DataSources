using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Branches;

internal class BranchesSource : AsyncRowsSourceBase<BranchEntity>
{
    private const string SourceName = "github_branches";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;

    public BranchesSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo)
        : base(runtimeContext.EndWorkToken)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _owner = owner;
        _repo = repo;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var takeValue = _runtimeContext.QueryHints.TakeValue;
            var skipValue = _runtimeContext.QueryHints.SkipValue;
            
            int page = 1;
            int perPage = 100;
            
            if (skipValue.HasValue && skipValue.Value > 0)
            {
                page = (int)(skipValue.Value / perPage) + 1;
            }
            
            var maxRows = takeValue.HasValue ? (int)takeValue.Value : int.MaxValue;
            var fetchedRows = 0;
            
            while (fetchedRows < maxRows && !cancellationToken.IsCancellationRequested)
            {
                var branches = await _api.GetBranchesAsync(_owner, _repo, perPage, page);
                
                if (branches.Count == 0)
                    break;
                
                var resolvers = branches
                    .Take(maxRows - fetchedRows)
                    .Select(b => new EntityResolver<BranchEntity>(
                        b, 
                        BranchesSourceHelper.BranchesNameToIndexMap, 
                        BranchesSourceHelper.BranchesIndexToMethodAccessMap))
                    .ToList();
                
                chunkedSource.Add(resolvers);
                
                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                
                if (branches.Count < perPage)
                    break;
                
                page++;
            }
            
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
