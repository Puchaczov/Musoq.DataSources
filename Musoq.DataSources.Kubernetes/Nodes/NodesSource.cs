using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Nodes;

internal class NodesSource : RowSourceBase<NodeEntity>
{
    private readonly IKubernetesApi _client;

    public NodesSource(IKubernetesApi client)
    {
        _client = client;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var nodes = _client.ListNodes();

        chunkedSource.Add(
            nodes.Items.Select(c => 
                new EntityResolver<NodeEntity>(MapV1NodeToNodeEntity(c), NodesSourceHelper.NodesNameToIndexMap, NodesSourceHelper.NodesIndexToMethodAccessMap)).ToList());
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