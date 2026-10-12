using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Images;

internal static class ImagesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ImagesNameToIndexMap;

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
