using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodContainers;

internal class PodContainersSource : RowSourceBase<PodContainerEntity>
{
    private const string PodContainersSourceName = "kubernetes_podcontainers";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public PodContainersSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(PodContainersSourceName);

        try
        {
            var pods = _kubernetesApi.ListPodsForAllNamespaces();
            var containers = pods.Items.SelectMany(c =>
                    c.Spec.Containers
                        .Select(f => new { c.Metadata, Container = f }))
                .Select(c => new EntityResolver<PodContainerEntity>(
                    MapV1MetadataAndV1ContainerToPodContainerEntity(c.Metadata, c.Container),
                    PodContainersSourceHelper.PodContainersNameToIndexMap,
                    PodContainersSourceHelper.PodContainersIndexToMethodAccessMap)).ToList();

            _runtimeContext.ReportDataSourceRowsKnown(PodContainersSourceName, containers.Count);

            chunkedSource.Add(containers);

            _runtimeContext.ReportDataSourceEnd(PodContainersSourceName, containers.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(PodContainersSourceName, 0);
            throw;
        }
    }

    private static PodContainerEntity MapV1MetadataAndV1ContainerToPodContainerEntity(V1ObjectMeta metadata,
        V1Container container)
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