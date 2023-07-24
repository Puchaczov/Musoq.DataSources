namespace Musoq.DataSources.Kubernetes.Secrets;

public class SecretEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public string Type { get; set; }
    
    public DateTime? Age { get; set; }
}