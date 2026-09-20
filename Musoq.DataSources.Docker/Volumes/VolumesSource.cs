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
            var rows = volumes
                .Select(volume => new VolumeEntity(volume))
                .Where(entity => DockerSourcePlanner.Matches(executionContext.Plan.AcceptedPredicate, entity))
                .ToList();

            executionContext.ReportDataSourceRowsKnown(VolumesSourceName, rows.Count);
            writer.Write(rows);

            executionContext.ReportDataSourceEnd(VolumesSourceName, rows.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(VolumesSourceName, 0);
            throw;
        }
    }
}
