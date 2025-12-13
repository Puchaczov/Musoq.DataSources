using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Nodes;

internal class NodesSource : RowSourceBase<NodeEntity>
{
    private const string NodesSourceName = "kubernetes_nodes";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public NodesSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(NodesSourceName);
        
        try
        {
            var nodes = _client.ListNodes();
            _runtimeContext.ReportDataSourceRowsKnown(NodesSourceName, nodes.Items.Count);

            chunkedSource.Add(
                nodes.Items.Select(c => 
                    new EntityResolver<NodeEntity>(MapV1NodeToNodeEntity(c), NodesSourceHelper.NodesNameToIndexMap, NodesSourceHelper.NodesIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(NodesSourceName, nodes.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(NodesSourceName, 0);
            throw;
        }
    }

    private static NodeEntity MapV1NodeToNodeEntity(V1Node v1Node)
    {     
        return new NodeEntity
        {
            Name = v1Node.Metadata.Name,
            Status = v1Node.Status.Conditions[0].Status,
            Roles = v1Node.Spec.Taints != null ? string.Join(",", v1Node.Spec.Taints.Select(c => c.Key)) : string.Empty,
            Age = v1Node.Metadata.CreationTimestamp,
            Version = v1Node.Status.NodeInfo.KubeletVersion,
            Kernel = v1Node.Status.NodeInfo.KernelVersion,
            OS = v1Node.Status.NodeInfo.OperatingSystem,
            Architecture = v1Node.Status.NodeInfo.Architecture,
            ContainerRuntime = v1Node.Status.NodeInfo.ContainerRuntimeVersion,
            Cpu = v1Node.Status.Allocatable["cpu"].Value,
            Memory = v1Node.Status.Allocatable["memory"].Value
        };
    }
}