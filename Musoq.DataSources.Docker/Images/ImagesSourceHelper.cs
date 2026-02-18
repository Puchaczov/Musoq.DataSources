using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Images;

internal static class ImagesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ImagesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<ImagesListResponse, object>> ImagesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] ImagesColumns;

    static ImagesSourceHelper()
    {
        ImagesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ImagesListResponse.Containers), 0 },
            { nameof(ImagesListResponse.Created), 1 },
            { nameof(ImagesListResponse.ID), 2 },
            { nameof(ImagesListResponse.Labels), 3 },
            { nameof(ImagesListResponse.ParentID), 4 },
            { nameof(ImagesListResponse.RepoDigests), 5 },
            { nameof(ImagesListResponse.RepoTags), 6 },
            { nameof(ImagesListResponse.SharedSize), 7 },
            { nameof(ImagesListResponse.Size), 8 },
            { nameof(ImagesListResponse.VirtualSize), 9 }
        };

        ImagesIndexToMethodAccessMap = new Dictionary<int, Func<ImagesListResponse, object>>
        {
            { 0, info => info.Containers },
            { 1, info => info.Created },
            { 2, info => info.ID },
            { 3, info => info.Labels },
            { 4, info => info.ParentID },
            { 5, info => info.RepoDigests },
            { 6, info => info.RepoTags },
            { 7, info => info.SharedSize },
            { 8, info => info.Size },
            { 9, info => info.VirtualSize }
        };

        ImagesColumns =
        [
            new SchemaColumn(nameof(ImagesListResponse.Containers), 0, typeof(long)),
            new SchemaColumn(nameof(ImagesListResponse.Created), 1, typeof(DateTime)),
            new SchemaColumn(nameof(ImagesListResponse.ID), 2, typeof(string)),
            new SchemaColumn(nameof(ImagesListResponse.Labels), 3, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(ImagesListResponse.ParentID), 4, typeof(string)),
            new SchemaColumn(nameof(ImagesListResponse.RepoDigests), 5, typeof(IList<string>)),
            new SchemaColumn(nameof(ImagesListResponse.RepoTags), 6, typeof(IList<string>)),
            new SchemaColumn(nameof(ImagesListResponse.SharedSize), 7, typeof(long)),
            new SchemaColumn(nameof(ImagesListResponse.Size), 8, typeof(long)),
            new SchemaColumn(nameof(ImagesListResponse.VirtualSize), 9, typeof(long))
        ];
    }
}