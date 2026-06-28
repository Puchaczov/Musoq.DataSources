using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Images;

internal class ImagesSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<ImageEntity>
{
    private const string ImagesSourceName = "docker_images";

    protected override void CollectChunks(IChunkWriter<ImageEntity> writer)
    {
        executionContext.ReportDataSourceBegin(ImagesSourceName);

        try
        {
            var images = api.ListImagesAsync().Result;
            var rows = images
                .Select(image => new ImageEntity(image))
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
