namespace Musoq.DataSources.Kubernetes.PersistentVolumes;

public class PersistentVolumeEntity
{
    public string Namespace { get; set; }

    public string Name { get; set; }

    public string AccessModes { get; set; }

    public string ReclaimPolicy { get; set; }

    public string Status { get; set; }

    public string Claim { get; set; }

    public string StorageClass { get; set; }

    public string Reason { get; set; }

    public DateTime? Age { get; set; }
}