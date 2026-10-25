using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Images;

internal class ImagesSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<ImageEntity>
{
    private const string ImagesSourceName = "docker_images";

    protected override void CollectChunks(IChunkWriter<ImageEntity> writer)
    {
        if (PatternSourcePlan.HasPatterns(executionContext))
        {
            var filtered = PatternSourcePlan.Read(executionContext, inner => new ImagesSource(api, inner), DockerNativePatterns.Image.Names);
            foreach (var chunk in filtered.Chunks)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                writer.Write(chunk);
            }
            return;
        }

        executionContext.ReportDataSourceBegin(ImagesSourceName);

        try
        {
            var native = NativePatternExecution.Get(executionContext);
            native?.PageRead();
            var images = api.ListImagesAsync().Result;
            var rows = images
                .Where(value => { writer.CancellationToken.ThrowIfCancellationRequested(); executionContext.EndWorkToken.ThrowIfCancellationRequested(); return native?.Matches(value, DockerNativePatterns.Image) != false; })
                .Select(value => { var row = new ImageEntity(value); native?.Constructed(); return row; })
                .Where(entity => DockerSourcePlanner.Matches(executionContext.Plan.AcceptedPredicate, entity))
                .ToList();

            executionContext.ReportDataSourceRowsKnown(ImagesSourceName, rows.Count);
            writer.Write(rows);

            executionContext.ReportDataSourceEnd(ImagesSourceName, rows.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(ImagesSourceName, 0);
            throw;
        }
    }
}
