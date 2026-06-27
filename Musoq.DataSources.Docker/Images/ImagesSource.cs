using Docker.DotNet.Models;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Images;

internal class ImagesSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<ImagesListResponse>
{
    private const string ImagesSourceName = "docker_images";

    protected override void CollectChunks(IChunkWriter<ImagesListResponse> writer)
    {
        executionContext.ReportDataSourceBegin(ImagesSourceName);

        try
        {
            var images = api.ListImagesAsync().Result;
            executionContext.ReportDataSourceRowsKnown(ImagesSourceName, images.Count);

            writer.Write(images.ToList());

            executionContext.ReportDataSourceEnd(ImagesSourceName, images.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(ImagesSourceName, 0);
            throw;
        }
    }
}
