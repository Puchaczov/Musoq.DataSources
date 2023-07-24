using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PersistentVolumeClaims;

internal class PersistentVolumeClaimsSource : RowSourceBase<PersistentVolumeClaimEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public PersistentVolumeClaimsSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var pvcList = _kubernetesApi.ListPersistentVolumeClaimsForAllNamespaces();

        chunkedSource.Add(
            pvcList.Items.Select(c => new EntityResolver<PersistentVolumeClaimEntity>(
                MapV1PersistentVolumeClaimToPersistentVolumeClaimsEntity(c),
                PersistentVolumeClaimsSourceHelper.PersistentVolumeClaimsNameToIndexMap,
                PersistentVolumeClaimsSourceHelper.PersistentVolumeClaimsIndexToMethodAccessMap)).ToList());
    }

    private static PersistentVolumeClaimEntity MapV1PersistentVolumeClaimToPersistentVolumeClaimsEntity(
        V1PersistentVolumeClaim v1Pvc)
    {
        return new PersistentVolumeClaimEntity()
        {
            Namespace = v1Pvc.Metadata.NamespaceProperty,
            Name = v1Pvc.Metadata.Name,
            Capacity = v1Pvc.Status.Capacity.First().Value.ToString(),
            Volume = v1Pvc.Spec.VolumeName,
            Status = v1Pvc.Status.Phase,
            Age = v1Pvc.Metadata.CreationTimestamp
        };
    }
}