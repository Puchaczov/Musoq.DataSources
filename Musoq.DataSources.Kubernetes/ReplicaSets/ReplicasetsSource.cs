using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.ReplicaSets;

internal class ReplicaSetsSource : RowSourceBase<ReplicaSetEntity>
{
    private readonly IKubernetesApi _client;

    public ReplicaSetsSource(IKubernetesApi client)
    {
        _client = client;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var replicaSets = _client.ListReplicaSetsForAllNamespaces();

        chunkedSource.Add(
            replicaSets.Items.Select(c => new EntityResolver<ReplicaSetEntity>(MapV1ReplicaSetToReplicasetEntity(c), ReplicaSetsSourceHelper.ReplicaSetsNameToIndexMap, ReplicaSetsSourceHelper.ReplicaSetsIndexToMethodAccessMap)).ToList());
    }

    private static ReplicaSetEntity MapV1ReplicaSetToReplicasetEntity(V1ReplicaSet v1ReplicaSet)
    {
        return new ReplicaSetEntity
        {
            Name = v1ReplicaSet.Metadata.Name,
            Namespace = v1ReplicaSet.Metadata.NamespaceProperty,
            Desired = v1ReplicaSet.Spec.Replicas,
            Current = v1ReplicaSet.Status.Replicas,
            Ready = v1ReplicaSet.Status.ReadyReplicas,
            Age = v1ReplicaSet.Metadata.CreationTimestamp
        };
    }
}