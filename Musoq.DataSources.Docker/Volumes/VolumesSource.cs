using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Volumes;

internal class VolumesSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<VolumeEntity>
{
    private const string VolumesSourceName = "docker_volumes";

    protected override void CollectChunks(IChunkWriter<VolumeEntity> writer)
    {
        executionContext.ReportDataSourceBegin(VolumesSourceName);

        try
        {
            var volumes = api.ListVolumesAsync().Result;
            executionContext.ReportDataSourceRowsKnown(VolumesSourceName, volumes.Count);

            writer.Write(volumes.Select(volume => new VolumeEntity(volume)).ToList());

            executionContext.ReportDataSourceEnd(VolumesSourceName, volumes.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(VolumesSourceName, 0);
            throw;
        }
    }
}
