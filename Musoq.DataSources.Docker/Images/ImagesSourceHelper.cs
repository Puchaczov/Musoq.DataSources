using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Images;

internal static class ImagesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ImagesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<ImageEntity, object?>> ImagesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] ImagesColumns;

    static ImagesSourceHelper()
    {
        ImagesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ImageEntity.Containers), 0 },
            { nameof(ImageEntity.Created), 1 },
            { nameof(ImageEntity.ID), 2 },
            { nameof(ImageEntity.Labels), 3 },
            { nameof(ImageEntity.ParentID), 4 },
            { nameof(ImageEntity.RepoDigests), 5 },
            { nameof(ImageEntity.RepoTags), 6 },
            { nameof(ImageEntity.SharedSize), 7 },
            { nameof(ImageEntity.Size), 8 },
            { nameof(ImageEntity.VirtualSize), 9 }
        };

        ImagesIndexToMethodAccessMap = new Dictionary<int, Func<ImageEntity, object?>>
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
            new SchemaColumn(nameof(ImageEntity.Containers), 0, typeof(long)),
            new SchemaColumn(nameof(ImageEntity.Created), 1, typeof(DateTime)),
            new SchemaColumn(nameof(ImageEntity.ID), 2, typeof(string)),
            new SchemaColumn(nameof(ImageEntity.Labels), 3, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(ImageEntity.ParentID), 4, typeof(string)),
            new SchemaColumn(nameof(ImageEntity.RepoDigests), 5, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(ImageEntity.RepoTags), 6, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(ImageEntity.SharedSize), 7, typeof(long)),
            new SchemaColumn(nameof(ImageEntity.Size), 8, typeof(long)),
            new SchemaColumn(nameof(ImageEntity.VirtualSize), 9, typeof(long))
        ];
    }
}
