using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Configmaps;

internal static class ConfigmapsSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> ConfigmapsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(ConfigmapEntity.Namespace), 0},
        {nameof(ConfigmapEntity.Name), 1},
        {nameof(ConfigmapEntity.Age), 2}
    };

    internal static readonly IReadOnlyDictionary<int, Func<ConfigmapEntity, object?>> ConfigmapsIndexToMethodAccessMap = new Dictionary<int, Func<ConfigmapEntity, object?>>
    {
        {0, t => t.Namespace},
        {1, t => t.Name},
        {2, t => t.Age}
    };
    
    internal static readonly ISchemaColumn[] ConfigmapsColumns =
    [
        new SchemaColumn(nameof(ConfigmapEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(ConfigmapEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(ConfigmapEntity.Age), 2, typeof(DateTime?))
    ];
}