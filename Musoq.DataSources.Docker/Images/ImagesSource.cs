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
            executionContext.ReportDataSourceRowsKnown(ImagesSourceName, images.Count);

            writer.Write(images.Select(image => new ImageEntity(image)).ToList());

            executionContext.ReportDataSourceEnd(ImagesSourceName, images.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(ImagesSourceName, 0);
            throw;
        }
    }
}
