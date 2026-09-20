using Microsoft.Extensions.Logging;
using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub.Sources.Releases;

internal class ReleasesSource(IGitHubApi api, SourceExecutionContext executionContext, string owner, string repo)
    : RowSourceBase<ReleaseEntity>
{
    private const string SourceName = "github_releases";

    protected override void CollectChunks(IChunkWriter<ReleaseEntity> writer)
    {
        executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;

            while (!writer.CancellationToken.IsCancellationRequested)
            {
                var releases = api.GetReleasesAsync(owner, repo, perPage, page).Result;

                if (releases.Count == 0)
                    break;

                writer.Write(releases);

                totalRowsProcessed += releases.Count;
                executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (releases.Count < perPage)
                    break;

                page++;
            }
        }
        catch (Exception ex)
        {
            executionContext.Logger.LogError(ex, "Error occurred while collecting {SourceName} data.", SourceName);
            throw;
        }
        finally
        {
            executionContext.ReportDataSourceEnd(SourceName, totalRowsProcessed);
        }
    }
}
