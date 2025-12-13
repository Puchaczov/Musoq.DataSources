using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Images;

internal class ImagesSource : RowSourceBase<ImagesListResponse>
{
    private const string ImagesSourceName = "docker_images";
    private readonly IDockerApi _api;
    private readonly RuntimeContext _runtimeContext;

    public ImagesSource(IDockerApi api, RuntimeContext runtimeContext)
    {
        _api = api;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(ImagesSourceName);
        
        try
        {
            var images = _api.ListImagesAsync().Result;
            _runtimeContext.ReportDataSourceRowsKnown(ImagesSourceName, images.Count);

            chunkedSource.Add(
                images.Select(c => new EntityResolver<ImagesListResponse>(c, ImagesSourceHelper.ImagesNameToIndexMap, ImagesSourceHelper.ImagesIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(ImagesSourceName, images.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(ImagesSourceName, 0);
            throw;
        }
    }
}