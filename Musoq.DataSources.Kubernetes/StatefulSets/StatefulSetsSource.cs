using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.StatefulSets;

internal class StatefulSetsSource : RowSourceBase<StatefulSetEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public StatefulSetsSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var statefulSets = _kubernetesApi.ListStatefulSetsForAllNamespaces();

        chunkedSource.Add(
            statefulSets.Items.Select(c => new EntityResolver<StatefulSetEntity>(MapV1StatefulSetToStatefulSetEntity(c),
                StatefulSetsSourceHelper.StatefulSetsNameToIndexMap,
                StatefulSetsSourceHelper.StatefulSetsIndexToMethodAccessMap)).ToList());
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