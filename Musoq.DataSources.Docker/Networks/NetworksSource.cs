using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworksSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<NetworkEntity>
{
    private const string NetworksSourceName = "docker_networks";

    protected override void CollectChunks(IChunkWriter<NetworkEntity> writer)
    {
        if (PatternSourcePlan.HasPatterns(executionContext))
        {
            var filtered = PatternSourcePlan.Read(executionContext, inner => new NetworksSource(api, inner), DockerNativePatterns.Network.Names);
            foreach (var chunk in filtered.Chunks)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                writer.Write(chunk);
            }
            return;
        }

        executionContext.ReportDataSourceBegin(NetworksSourceName);

        try
        {
            var native = NativePatternExecution.Get(executionContext);
            native?.PageRead();
            var networks = api.ListNetworksAsync().Result;
            var rows = networks
                .Where(value => { writer.CancellationToken.ThrowIfCancellationRequested(); executionContext.EndWorkToken.ThrowIfCancellationRequested(); return native?.Matches(value, DockerNativePatterns.Network) != false; })
                .Select(value => { var row = new NetworkEntity(value); native?.Constructed(); return row; })
                .Where(entity => DockerSourcePlanner.Matches(executionContext.Plan.AcceptedPredicate, entity))
                .ToList();

            executionContext.ReportDataSourceRowsKnown(NetworksSourceName, rows.Count);
            writer.Write(rows);

            executionContext.ReportDataSourceEnd(NetworksSourceName, rows.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(NetworksSourceName, 0);
            throw;
        }
    }
}
