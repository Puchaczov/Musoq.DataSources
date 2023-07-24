namespace Musoq.DataSources.Kubernetes.StatefulSets;

public class StatefulSetEntity
{
    public string Namespace { get; set; }

    public string Name { get; set; }

    public int? Replicas { get; set; }

    public DateTime? Age { get; set; }
}