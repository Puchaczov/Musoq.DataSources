using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Containers;

public class ContainersSource : RowSourceBase<ContainerListResponse>
{
    private readonly IDockerApi _api;

    public ContainersSource(IDockerApi api)
    {
        _api = api;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var containers = _api.ListContainersAsync().Result;

        chunkedSource.Add(
            containers.Select(c => new EntityResolver<ContainerListResponse>(c, ContainersSourceHelper.ContainersNameToIndexMap, ContainersSourceHelper.ContainersIndexToMethodAccessMap)).ToList());
    }
}