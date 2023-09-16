using k8s.Models;
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
            {nameof(ServiceEntity.Metadata), 0},
            {nameof(ServiceEntity.Spec), 1},
            {nameof(ServiceEntity.Kind), 2},
            {nameof(ServiceEntity.Status), 3}
        };
        
        ServicesIndexToMethodAccessMap = new Dictionary<int, Func<ServiceEntity, object?>>
        {
            {0, t => t.Metadata},
            {1, t => t.Spec},
            {2, t => t.Kind},
            {3, t => t.Status}
        };
        
        ServicesColumns = new ISchemaColumn[]
        {
            new SchemaColumn(nameof(ServiceEntity.Metadata), 0, typeof(V1ObjectMeta)),
            new SchemaColumn(nameof(ServiceEntity.Spec), 1, typeof(V1ServiceSpec)),
            new SchemaColumn(nameof(ServiceEntity.Kind), 2, typeof(string)),
            new SchemaColumn(nameof(ServiceEntity.Status), 3, typeof(V1ServiceStatus))
        };
    }
}