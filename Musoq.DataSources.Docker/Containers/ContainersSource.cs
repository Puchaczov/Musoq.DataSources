using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Containers;

internal class ContainersSource : RowSourceBase<ContainerListResponse>
{
    private const string ContainersSourceName = "docker_containers";
    private readonly IDockerApi _api;
    private readonly RuntimeContext _runtimeContext;

    public ContainersSource(IDockerApi api, RuntimeContext runtimeContext)
    {
        _api = api;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(ContainersSourceName);
        
        try
        {
            var containers = _api.ListContainersAsync().Result;
            _runtimeContext.ReportDataSourceRowsKnown(ContainersSourceName, containers.Count);

            chunkedSource.Add(
                containers.Select(c => new EntityResolver<ContainerListResponse>(c, ContainersSourceHelper.ContainersNameToIndexMap, ContainersSourceHelper.ContainersIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(ContainersSourceName, containers.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(ContainersSourceName, 0);
            throw;
        }
    }
}