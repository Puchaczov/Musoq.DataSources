namespace Musoq.DataSources.Kubernetes.Nodes;

public class NodeEntity
{
    public string Name { get; set; }
    
    public string Status { get; set; }
    
    public string Roles { get; set; }
    
    public DateTime? Age { get; set; }
    
    public string Version { get; set; }
    
    public string Kernel { get; set; }
    
    public string OS { get; set; }
    
    public string Architecture { get; set; }
    
    public string ContainerRuntime { get; set; }
    
    public string Cpu { get; set; }
    
    public string Memory { get; set; }
}