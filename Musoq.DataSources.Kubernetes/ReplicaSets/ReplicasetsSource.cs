using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.ReplicaSets;

internal class ReplicaSetsSource : RowSourceBase<ReplicaSetEntity>
{
    private const string ReplicaSetsSourceName = "kubernetes_replicasets";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public ReplicaSetsSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(ReplicaSetsSourceName);
        
        try
        {
            var replicaSets = _client.ListReplicaSetsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(ReplicaSetsSourceName, replicaSets.Items.Count);

            chunkedSource.Add(
                replicaSets.Items.Select(c => new EntityResolver<ReplicaSetEntity>(MapV1ReplicaSetToReplicasetEntity(c), ReplicaSetsSourceHelper.ReplicaSetsNameToIndexMap, ReplicaSetsSourceHelper.ReplicaSetsIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(ReplicaSetsSourceName, replicaSets.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(ReplicaSetsSourceName, 0);
            throw;
        }
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