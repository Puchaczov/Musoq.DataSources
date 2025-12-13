using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Volumes;

internal class VolumesSource : RowSourceBase<VolumeResponse>
{
    private const string VolumesSourceName = "docker_volumes";
    private readonly IDockerApi _api;
    private readonly RuntimeContext _runtimeContext;

    public VolumesSource(IDockerApi api, RuntimeContext runtimeContext)
    {
        _api = api;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(VolumesSourceName);
        
        try
        {
            var volumes = _api.ListVolumesAsync().Result;
            _runtimeContext.ReportDataSourceRowsKnown(VolumesSourceName, volumes.Count);

            chunkedSource.Add(
                volumes.Select(c => new EntityResolver<VolumeResponse>(c, VolumesSourceHelper.VolumesNameToIndexMap, VolumesSourceHelper.VolumesIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(VolumesSourceName, volumes.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(VolumesSourceName, 0);
            throw;
        }
    }
}