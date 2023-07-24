using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.StatefulSets;

internal static class StatefulSetsSourceHelper
{
    internal static readonly IDictionary<string, int> StatefulSetsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(StatefulSetEntity.Namespace), 0},
        {nameof(StatefulSetEntity.Name), 1},
        {nameof(StatefulSetEntity.Replicas), 2},
        {nameof(StatefulSetEntity.Age), 3}
    };

    internal static readonly IDictionary<int, Func<StatefulSetEntity, object?>> StatefulSetsIndexToMethodAccessMap =
        new Dictionary<int, Func<StatefulSetEntity, object?>>
        {
            {0, c => c.Namespace},
            {1, c => c.Name},
            {2, c => c.Replicas},
            {3, c => c.Age}
        };

    internal static readonly ISchemaColumn[] StatefulSetsColumns = new ISchemaColumn[]
    {
        new SchemaColumn(nameof(StatefulSetEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(StatefulSetEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(StatefulSetEntity.Replicas), 2, typeof(int?)),
        new SchemaColumn(nameof(StatefulSetEntity.Age), 3, typeof(DateTime?))
    };
}