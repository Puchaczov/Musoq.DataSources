using k8s.Models;

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
    
    internal V1ObjectMeta RawObjectMetadata { get; init; }
    
    internal V1Container RawObjectContainer { get; init; }

    public V1ObjectMeta Metadata => RawObjectMetadata;
}