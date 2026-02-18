using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PersistentVolumes;

internal static class PersistentVolumesSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> PersistentVolumesNameToIndexMap =
        new Dictionary<string, int>
        {
            { nameof(PersistentVolumeEntity.Name), 0 },
            { nameof(PersistentVolumeEntity.Namespace), 1 },
            { nameof(PersistentVolumeEntity.AccessModes), 2 },
            { nameof(PersistentVolumeEntity.ReclaimPolicy), 3 },
            { nameof(PersistentVolumeEntity.Status), 4 },
            { nameof(PersistentVolumeEntity.Claim), 5 },
            { nameof(PersistentVolumeEntity.StorageClass), 6 },
            { nameof(PersistentVolumeEntity.Reason), 7 },
            { nameof(PersistentVolumeEntity.Age), 8 }
        };

    internal static readonly IReadOnlyDictionary<int, Func<PersistentVolumeEntity, object?>>
        PersistentVolumesIndexToMethodAccessMap = new Dictionary<int, Func<PersistentVolumeEntity, object?>>
        {
            { 0, c => c.Name },
            { 1, c => c.Namespace },
            { 2, c => c.AccessModes },
            { 3, c => c.ReclaimPolicy },
            { 4, c => c.Status },
            { 5, c => c.Claim },
            { 6, c => c.StorageClass },
            { 7, c => c.Reason },
            { 8, c => c.Age }
        };

    internal static readonly ISchemaColumn[] PersistentVolumesColumns =
    [
        new SchemaColumn(nameof(PersistentVolumeEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.Namespace), 1, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.AccessModes), 2, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.ReclaimPolicy), 3, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.Status), 4, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.Claim), 5, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.StorageClass), 6, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.Reason), 7, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeEntity.Age), 8, typeof(DateTime?))
    ];
}