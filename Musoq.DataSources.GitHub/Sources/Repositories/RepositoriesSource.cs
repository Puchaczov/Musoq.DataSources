using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Octokit;

namespace Musoq.DataSources.GitHub.Sources.Repositories;

internal class RepositoriesSource : AsyncRowsSourceBase<RepositoryEntity>
{
    private const string SourceName = "github_repositories";
    private readonly IGitHubApi _api;
    private readonly string? _owner;
    private readonly RuntimeContext _runtimeContext;

    public RepositoriesSource(IGitHubApi api, RuntimeContext runtimeContext, string? owner = null)
        : base(runtimeContext.EndWorkToken)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _owner = owner;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var parameters = WhereNodeHelper.ExtractParameters(_runtimeContext.QuerySourceInfo.WhereNode);
            var takeValue = _runtimeContext.QueryHints.TakeValue;
            var skipValue = _runtimeContext.QueryHints.SkipValue;

            var page = 1;
            var perPage = 100;


            if (skipValue.HasValue && skipValue.Value > 0) page = (int)(skipValue.Value / perPage) + 1;

            var maxRows = takeValue.HasValue ? (int)takeValue.Value : int.MaxValue;
            var fetchedRows = 0;

            while (fetchedRows < maxRows && !cancellationToken.IsCancellationRequested)
            {
                IReadOnlyList<RepositoryEntity> repos;

                if (!string.IsNullOrEmpty(parameters.SearchQuery) ||
                    !string.IsNullOrEmpty(parameters.Language))
                {
                    var searchRequest = new SearchRepositoriesRequest(parameters.SearchQuery ?? "")
                    {
                        Language = !string.IsNullOrEmpty(parameters.Language)
                            ? Language.CSharp
                            : null
                    };

                    if (parameters.IsFork.HasValue)
                        searchRequest.Fork = parameters.IsFork.Value
                            ? ForkQualifier.OnlyForks
                            : ForkQualifier.IncludeForks;

                    if (parameters.IsArchived.HasValue)
                        searchRequest.Archived = parameters.IsArchived.Value;

                    repos = await _api.SearchRepositoriesAsync(searchRequest, perPage, page);
                }
                else if (!string.IsNullOrEmpty(_owner))
                {
                    repos = await _api.GetRepositoriesForOwnerAsync(_owner, perPage, page);
                }
                else
                {
                    var request = new RepositoryRequest();

                    if (!string.IsNullOrEmpty(parameters.Visibility))
                        request.Visibility = parameters.Visibility.ToLowerInvariant() switch
                        {
                            "public" => RepositoryRequestVisibility.Public,
                            "private" => RepositoryRequestVisibility.Private,
                            "internal" => RepositoryRequestVisibility.Internal,
                            _ => RepositoryRequestVisibility.All
                        };

                    repos = await _api.GetUserRepositoriesAsync(request, perPage, page);
                }

                if (repos.Count == 0)
                    break;

                var resolvers = repos
                    .Take(maxRows - fetchedRows)
                    .Select(r => new EntityResolver<RepositoryEntity>(
                        r,
                        RepositoriesSourceHelper.RepositoriesNameToIndexMap,
                        RepositoriesSourceHelper.RepositoriesIndexToMethodAccessMap))
                    .ToList();

                chunkedSource.Add(resolvers);

                fetchedRows += resolvers.Count;
                totalRowsProcessed += resolvers.Count;
                _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (repos.Count < perPage)
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