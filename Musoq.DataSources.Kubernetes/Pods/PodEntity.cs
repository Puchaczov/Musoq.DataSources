namespace Musoq.DataSources.Kubernetes.Pods;

public class PodEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public string ContainersNames { get; set; }
    
    public string PF { get; set; }
    
    public bool Ready { get; set; }
    
    public string Restarts { get; set; }
    
    public string Statuses { get; set; }
    
    public string IP { get; set; }
}