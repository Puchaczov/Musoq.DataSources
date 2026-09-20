using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworksSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<NetworkEntity>
{
    private const string NetworksSourceName = "docker_networks";

    protected override void CollectChunks(IChunkWriter<NetworkEntity> writer)
    {
        executionContext.ReportDataSourceBegin(NetworksSourceName);

        try
        {
            var networks = api.ListNetworksAsync().Result;
            var rows = networks
                .Select(network => new NetworkEntity(network))
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
