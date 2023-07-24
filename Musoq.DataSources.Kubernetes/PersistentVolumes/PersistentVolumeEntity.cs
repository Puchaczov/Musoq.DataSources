using k8s.Models;

namespace Musoq.DataSources.Kubernetes.PersistentVolumes;

public class PersistentVolumeEntity
{
    public string Name { get; set; }

    public IDictionary<string, ResourceQuantity> Capacity { get; set; }
    
    public string AccessModes { get; set; }
    
    public string ReclaimPolicy { get; set; }
    
    public string Status { get; set; }
    
    public string Claim { get; set; }
    
    public string StorageClass { get; set; }
    
    public string Reason { get; set; }
    
    public DateTime? Age { get; set; }
}