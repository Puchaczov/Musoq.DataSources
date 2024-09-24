using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.DaemonSets;

internal static class DaemonSetsSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> DaemonSetsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(DaemonSetEntity.Namespace), 0},
        {nameof(DaemonSetEntity.Name), 1},
        {nameof(DaemonSetEntity.Desired), 2},
        {nameof(DaemonSetEntity.Current), 3},
        {nameof(DaemonSetEntity.Ready), 4},
        {nameof(DaemonSetEntity.UpToDate), 5},
        {nameof(DaemonSetEntity.Available), 6},
        {nameof(DaemonSetEntity.Age), 7}
    };

    internal static readonly IReadOnlyDictionary<int, Func<DaemonSetEntity, object?>> DaemonSetsIndexToMethodAccessMap = new Dictionary<int, Func<DaemonSetEntity, object?>>
    {
        {0, c => c.Namespace},
        {1, c => c.Name},
        {2, c => c.Desired},
        {3, c => c.Current},
        {4, c => c.Ready},
        {5, c => c.UpToDate},
        {6, c => c.Available},
        {7, c => c.Age}
    };
    
    internal static readonly ISchemaColumn[] DaemonSetsColumns =
    [
        new SchemaColumn(nameof(DaemonSetEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(DaemonSetEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(DaemonSetEntity.Desired), 2, typeof(int)),
        new SchemaColumn(nameof(DaemonSetEntity.Current), 3, typeof(int)),
        new SchemaColumn(nameof(DaemonSetEntity.Ready), 4, typeof(int)),
        new SchemaColumn(nameof(DaemonSetEntity.UpToDate), 5, typeof(int?)),
        new SchemaColumn(nameof(DaemonSetEntity.Available), 6, typeof(int?)),
        new SchemaColumn(nameof(DaemonSetEntity.Age), 7, typeof(DateTime?))
    ];
}