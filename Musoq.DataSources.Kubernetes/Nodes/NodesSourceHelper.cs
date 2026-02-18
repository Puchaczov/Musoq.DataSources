using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Nodes;

internal static class NodesSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> NodesNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(NodeEntity.Name), 0 },
        { nameof(NodeEntity.Status), 1 },
        { nameof(NodeEntity.Roles), 2 },
        { nameof(NodeEntity.Age), 3 },
        { nameof(NodeEntity.Version), 4 },
        { nameof(NodeEntity.Kernel), 5 },
        { nameof(NodeEntity.OS), 6 },
        { nameof(NodeEntity.Architecture), 7 },
        { nameof(NodeEntity.ContainerRuntime), 8 },
        { nameof(NodeEntity.Cpu), 9 },
        { nameof(NodeEntity.Memory), 10 }
    };

    internal static readonly IReadOnlyDictionary<int, Func<NodeEntity, object?>> NodesIndexToMethodAccessMap =
        new Dictionary<int, Func<NodeEntity, object?>>
        {
            { 0, t => t.Name },
            { 1, t => t.Status },
            { 2, t => t.Roles },
            { 3, t => t.Age },
            { 4, t => t.Version },
            { 5, t => t.Kernel },
            { 6, t => t.OS },
            { 7, t => t.Architecture },
            { 8, t => t.ContainerRuntime },
            { 9, t => t.Cpu },
            { 10, t => t.Memory }
        };

    internal static readonly ISchemaColumn[] NodesColumns =
    [
        new SchemaColumn(nameof(NodeEntity.Name), 0, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Status), 1, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Roles), 2, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Age), 3, typeof(DateTime?)),
        new SchemaColumn(nameof(NodeEntity.Version), 4, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Kernel), 5, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.OS), 6, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Architecture), 7, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.ContainerRuntime), 8, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Cpu), 9, typeof(string)),
        new SchemaColumn(nameof(NodeEntity.Memory), 10, typeof(string))
    ];
}