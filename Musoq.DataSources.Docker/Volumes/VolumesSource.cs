using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Volumes;

internal class VolumesSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<VolumeEntity>
{
    private const string VolumesSourceName = "docker_volumes";

    protected override void CollectChunks(IChunkWriter<VolumeEntity> writer)
    {
        if (PatternSourcePlan.HasPatterns(executionContext))
        {
            var filtered = PatternSourcePlan.Read(executionContext, inner => new VolumesSource(api, inner), DockerNativePatterns.Volume.Names);
            foreach (var chunk in filtered.Chunks)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                writer.Write(chunk);
            }
            return;
        }

        executionContext.ReportDataSourceBegin(VolumesSourceName);

        try
        {
            var native = NativePatternExecution.Get(executionContext);
            native?.PageRead();
            var volumes = api.ListVolumesAsync().Result;
            var rows = volumes
                .Where(value => { writer.CancellationToken.ThrowIfCancellationRequested(); executionContext.EndWorkToken.ThrowIfCancellationRequested(); return native?.Matches(value, DockerNativePatterns.Volume) != false; })
                .Select(value => { var row = new VolumeEntity(value); native?.Constructed(); return row; })
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
