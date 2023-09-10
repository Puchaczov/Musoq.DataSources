using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Images;

internal class ImagesSource : RowSourceBase<ImagesListResponse>
{
    private readonly IDockerApi _api;

    public ImagesSource(IDockerApi api)
    {
        _api = api;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var images = _api.ListImagesAsync().Result;

        chunkedSource.Add(
            images.Select(c => new EntityResolver<ImagesListResponse>(c, ImagesSourceHelper.ImagesNameToIndexMap, ImagesSourceHelper.ImagesIndexToMethodAccessMap)).ToList());
    }
}