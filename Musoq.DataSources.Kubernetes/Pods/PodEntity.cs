using k8s.Models;

namespace Musoq.DataSources.Kubernetes.Pods;

public class PodEntity : IWithObjectMetadata
{
    public PodEntity(V1Pod pod)
    {
        RawObject = pod;
    }

    public string Namespace { get; init; }

    public string Name { get; init; }

    public string ContainersNames { get; init; }

    public string PF { get; init; }

    public bool Ready { get; init; }

    public string Restarts { get; init; }

    public string Statuses { get; init; }

    public string IP { get; init; }

    internal V1Pod RawObject { get; }

    public V1ObjectMeta Metadata => RawObject.Metadata;
}