namespace Musoq.DataSources.Kubernetes.SecretData;

public class SecretDataEntity
{
    public string Namespace { get; set; }
    
    public string Name { get; set; }
    
    public string Key { get; set; }
    
    public byte[] Value { get; set; }
}