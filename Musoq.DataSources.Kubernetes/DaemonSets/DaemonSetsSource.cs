using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.DaemonSets;

internal class DaemonSetsSource : RowSourceBase<DaemonSetEntity>
{
    private const string DaemonSetsSourceName = "kubernetes_daemonsets";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public DaemonSetsSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(DaemonSetsSourceName);
        
        try
        {
            var daemonSets = _kubernetesApi.ListDaemonSetsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(DaemonSetsSourceName, daemonSets.Items.Count);
            
            chunkedSource.Add(
                daemonSets.Items.Select(c => new EntityResolver<DaemonSetEntity>(MapV1DaemonSetToDaemonSetEntity(c), DaemonSetsSourceHelper.DaemonSetsNameToIndexMap, DaemonSetsSourceHelper.DaemonSetsIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(DaemonSetsSourceName, daemonSets.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(DaemonSetsSourceName, 0);
            throw;
        }
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