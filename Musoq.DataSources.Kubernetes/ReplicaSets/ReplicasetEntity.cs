namespace Musoq.DataSources.Kubernetes.ReplicaSets;

public class ReplicaSetEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public int? Desired { get; set; }
    
    public string Current { get; set; }
    
    public int? Ready { get; set; }
    
    public DateTime? Age { get; set; }
}