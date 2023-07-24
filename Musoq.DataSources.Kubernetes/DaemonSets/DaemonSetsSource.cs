using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.DaemonSets;

internal class DaemonSetsSource : RowSourceBase<DaemonSetEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public DaemonSetsSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var daemonSets = _kubernetesApi.ListDaemonSetsForAllNamespaces();
        chunkedSource.Add(
            daemonSets.Items.Select(c => new EntityResolver<DaemonSetEntity>(MapV1DaemonSetToDaemonSetEntity(c), DaemonSetsSourceHelper.DaemonSetsNameToIndexMap, DaemonSetsSourceHelper.DaemonSetsIndexToMethodAccessMap)).ToList());
    }

    private static DaemonSetEntity MapV1DaemonSetToDaemonSetEntity(V1DaemonSet v1DaemonSet)
    {
        return new DaemonSetEntity
        {
            Name = v1DaemonSet.Metadata.Name,
            Namespace = v1DaemonSet.Metadata.NamespaceProperty,
            Desired = v1DaemonSet.Status.DesiredNumberScheduled,
            Current = v1DaemonSet.Status.CurrentNumberScheduled,
            Ready = v1DaemonSet.Status.NumberReady,
            UpToDate = v1DaemonSet.Status.UpdatedNumberScheduled,
            Available = v1DaemonSet.Status.NumberAvailable,
            Age = v1DaemonSet.Metadata.CreationTimestamp
        };
    }
}