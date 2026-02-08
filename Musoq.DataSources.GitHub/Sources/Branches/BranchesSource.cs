using System.Collections.Concurrent;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Branches;

internal class BranchesSource : RowSourceBase<BranchEntity>
{
    private const string SourceName = "github_branches";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;

    public BranchesSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _owner = owner;
        _repo = repo;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
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
            
            while (fetchedRows < maxRows && !_runtimeContext.EndWorkToken.IsCancellationRequested)
            {
                var branches = _api.GetBranchesAsync(_owner, _repo, perPage, page).Result;
                
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
            
            _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
            throw;
        }
    }
}
