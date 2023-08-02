using k8s.Models;

namespace Musoq.DataSources.Kubernetes.Deployments;

public class DeploymentEntity : IWithObjectMetadata
{
    public DeploymentEntity(V1Deployment rawObject)
    {
        RawObject = rawObject;
    }

    public string Namespace { get; init; }
    
    public string Name { get; init; }
    
    public DateTime? CreationTimestamp { get; init; }
    
    public long? Generation { get; init; }
    
    public string ResourceVersion { get; init; }
    
    public string Images { get; init; }
    
    public string ImagePullPolicies { get; init; }
    
    public string RestartPolicy { get; init; }
    
    public string ContainersNames { get; init; }
    
    public string Status { get; init; }
    
    internal V1Deployment RawObject { get; init; }
    
    public V1ObjectMeta Metadata => RawObject.Metadata;
}