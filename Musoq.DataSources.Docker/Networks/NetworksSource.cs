using Docker.DotNet.Models;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworksSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<NetworkResponse>
{
    private const string NetworksSourceName = "docker_networks";

    protected override void CollectChunks(IChunkWriter<NetworkResponse> writer)
    {
        executionContext.ReportDataSourceBegin(NetworksSourceName);

        try
        {
            var networks = api.ListNetworksAsync().Result;
            executionContext.ReportDataSourceRowsKnown(NetworksSourceName, networks.Count);

            writer.Write(networks.ToList());

            executionContext.ReportDataSourceEnd(NetworksSourceName, networks.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(NetworksSourceName, 0);
            throw;
        }
    }
}
