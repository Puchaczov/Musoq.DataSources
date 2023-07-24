namespace Musoq.DataSources.Kubernetes.Pods;

public class PodEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public string Type { get; set; }
    
    public string PF { get; set; }
    
    public bool Ready { get; set; }
    
    public int Restarts { get; set; }
    
    public string Status { get; set; }
    
    public string Cpu { get; set; }
    
    public string Memory { get; set; }
    
    public string IP { get; set; }
}