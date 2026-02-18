using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Sources.Commits;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.GitHub.Sources.BranchCommits;

internal class BranchCommitsSource : AsyncRowsSourceBase<CommitEntity>
{
    private const string SourceName = "github_branch_commits";
    private readonly IGitHubApi _api;
    private readonly RuntimeContext _runtimeContext;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _base;
    private readonly string _head;

    public BranchCommitsSource(IGitHubApi api, RuntimeContext runtimeContext, string owner, string repo, string @base, string head)
        : base(runtimeContext.EndWorkToken)
    {
        _api = api;
        _runtimeContext = runtimeContext;
        _owner = owner;
        _repo = repo;
        _base = @base;
        _head = head;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        _runtimeContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var commits = await _api.GetBranchSpecificCommitsAsync(_owner, _repo, _base, _head);

            var resolvers = commits
                .Select(c => new EntityResolver<CommitEntity>(
                    c,
                    CommitsSourceHelper.CommitsNameToIndexMap,
                    CommitsSourceHelper.CommitsIndexToMethodAccessMap))
                .ToList<IObjectResolver>();

            chunkedSource.Add(resolvers);
            totalRowsProcessed = resolvers.Count;
            _runtimeContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);
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
