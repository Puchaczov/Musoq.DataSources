using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodLogs;

internal class PodLogsSource : RowSourceBase<PodLogsEntity>
{
    private readonly IKubernetesApi _kubernetesApi;
    private readonly string _podName;
    private readonly string _containerName;
    private readonly string _namespaceName;

    public PodLogsSource(IKubernetesApi kubernetesApi, string podName, string containerName, string namespaceName)
    {
        _kubernetesApi = kubernetesApi;
        _podName = podName;
        _containerName = containerName;
        _namespaceName = namespaceName;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var logsStream = _kubernetesApi.ReadNamespacedPodLogs(_podName, _namespaceName, _containerName);
        
        var logs = new List<PodLogsEntity>();
        const int chunkSize = 1000;
        using var reader = new StreamReader(logsStream);
        var line = reader.ReadLine();
        
        while (line != null)
        {
            var log = new PodLogsEntity
            {
                Namespace = _namespaceName,
                Name = _podName,
                ContainerName = _containerName,
                Line = line
            };
            
            logs.Add(log);
            
            if (logs.Count == chunkSize)
            {
                chunkedSource.Add(logs.Select(logEntity => new EntityResolver<PodLogsEntity>(
                    logEntity,
                    PodLogsSourceHelper.PodLogsNameToIndexMap,
                    PodLogsSourceHelper.PodLogsIndexToMethodAccessMap)).ToList());
                logs.Clear();
            }

            line = reader.ReadLine();
        }
        
        chunkedSource.Add(logs.Select(logEntity => new EntityResolver<PodLogsEntity>(
            logEntity,
            PodLogsSourceHelper.PodLogsNameToIndexMap,
            PodLogsSourceHelper.PodLogsIndexToMethodAccessMap)).ToList());
    }
}