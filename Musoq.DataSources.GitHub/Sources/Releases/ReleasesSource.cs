using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.Releases;

internal class ReleasesSource : RowSourceBase<ReleaseEntity>
{
    private const string SourceName = "github_releases";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;

    public ReleasesSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo)
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
                var releases = _api.GetReleasesAsync(_owner, _repo, perPage, page).Result;
                
                if (releases.Count == 0)
                    break;
                
                var resolvers = releases
                    .Take(maxRows - fetchedRows)
                    .Select(r => new EntityResolver<ReleaseEntity>(
                        r, 
                        ReleasesSourceHelper.ReleasesNameToIndexMap, 
                        ReleasesSourceHelper.ReleasesIndexToMethodAccessMap))
                    .ToList();
                
                chunkedSource.Add(resolvers);
                
                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                
                if (releases.Count < perPage)
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
