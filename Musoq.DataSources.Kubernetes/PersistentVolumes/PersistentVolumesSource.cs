using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PersistentVolumes;

internal class PersistentVolumesSource : RowSourceBase<PersistentVolumeEntity>
{
    private const string PersistentVolumesSourceName = "kubernetes_persistentvolumes";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public PersistentVolumesSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(PersistentVolumesSourceName);
        long totalRowsProcessed = 0;
        
        try
        {
            var volumes = _kubernetesApi.ListPersistentVolumes();
            totalRowsProcessed = volumes.Items.Count;
            _runtimeContext.ReportDataSourceRowsKnown(PersistentVolumesSourceName, totalRowsProcessed);

            chunkedSource.Add(
                volumes.Items.Select(c => new EntityResolver<PersistentVolumeEntity>(MapV1PersistentVolumeToPersistentVolumeEntity(c), PersistentVolumesSourceHelper.PersistentVolumesNameToIndexMap, PersistentVolumesSourceHelper.PersistentVolumesIndexToMethodAccessMap)).ToList());
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(PersistentVolumesSourceName, totalRowsProcessed);
        }
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