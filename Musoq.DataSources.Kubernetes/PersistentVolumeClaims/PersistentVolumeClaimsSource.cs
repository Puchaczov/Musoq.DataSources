using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PersistentVolumeClaims;

internal class PersistentVolumeClaimsSource : RowSourceBase<PersistentVolumeClaimEntity>
{
    private const string PersistentVolumeClaimsSourceName = "kubernetes_persistentvolumeclaims";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public PersistentVolumeClaimsSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(PersistentVolumeClaimsSourceName);
        long totalRowsProcessed = 0;
        
        try
        {
            var pvcList = _kubernetesApi.ListPersistentVolumeClaimsForAllNamespaces();
            totalRowsProcessed = pvcList.Items.Count;
            _runtimeContext.ReportDataSourceRowsKnown(PersistentVolumeClaimsSourceName, totalRowsProcessed);

            chunkedSource.Add(
                pvcList.Items.Select(c => new EntityResolver<PersistentVolumeClaimEntity>(
                    MapV1PersistentVolumeClaimToPersistentVolumeClaimsEntity(c),
                    PersistentVolumeClaimsSourceHelper.PersistentVolumeClaimsNameToIndexMap,
                    PersistentVolumeClaimsSourceHelper.PersistentVolumeClaimsIndexToMethodAccessMap)).ToList());
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(PersistentVolumeClaimsSourceName, totalRowsProcessed);
        }
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