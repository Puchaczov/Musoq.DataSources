using System.Collections.Concurrent;
using System.Diagnostics;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Pods;

internal class PodsSource : RowSourceBase<PodEntity>
{
    private readonly IKubernetesApi _client;

    public PodsSource(IKubernetesApi client)
    {
        _client = client;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var pods = _client.ListPodsForAllNamespaces();

        chunkedSource.Add(
            pods.Items.Select(c => 
                new EntityResolver<PodEntity>(MapV1PodToPodEntity(c), PodsSourceHelper.PodsNameToIndexMap, PodsSourceHelper.PodsIndexToMethodAccessMap)).ToList());
    }

    private static PodEntity MapV1PodToPodEntity(V1Pod v1Pod)
    {
        var hasAnyStatus = v1Pod.Status.ContainerStatuses?.Any() ?? false;
        var hasAnyContainer = v1Pod.Spec.Containers?.Any() ?? false;

        Debug.WriteLine(v1Pod.Spec.Containers != null, "v1Pod.Spec.Containers != null");
        Debug.WriteLine(v1Pod.Status.ContainerStatuses != null, "v1Pod.Status.ContainerStatuses != null");
        
        return new PodEntity
        {
            Name = v1Pod.Metadata.Name,
            Namespace = v1Pod.Metadata.NamespaceProperty,
            Type = hasAnyContainer ? v1Pod.Spec.Containers[0].Name : "--",
            PF = v1Pod.Status.Phase,
            Ready = hasAnyStatus && v1Pod.Status.ContainerStatuses[0].Ready,
            Restarts = hasAnyStatus ? v1Pod.Status.ContainerStatuses[0].RestartCount : 0,
            Status = hasAnyStatus ? v1Pod.Status.ContainerStatuses[0].State.Running != null ? "Running" : "Not Running" : "Not Running",
            Cpu = hasAnyStatus ? v1Pod.Status.ContainerStatuses[0].Resources.Limits["cpu"].Value : "--",
            Memory = hasAnyStatus ? v1Pod.Status.ContainerStatuses[0].Resources.Limits["memory"].Value : "--",
            IP = v1Pod.Status.PodIP
        };
    }
}