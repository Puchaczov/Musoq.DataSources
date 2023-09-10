using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworksSource : RowSourceBase<NetworkResponse>
{
    private readonly IDockerApi _api;

    public NetworksSource(IDockerApi api)
    {
        _api = api;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var networks = _api.ListNetworksAsync().Result;
        
        chunkedSource.Add(
            networks.Select(c => new EntityResolver<NetworkResponse>(c, NetworksSourceHelper.NetworksNameToIndexMap, NetworksSourceHelper.NetworksIndexToMethodAccessMap)).ToList());
    }
}