using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Jobs;

internal class JobsSource : RowSourceBase<JobEntity>
{
    private const string JobsSourceName = "kubernetes_jobs";
    private readonly IKubernetesApi _kubernetesApi;
    private readonly RuntimeContext _runtimeContext;

    public JobsSource(IKubernetesApi kubernetesApi, RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(JobsSourceName);

        try
        {
            var jobs = _kubernetesApi.ListJobsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(JobsSourceName, jobs.Items.Count);

            chunkedSource.Add(
                jobs.Items.Select(c => new EntityResolver<JobEntity>(MapV1JobToJobEntity(c),
                    JobsSourceHelper.JobsNameToIndexMap, JobsSourceHelper.JobsIndexToMethodAccessMap)).ToList());

            _runtimeContext.ReportDataSourceEnd(JobsSourceName, jobs.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(JobsSourceName, 0);
            throw;
        }
    }

    private static JobEntity MapV1JobToJobEntity(V1Job v1Job)
    {
        return new JobEntity
        {
            Name = v1Job.Metadata.Name,
            Namespace = v1Job.Metadata.NamespaceProperty,
            Completions = v1Job.Spec.Completions ?? 0,
            Duration = v1Job.Status.CompletionTime - v1Job.Status.StartTime,
            Images = string.Join(",", v1Job.Spec.Template.Spec.Containers.Select(c => c.Image)),
            Containers = string.Join(",", v1Job.Spec.Template.Spec.Containers.Select(c => c.Name)),
            Age = v1Job.Metadata.CreationTimestamp
        };
    }
}