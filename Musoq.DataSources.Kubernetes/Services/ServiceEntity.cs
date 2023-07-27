namespace Musoq.DataSources.Kubernetes.Services;

public class ServiceEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public string Type { get; set; }
    
    public string ClusterIP { get; set; }
    
    public string ExternalIPs { get; set; }
    
    public string Ports { get; set; }
}