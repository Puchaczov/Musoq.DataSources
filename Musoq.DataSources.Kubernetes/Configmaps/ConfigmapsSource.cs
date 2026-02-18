using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Configmaps;

internal class ConfigmapsSource : RowSourceBase<ConfigmapEntity>
{
    private const string ConfigmapsSourceName = "kubernetes_configmaps";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public ConfigmapsSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(ConfigmapsSourceName);

        try
        {
            var configmaps = _client.ListConfigMapsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(ConfigmapsSourceName, configmaps.Items.Count);

            chunkedSource.Add(
                configmaps.Items.Select(c => new EntityResolver<ConfigmapEntity>(MapV1ConfigmapToConfigmapEntity(c),
                    ConfigmapsSourceHelper.ConfigmapsNameToIndexMap,
                    ConfigmapsSourceHelper.ConfigmapsIndexToMethodAccessMap)).ToList());

            _runtimeContext.ReportDataSourceEnd(ConfigmapsSourceName, configmaps.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(ConfigmapsSourceName, 0);
            throw;
        }
    }

    private static ConfigmapEntity MapV1ConfigmapToConfigmapEntity(V1ConfigMap v1ConfigMap)
    {
        return new ConfigmapEntity
        {
            Name = v1ConfigMap.Metadata.Name,
            Namespace = v1ConfigMap.Metadata.NamespaceProperty,
            Age = v1ConfigMap.Metadata.CreationTimestamp
        };
    }
}