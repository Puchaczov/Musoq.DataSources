using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Configmaps;

internal class ConfigmapsSource : RowSourceBase<ConfigmapEntity>
{
    private readonly IKubernetesApi _client;

    public ConfigmapsSource(IKubernetesApi client)
    {
        _client = client;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var configmaps = _client.ListConfigMapsForAllNamespaces();

        chunkedSource.Add(
            configmaps.Items.Select(c => new EntityResolver<ConfigmapEntity>(MapV1ConfigmapToConfigmapEntity(c), ConfigmapsSourceHelper.ConfigmapsNameToIndexMap, ConfigmapsSourceHelper.ConfigmapsIndexToMethodAccessMap)).ToList());
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