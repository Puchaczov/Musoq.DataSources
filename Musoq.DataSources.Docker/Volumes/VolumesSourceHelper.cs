using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Volumes;

internal static class VolumesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> VolumesNameToIndexMap;

    public static readonly ISchemaColumn[] VolumesColumns;

    static VolumesSourceHelper()
    {
        VolumesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(VolumeEntity.CreatedAt), 0 },
            { nameof(VolumeEntity.Driver), 1 },
            { nameof(VolumeEntity.Labels), 2 },
            { nameof(VolumeEntity.Mountpoint), 3 },
            { nameof(VolumeEntity.Name), 4 },
            { nameof(VolumeEntity.Options), 5 },
            { nameof(VolumeEntity.Scope), 6 },
            { nameof(VolumeEntity.Status), 7 },
            { nameof(VolumeEntity.UsageData), 8 }
        };

        VolumesColumns =
        [
            new SchemaColumn(nameof(VolumeEntity.CreatedAt), 0, typeof(string)),
            new SchemaColumn(nameof(VolumeEntity.Driver), 1, typeof(string)),
            new SchemaColumn(nameof(VolumeEntity.Labels), 2, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(VolumeEntity.Mountpoint), 3, typeof(string)),
            new SchemaColumn(nameof(VolumeEntity.Name), 4, typeof(string)),
            new SchemaColumn(nameof(VolumeEntity.Options), 5, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(VolumeEntity.Scope), 6, typeof(string)),
            new SchemaColumn(nameof(VolumeEntity.Status), 7, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(VolumeEntity.UsageData), 8, typeof(string))
        ];
    }
}
