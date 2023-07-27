using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.ReplicaSets;

internal static class ReplicaSetsSourceHelper
{
    internal static readonly IDictionary<string, int> ReplicaSetsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(ReplicaSetEntity.Namespace), 0},
        {nameof(ReplicaSetEntity.Name), 1},
        {nameof(ReplicaSetEntity.Desired), 2},
        {nameof(ReplicaSetEntity.Current), 3},
        {nameof(ReplicaSetEntity.Ready), 4},
        {nameof(ReplicaSetEntity.Age), 5}
    };

    internal static readonly IDictionary<int, Func<ReplicaSetEntity, object?>> ReplicaSetsIndexToMethodAccessMap = new Dictionary<int, Func<ReplicaSetEntity, object?>>
    {
        {0, t => t.Namespace},
        {1, t => t.Name},
        {2, t => t.Desired},
        {3, t => t.Current},
        {4, t => t.Ready},
        {5, t => t.Age}
    };
    
    internal static readonly ISchemaColumn[] ReplicaSetsColumns = {
        new SchemaColumn(nameof(ReplicaSetEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(ReplicaSetEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(ReplicaSetEntity.Desired), 2, typeof(int?)),
        new SchemaColumn(nameof(ReplicaSetEntity.Current), 3, typeof(int)),
        new SchemaColumn(nameof(ReplicaSetEntity.Ready), 4, typeof(int?)),
        new SchemaColumn(nameof(ReplicaSetEntity.Age), 5, typeof(DateTime?))
    };
}