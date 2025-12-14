using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.CronJobs;

internal class CronJobsSource : RowSourceBase<CronJobEntity>
{
    private const string CronJobsSourceName = "kubernetes_cronjobs";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public CronJobsSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(CronJobsSourceName);
        
        try
        {
            var cronJobs = _kubernetesApi.ListCronJobsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(CronJobsSourceName, cronJobs.Items.Count);

            chunkedSource.Add(
                cronJobs.Items.Select(cj => new EntityResolver<CronJobEntity>(MapV1CronJobToCronJobEntity(cj),
                        CronJobsSourceHelper.CronJobsNameToIndexMap, CronJobsSourceHelper.CronJobsIndexToMethodAccessMap))
                    .ToList());
            
            _runtimeContext.ReportDataSourceEnd(CronJobsSourceName, cronJobs.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(CronJobsSourceName, 0);
            throw;
        }
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