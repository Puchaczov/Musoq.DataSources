using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodContainers;

internal class PodContainersSource : RowSourceBase<PodContainerEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public PodContainersSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var pods = _kubernetesApi.ListPodsForAllNamespaces();
        var containers = pods.Items.SelectMany(c =>
                c.Spec.Containers
                    .Select(f => new {c.Metadata, Container = f}))
            .Select(c => new EntityResolver<PodContainerEntity>(
                MapV1MetadataAndV1ContainerToPodContainerEntity(c.Metadata, c.Container),
                PodContainersSourceHelper.PodContainersNameToIndexMap,
                PodContainersSourceHelper.PodContainersIndexToMethodAccessMap)).ToList();
        
        chunkedSource.Add(containers);
    }

    private static PodContainerEntity MapV1MetadataAndV1ContainerToPodContainerEntity(V1ObjectMeta metadata, V1Container container)
    {
        return new PodContainerEntity(metadata, container)
        {
            Name = metadata.Name,
            Namespace = metadata.NamespaceProperty,
            ContainerName = container.Name,
            Image = container.Image,
            ImagePullPolicy = container.ImagePullPolicy,
            Age = metadata.CreationTimestamp,
            SecurityContext = container.SecurityContext,
            Stdin = container.Stdin,
            StdinOnce = container.StdinOnce,
            TerminationMessagePath = container.TerminationMessagePath,
            TerminationMessagePolicy = container.TerminationMessagePolicy,
            Tty = container.Tty,
            WorkingDir = container.WorkingDir
        };
    }
}