namespace Musoq.DataSources.Kubernetes.Configmaps;

public class ConfigmapEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public DateTime? Age { get; set; }
}