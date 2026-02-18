using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Ingresses;

internal static class IngressesSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> IngressesNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(IngressEntity.Namespace), 0 },
        { nameof(IngressEntity.Name), 1 },
        { nameof(IngressEntity.Class), 2 },
        { nameof(IngressEntity.Hosts), 3 },
        { nameof(IngressEntity.Address), 4 },
        { nameof(IngressEntity.Ports), 5 },
        { nameof(IngressEntity.Age), 6 }
    };

    internal static readonly IReadOnlyDictionary<int, Func<IngressEntity, object?>> IngressesIndexToMethodAccessMap =
        new Dictionary<int, Func<IngressEntity, object?>>
        {
            { 0, c => c.Namespace },
            { 1, c => c.Name },
            { 2, c => c.Class },
            { 3, c => c.Hosts },
            { 4, c => c.Address },
            { 5, c => c.Ports },
            { 6, c => c.Age }
        };

    internal static readonly ISchemaColumn[] IngressesColumns =
    [
        new SchemaColumn(nameof(IngressEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(IngressEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(IngressEntity.Class), 2, typeof(string)),
        new SchemaColumn(nameof(IngressEntity.Hosts), 3, typeof(string)),
        new SchemaColumn(nameof(IngressEntity.Address), 4, typeof(string)),
        new SchemaColumn(nameof(IngressEntity.Ports), 5, typeof(string)),
        new SchemaColumn(nameof(IngressEntity.Age), 6, typeof(DateTime?))
    ];
}