namespace Musoq.DataSources.Kubernetes.DaemonSets;

public class DaemonSetEntity
{
    public string Namespace { get; set; }

    public string Name { get; set; }

    public int Desired { get; set; }

    public int Current { get; set; }

    public int Ready { get; set; }

    public int? UpToDate { get; set; }

    public int? Available { get; set; }

    public DateTime? Age { get; set; }
}