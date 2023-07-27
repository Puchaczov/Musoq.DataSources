using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Jobs;

internal class JobsSource : RowSourceBase<JobEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public JobsSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var jobs = _kubernetesApi.ListJobsForAllNamespaces();

        chunkedSource.Add(
            jobs.Items.Select(c => new EntityResolver<JobEntity>(MapV1JobToJobEntity(c), JobsSourceHelper.JobsNameToIndexMap, JobsSourceHelper.JobsIndexToMethodAccessMap)).ToList());
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