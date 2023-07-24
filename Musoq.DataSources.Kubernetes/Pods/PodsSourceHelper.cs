using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Pods;

public static class PodsSourceHelper
{
    public static readonly IDictionary<string, int> PodsNameToIndexMap;
    public static readonly IDictionary<int, Func<PodEntity, object?>> PodsIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] PodsColumns;

    static PodsSourceHelper()
    {
        PodsNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(PodEntity.Namespace), 0},
            {nameof(PodEntity.Name), 1},
            {nameof(PodEntity.Type), 2},
            {nameof(PodEntity.PF), 3},
            {nameof(PodEntity.Ready), 4},
            {nameof(PodEntity.Restarts), 5},
            {nameof(PodEntity.Status), 6},
            {nameof(PodEntity.Cpu), 7},
            {nameof(PodEntity.Memory), 8},
            {nameof(PodEntity.IP), 9}
        };
        
        PodsIndexToMethodAccessMap = new Dictionary<int, Func<PodEntity, object?>>
        {
            {0, info => info.Namespace},
            {1, info => info.Name},
            {2, info => info.Type},
            {3, info => info.PF},
            {4, info => info.Ready},
            {5, info => info.Restarts},
            {6, info => info.Status},
            {7, info => info.Cpu},
            {8, info => info.Memory},
            {9, info => info.IP}
        };
        
        PodsColumns = new ISchemaColumn[]
        {
            new SchemaColumn(nameof(PodEntity.Namespace), 0, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Type), 2, typeof(string)),
            new SchemaColumn(nameof(PodEntity.PF), 3, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Ready), 4, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Restarts), 5, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Status), 6, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Cpu), 7, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Memory), 8, typeof(string)),
            new SchemaColumn(nameof(PodEntity.IP), 9, typeof(string))
        };
    }
    
}