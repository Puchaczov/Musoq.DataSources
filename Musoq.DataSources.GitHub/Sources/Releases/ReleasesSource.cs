using Microsoft.Extensions.Logging;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub.Sources.Releases;

internal class ReleasesSource(IGitHubApi api, SourceExecutionContext executionContext, string owner, string repo)
    : RowSourceBase<ReleaseEntity>
{
    private const string SourceName = "github_releases";

    protected override void CollectChunks(IChunkWriter<ReleaseEntity> writer)
    {
        if (PatternSourcePlan.HasPatterns(executionContext))
        {
            var filtered = GitHubPatternApi.Read(executionContext, api, "releases", inner => new ReleasesSource(api, inner, owner, repo));
            foreach (var chunk in filtered.Chunks)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                writer.Write(chunk);
            }
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(writer.CancellationToken, executionContext.EndWorkToken);
        var boundApi = GitHubPatternApi.Bind(api, executionContext, linked.Token);
        executionContext.ReportDataSourceBegin(SourceName);
        long totalRowsProcessed = 0;

        try
        {
            var page = 1;
            var perPage = 100;
            long skipped = 0, emitted = 0;
            var plan = executionContext.Plan;

            if (plan.AcceptedTake == 0) return;
            while (!linked.IsCancellationRequested)
            {
                var releases = boundApi.GetReleasesAsync(owner, repo, perPage, page).Result;

                if (PatternPage.RawCount(releases) == 0)
                    break;

                var planned = GitHubSourcePlanner.ApplyAcceptedPlan(releases, plan, ref skipped, ref emitted);
                if (planned.Count > 0) writer.Write(planned);
                totalRowsProcessed += planned.Count;
                executionContext.ReportDataSourceRowsRead(SourceName, totalRowsProcessed);

                if (PatternPage.RawCount(releases) < perPage || GitHubSourcePlanner.IsTakeSatisfied(plan, emitted))
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
