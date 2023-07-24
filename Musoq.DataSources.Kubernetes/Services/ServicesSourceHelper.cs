using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Services;

internal static class ServicesSourceHelper
{
    public static readonly IDictionary<string, int> ServicesNameToIndexMap;
    public static readonly IDictionary<int, Func<ServiceEntity, object?>> ServicesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] ServicesColumns;

    static ServicesSourceHelper()
    {
        ServicesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(ServiceEntity.Namespace), 0},
            {nameof(ServiceEntity.Name), 1},
            {nameof(ServiceEntity.Type), 2},
            {nameof(ServiceEntity.ClusterIP), 3},
            {nameof(ServiceEntity.ExternalIP), 4},
            {nameof(ServiceEntity.Ports), 5}
        };
        
        ServicesIndexToMethodAccessMap = new Dictionary<int, Func<ServiceEntity, object?>>
        {
            {0, info => info.Namespace},
            {1, info => info.Name},
            {2, info => info.Type},
            {3, info => info.ClusterIP},
            {4, info => info.ExternalIP},
            {5, info => info.Ports}
        };
        
        ServicesColumns = new ISchemaColumn[]
        {
            new SchemaColumn(nameof(ServiceEntity.Namespace), 0, typeof(string)),
            new SchemaColumn(nameof(ServiceEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(ServiceEntity.Type), 2, typeof(string)),
            new SchemaColumn(nameof(ServiceEntity.ClusterIP), 3, typeof(string)),
            new SchemaColumn(nameof(ServiceEntity.ExternalIP), 4, typeof(string)),
            new SchemaColumn(nameof(ServiceEntity.Ports), 5, typeof(string))
        };
    }
}