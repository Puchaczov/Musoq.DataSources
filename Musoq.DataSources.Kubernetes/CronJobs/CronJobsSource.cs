using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.CronJobs;

internal class CronJobsSource : RowSourceBase<CronJobEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public CronJobsSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var cronJobs = _kubernetesApi.ListCronJobsForAllNamespaces();

        chunkedSource.Add(
            cronJobs.Items.Select(cj => new EntityResolver<CronJobEntity>(MapV1CronJobToCronJobEntity(cj),
                    CronJobsSourceHelper.CronJobsNameToIndexMap, CronJobsSourceHelper.CronJobsIndexToMethodAccessMap))
                .ToList());
    }

    private static CronJobEntity MapV1CronJobToCronJobEntity(V1CronJob v1CronJob)
    {
        return new CronJobEntity
        {
            Name = v1CronJob.Metadata.Name,
            Namespace = v1CronJob.Metadata.NamespaceProperty,
            Schedule = v1CronJob.Spec.Schedule,
            Statuses = v1CronJob.Status.Active != null ? string.Join(",", v1CronJob.Status.Active.Select(f => f.Name)) : string.Empty,
            LastScheduleTime = v1CronJob.Status.LastScheduleTime
        };
    }
}