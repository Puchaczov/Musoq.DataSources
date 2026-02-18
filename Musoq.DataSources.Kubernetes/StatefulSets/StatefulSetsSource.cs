using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.StatefulSets;

internal class StatefulSetsSource : RowSourceBase<StatefulSetEntity>
{
    private const string StatefulSetsSourceName = "kubernetes_statefulsets";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public StatefulSetsSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(StatefulSetsSourceName);

        try
        {
            var statefulSets = _kubernetesApi.ListStatefulSetsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(StatefulSetsSourceName, statefulSets.Items.Count);

            chunkedSource.Add(
                statefulSets.Items.Select(c => new EntityResolver<StatefulSetEntity>(
                    MapV1StatefulSetToStatefulSetEntity(c),
                    StatefulSetsSourceHelper.StatefulSetsNameToIndexMap,
                    StatefulSetsSourceHelper.StatefulSetsIndexToMethodAccessMap)).ToList());

            _runtimeContext.ReportDataSourceEnd(StatefulSetsSourceName, statefulSets.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(StatefulSetsSourceName, 0);
            throw;
        }
    }

    private static StatefulSetEntity MapV1StatefulSetToStatefulSetEntity(V1StatefulSet v1StatefulSets)
    {
        return new StatefulSetEntity
        {
            Name = v1StatefulSets.Metadata.Name,
            Namespace = v1StatefulSets.Metadata.NamespaceProperty,
            Replicas = v1StatefulSets.Spec.Replicas,
            Age = v1StatefulSets.Metadata.CreationTimestamp
        };
    }
}