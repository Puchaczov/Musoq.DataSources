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
            executionContext.ReportDataSourceRowsKnown(NetworksSourceName, networks.Count);

            writer.Write(networks.Select(network => new NetworkEntity(network)).ToList());

            executionContext.ReportDataSourceEnd(NetworksSourceName, networks.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(NetworksSourceName, 0);
            throw;
        }
    }
}
