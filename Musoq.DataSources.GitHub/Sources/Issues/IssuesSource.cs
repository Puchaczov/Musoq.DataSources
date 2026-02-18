using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.Issues;

internal class IssuesSource : RowSourceBase<IssueEntity>
{
    private const string SourceName = "github_issues";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;

    public IssuesSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo)
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
            var request = new RepositoryIssueRequest();
            
            if (!string.IsNullOrEmpty(parameters.State))
            {
                request.State = parameters.State.ToLowerInvariant() switch
                {
                    "open" => ItemStateFilter.Open,
                    "closed" => ItemStateFilter.Closed,
                    _ => ItemStateFilter.All
                };
            }
            
            if (!string.IsNullOrEmpty(parameters.Assignee))
            {
                request.Assignee = parameters.Assignee;
            }
            
            if (!string.IsNullOrEmpty(parameters.Author))
            {
                request.Creator = parameters.Author;
            }
            
            if (!string.IsNullOrEmpty(parameters.Milestone))
            {
                request.Milestone = parameters.Milestone;
            }
            
            if (parameters.Since.HasValue)
            {
                request.Since = parameters.Since.Value;
            }
            
            if (parameters.Labels.Count > 0)
            {
                foreach (var label in parameters.Labels)
                {
                    request.Labels.Add(label);
                }
            }
            
            while (fetchedRows < maxRows && !_runtimeContext.EndWorkToken.IsCancellationRequested)
            {
                var issues = _api.GetIssuesAsync(_owner, _repo, request, perPage, page).Result;
                
                if (issues.Count == 0)
                    break;
                
                var resolvers = issues
                    .Take(maxRows - fetchedRows)
                    .Select(i => new EntityResolver<IssueEntity>(
                        i, 
                        IssuesSourceHelper.IssuesNameToIndexMap, 
                        IssuesSourceHelper.IssuesIndexToMethodAccessMap))
                    .ToList();
                
                chunkedSource.Add(resolvers);
                
                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                
                if (issues.Count < perPage)
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
