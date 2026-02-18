using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Pods;

internal class PodsSource : RowSourceBase<PodEntity>
{
    private const string PodsSourceName = "kubernetes_pods";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public PodsSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(PodsSourceName);

        try
        {
            var pods = _client.ListPodsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(PodsSourceName, pods.Items.Count);

            chunkedSource.Add(
                pods.Items.Select(c =>
                        new EntityResolver<PodEntity>(
                            MapV1PodToPodEntity(c),
                            PodsSourceHelper.PodsNameToIndexMap,
                            PodsSourceHelper.PodsIndexToMethodAccessMap))
                    .ToList());

            _runtimeContext.ReportDataSourceEnd(PodsSourceName, pods.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(PodsSourceName, 0);
            throw;
        }
    }

    private static PodEntity MapV1PodToPodEntity(V1Pod v1Pod)
    {
        if (v1Pod is null)
            throw new NullReferenceException(nameof(v1Pod));

        var hasAnyStatus = v1Pod.Status.ContainerStatuses?.Any() ?? false;
        var hasAnyContainer = v1Pod.Spec.Containers?.Any() ?? false;

        if (v1Pod.Spec.Containers is null)
            throw new NullReferenceException(nameof(v1Pod.Spec.Containers));

        if (v1Pod.Status.ContainerStatuses is null)
            throw new NullReferenceException(nameof(v1Pod.Status.ContainerStatuses));

        return new PodEntity(v1Pod)
        {
            Name = v1Pod.Metadata.Name,
            Namespace = v1Pod.Metadata.NamespaceProperty,
            ContainersNames = hasAnyContainer ? string.Join(",", v1Pod.Spec.Containers.Select(f => f.Name)) : "--",
            PF = v1Pod.Status.Phase,
            Ready = hasAnyStatus && v1Pod.Status.ContainerStatuses.All(f => f.Ready),
            Restarts = hasAnyStatus
                ? string.Join(",", v1Pod.Status.ContainerStatuses.Select(f => f.RestartCount))
                : "--",
            Statuses = hasAnyStatus
                ? string.Join(",",
                    v1Pod.Status.ContainerStatuses.Select(f => f.State.Running != null ? "Running" : "Not Running"))
                : "Empty",
            IP = v1Pod.Status.PodIP
        };
    }
}