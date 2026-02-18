using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.PullRequests;

internal class PullRequestsSource : AsyncRowsSourceBase<PullRequestEntity>
{
    private const string SourceName = "github_pullrequests";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;

    public PullRequestsSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo)
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
            var parameters = WhereNodeHelper.ExtractParameters(_runtimeContext.QuerySourceInfo.WhereNode);
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
            
            // Build request with filters from WHERE clause
            var request = new PullRequestRequest();
            
            if (!string.IsNullOrEmpty(parameters.State))
            {
                request.State = parameters.State.ToLowerInvariant() switch
                {
                    "open" => ItemStateFilter.Open,
                    "closed" => ItemStateFilter.Closed,
                    _ => ItemStateFilter.All
                };
            }
            
            if (!string.IsNullOrEmpty(parameters.Head))
            {
                request.Head = parameters.Head;
            }
            
            if (!string.IsNullOrEmpty(parameters.Base))
            {
                request.Base = parameters.Base;
            }
            
            while (fetchedRows < maxRows && !cancellationToken.IsCancellationRequested)
            {
                var pullRequests = await _api.GetPullRequestsAsync(_owner, _repo, request, perPage, page);
                
                if (pullRequests.Count == 0)
                    break;
                
                var resolvers = pullRequests
                    .Take(maxRows - fetchedRows)
                    .Select(pr => new EntityResolver<PullRequestEntity>(
                        pr, 
                        PullRequestsSourceHelper.PullRequestsNameToIndexMap, 
                        PullRequestsSourceHelper.PullRequestsIndexToMethodAccessMap))
                    .ToList();
                
                chunkedSource.Add(resolvers);
                
                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                
                if (pullRequests.Count < perPage)
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
