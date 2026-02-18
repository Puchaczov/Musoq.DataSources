using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodLogs;

internal class PodLogsSource : RowSourceBase<PodLogsEntity>
{
    private const string PodLogsSourceName = "kubernetes_podlogs";
    private readonly string _containerName;
    private readonly IKubernetesApi _kubernetesApi;
    private readonly string _namespaceName;
    private readonly string _podName;
    private readonly RuntimeContext _runtimeContext;

    public PodLogsSource(IKubernetesApi kubernetesApi, string podName, string containerName, string namespaceName,
        RuntimeContext runtimeContext)
    {
        _kubernetesApi = kubernetesApi;
        _podName = podName;
        _containerName = containerName;
        _namespaceName = namespaceName;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(PodLogsSourceName);
        long totalRowsProcessed = 0;

        try
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
                totalRowsProcessed++;

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
        finally
        {
            _runtimeContext.ReportDataSourceEnd(PodLogsSourceName, totalRowsProcessed);
        }
    }
}