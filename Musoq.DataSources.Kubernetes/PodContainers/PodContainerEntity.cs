using k8s.Models;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Musoq.DataSources.Kubernetes.PodContainers;

public class PodContainerEntity : IWithObjectMetadata
{
    public PodContainerEntity(V1ObjectMeta objectMeta, V1Container rawObject)
    {
        RawObjectMetadata = objectMeta;
        RawObjectContainer = rawObject;
    }

    public string Namespace { get; init; }
    
    public string Name { get; init; }
    
    public string ContainerName { get; init; }
    
    public string Image { get; init; }
    
    public string ImagePullPolicy { get; init; }
    
    public DateTime? Age { get; init; }
    
    public V1SecurityContext SecurityContext { get; init; }
    
    public bool? Stdin { get; init; }
    
    public bool? StdinOnce { get; init; }
    
    public string TerminationMessagePath { get; init; }
    
    public string TerminationMessagePolicy { get; init; }
    
    public bool? Tty { get; init; }
    
    public string WorkingDir { get; init; }
    
    internal V1ObjectMeta RawObjectMetadata { get; init; }
    
    internal V1Container RawObjectContainer { get; init; }

    public V1ObjectMeta Metadata => RawObjectMetadata;
}