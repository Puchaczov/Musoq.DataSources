namespace Musoq.DataSources.Kubernetes.Services;

public class ServiceEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public string Type { get; set; }
    
    public string ClusterIP { get; set; }
    
    public string ExternalIP { get; set; }
    
    public string Ports { get; set; }
}