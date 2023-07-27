namespace Musoq.DataSources.Kubernetes.Deployments;

public class DeploymentEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public DateTime? CreationTimestamp { get; set; }
    
    public long? Generation { get; set; }
    
    public string ResourceVersion { get; set; }
    
    public string Images { get; set; }
    
    public string ImagePullPolicies { get; set; }
    
    public string RestartPolicy { get; set; }
    
    public string ContainersNames { get; set; }
    
    public string Status { get; set; }
}