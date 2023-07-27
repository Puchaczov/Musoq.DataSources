namespace Musoq.DataSources.Kubernetes.CronJobs;

public class CronJobEntity
{
    public string Namespace { get; set; }

    public string Name { get; set; }

    public string Schedule { get; set; }

    public string Statuses { get; set; }

    public DateTime? LastScheduleTime { get; set; }
}