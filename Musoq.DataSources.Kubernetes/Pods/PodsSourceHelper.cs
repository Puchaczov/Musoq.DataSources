using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Pods;

internal static class PodsSourceHelper
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
            {nameof(PodEntity.ContainersNames), 2},
            {nameof(PodEntity.PF), 3},
            {nameof(PodEntity.Ready), 4},
            {nameof(PodEntity.Restarts), 5},
            {nameof(PodEntity.Statuses), 6},
            {nameof(PodEntity.IP), 7}
        };
        
        PodsIndexToMethodAccessMap = new Dictionary<int, Func<PodEntity, object?>>
        {
            {0, info => info.Namespace},
            {1, info => info.Name},
            {2, info => info.ContainersNames},
            {3, info => info.PF},
            {4, info => info.Ready},
            {5, info => info.Restarts},
            {6, info => info.Statuses},
            {7, info => info.IP}
        };
        
        PodsColumns = new ISchemaColumn[]
        {
            new SchemaColumn(nameof(PodEntity.Namespace), 0, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(PodEntity.ContainersNames), 2, typeof(string)),
            new SchemaColumn(nameof(PodEntity.PF), 3, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Ready), 4, typeof(bool)),
            new SchemaColumn(nameof(PodEntity.Restarts), 5, typeof(string)),
            new SchemaColumn(nameof(PodEntity.Statuses), 6, typeof(string)),
            new SchemaColumn(nameof(PodEntity.IP), 7, typeof(string))
        };
    }
    
}