using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PersistentVolumeClaims;

internal static class PersistentVolumeClaimsSourceHelper
{
    internal static readonly IDictionary<string, int> PersistentVolumeClaimsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(PersistentVolumeClaimEntity.Namespace), 0},
        {nameof(PersistentVolumeClaimEntity.Name), 1},
        {nameof(PersistentVolumeClaimEntity.Capacity), 2},
        {nameof(PersistentVolumeClaimEntity.Volume), 3},
        {nameof(PersistentVolumeClaimEntity.Status), 4},
        {nameof(PersistentVolumeClaimEntity.Age), 5}
    };

    internal static readonly IDictionary<int, Func<PersistentVolumeClaimEntity, object?>>
        PersistentVolumeClaimsIndexToMethodAccessMap = new Dictionary<int, Func<PersistentVolumeClaimEntity, object?>>
        {
            {0, c => c.Namespace},
            {1, c => c.Name},
            {2, c => c.Capacity},
            {3, c => c.Volume},
            {4, c => c.Status},
            {5, c => c.Age}
        };

    internal static readonly ISchemaColumn[] PersistentVolumeClaimsColumns =
    [
        new SchemaColumn(nameof(PersistentVolumeClaimEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeClaimEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeClaimEntity.Capacity), 2, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeClaimEntity.Volume), 3, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeClaimEntity.Status), 4, typeof(string)),
        new SchemaColumn(nameof(PersistentVolumeClaimEntity.Age), 5, typeof(DateTime?))
    ];
}