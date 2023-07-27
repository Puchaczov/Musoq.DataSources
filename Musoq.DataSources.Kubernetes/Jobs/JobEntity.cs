namespace Musoq.DataSources.Kubernetes.Jobs;

public class JobEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }

    public int Completions { get; set; }
    
    public TimeSpan? Duration { get; set; }
    
    public string Images { get; set; }
    
    public string Containers { get; set; }
    
    public DateTime? Age { get; set; }
}