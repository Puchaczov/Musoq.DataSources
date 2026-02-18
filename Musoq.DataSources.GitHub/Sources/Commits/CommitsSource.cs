using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.Commits;

internal class CommitsSource : RowSourceBase<CommitEntity>
{
    private const string SourceName = "github_commits";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string? _sha;

    public CommitsSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo, string? sha = null)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _owner = owner;
        _repo = repo;
        _sha = sha;
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
            var request = new CommitRequest();
            
            // Use SHA from parameter or WHERE clause
            if (!string.IsNullOrEmpty(_sha))
            {
                request.Sha = _sha;
            }
            else if (!string.IsNullOrEmpty(parameters.Sha))
            {
                request.Sha = parameters.Sha;
            }
            
            if (!string.IsNullOrEmpty(parameters.Path))
            {
                request.Path = parameters.Path;
            }
            
            if (!string.IsNullOrEmpty(parameters.Author))
            {
                request.Author = parameters.Author;
            }
            
            if (parameters.Since.HasValue)
            {
                request.Since = parameters.Since.Value;
            }
            
            while (fetchedRows < maxRows && !_runtimeContext.EndWorkToken.IsCancellationRequested)
            {
                var commits = _api.GetCommitsAsync(_owner, _repo, request, perPage, page).Result;
                
                if (commits.Count == 0)
                    break;
                
                var resolvers = commits
                    .Take(maxRows - fetchedRows)
                    .Select(c => new EntityResolver<CommitEntity>(
                        c, 
                        CommitsSourceHelper.CommitsNameToIndexMap, 
                        CommitsSourceHelper.CommitsIndexToMethodAccessMap))
                    .ToList();
                
                chunkedSource.Add(resolvers);
                
                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
                
                if (commits.Count < perPage)
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
