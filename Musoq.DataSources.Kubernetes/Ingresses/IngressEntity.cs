namespace Musoq.DataSources.Kubernetes.Ingresses;

public class IngressEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }

    public string Class { get; set; }
    
    public string Hosts { get; set; }
    
    public string Address { get; set; }
    
    public string Ports { get; set; }
    
    public DateTime? Age { get; set; }
}