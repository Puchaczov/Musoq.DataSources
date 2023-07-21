using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Volumes;

public class VolumesSource : RowSourceBase<VolumeResponse>
{
    private readonly IDockerApi _api;

    public VolumesSource(IDockerApi api)
    {
        _api = api;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var volumes = _api.ListVolumesAsync().Result;

        chunkedSource.Add(
            volumes.Select(c => new EntityResolver<VolumeResponse>(c, VolumesSourceHelper.VolumesNameToIndexMap, VolumesSourceHelper.VolumesIndexToMethodAccessMap)).ToList());
    }
}