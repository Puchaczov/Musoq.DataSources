using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworksSource : RowSourceBase<NetworkResponse>
{
    private const string NetworksSourceName = "docker_networks";
    private readonly IDockerApi _api;
    private readonly RuntimeContext _runtimeContext;

    public NetworksSource(IDockerApi api, RuntimeContext runtimeContext)
    {
        _api = api;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(NetworksSourceName);
        
        try
        {
            var networks = _api.ListNetworksAsync().Result;
            _runtimeContext.ReportDataSourceRowsKnown(NetworksSourceName, networks.Count);
        
            chunkedSource.Add(
                networks.Select(c => new EntityResolver<NetworkResponse>(c, NetworksSourceHelper.NetworksNameToIndexMap, NetworksSourceHelper.NetworksIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(NetworksSourceName, networks.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(NetworksSourceName, 0);
            throw;
        }
    }
}