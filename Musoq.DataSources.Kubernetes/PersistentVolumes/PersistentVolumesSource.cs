using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PersistentVolumes;

internal class PersistentVolumesSource : RowSourceBase<PersistentVolumeEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public PersistentVolumesSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var volumes = _kubernetesApi.ListPersistentVolumes();

        chunkedSource.Add(
            volumes.Items.Select(c => new EntityResolver<PersistentVolumeEntity>(MapV1PersistentVolumeToPersistentVolumeEntity(c), PersistentVolumesSourceHelper.PersistentVolumesNameToIndexMap, PersistentVolumesSourceHelper.PersistentVolumesIndexToMethodAccessMap)).ToList());
    }

    private static PersistentVolumeEntity MapV1PersistentVolumeToPersistentVolumeEntity(V1PersistentVolume v1PersistentVolume)
    {
        return new PersistentVolumeEntity
        {
            Name = v1PersistentVolume.Metadata.Name,
            Namespace = v1PersistentVolume.Metadata.NamespaceProperty,
            AccessModes = string.Join(",", v1PersistentVolume.Spec.AccessModes),
            ReclaimPolicy = v1PersistentVolume.Spec.PersistentVolumeReclaimPolicy,
            Status = v1PersistentVolume.Status.Phase,
            Claim = v1PersistentVolume.Spec.ClaimRef?.Name ?? string.Empty,
            StorageClass = v1PersistentVolume.Spec.StorageClassName,
            Reason = v1PersistentVolume.Status.Reason,
            Age = v1PersistentVolume.Metadata.CreationTimestamp
        };
    }
}