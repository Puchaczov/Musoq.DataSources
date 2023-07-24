namespace Musoq.DataSources.Kubernetes.PersistentVolumeClaims;

public class PersistentVolumeClaimEntity
{
    public string Namespace { get; set; }

    public string Name { get; set; }

    public string Status { get; set; }

    public string Volume { get; set; }

    public string Capacity { get; set; }

    public DateTime? Age { get; set; }
}