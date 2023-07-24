namespace Musoq.DataSources.Kubernetes.Deployments;

public class DeploymentEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public DateTime? CreationTimestamp { get; set; }
    
    public long? Generation { get; set; }
    
    public string ResourceVersion { get; set; }
    
    public string Image { get; set; }
    
    public string ImagePullPolicy { get; set; }
    
    public string RestartPolicy { get; set; }
    
    public string Type { get; set; }
    
    public string Status { get; set; }
    
    public string ClusterIP { get; set; }
    
    public string ExternalIP { get; set; }
    
    public string Ports { get; set; }
}