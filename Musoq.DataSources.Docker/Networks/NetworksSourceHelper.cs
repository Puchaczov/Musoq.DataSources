using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Networks;

internal static class NetworksSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> NetworksNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<NetworkEntity, object?>> NetworksIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] NetworksColumns;

    static NetworksSourceHelper()
    {
        NetworksNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(NetworkEntity.Name), 0 },
            { nameof(NetworkEntity.ID), 1 },
            { nameof(NetworkEntity.Created), 2 },
            { nameof(NetworkEntity.Scope), 3 },
            { nameof(NetworkEntity.Driver), 4 },
            { nameof(NetworkEntity.EnableIPv6), 5 },
            { nameof(NetworkEntity.IPAM), 6 },
            { nameof(NetworkEntity.Internal), 7 },
            { nameof(NetworkEntity.Attachable), 8 },
            { nameof(NetworkEntity.Ingress), 9 },
            { nameof(NetworkEntity.ConfigFrom), 10 },
            { nameof(NetworkEntity.ConfigOnly), 11 },
            { nameof(NetworkEntity.Containers), 12 },
            { nameof(NetworkEntity.Options), 13 },
            { nameof(NetworkEntity.Labels), 14 },
            { nameof(NetworkEntity.Peers), 15 },
            { nameof(NetworkEntity.Services), 16 }
        };

        NetworksIndexToMethodAccessMap = new Dictionary<int, Func<NetworkEntity, object?>>
        {
            { 0, info => info.Name },
            { 1, info => info.ID },
            { 2, info => info.Created },
            { 3, info => info.Scope },
            { 4, info => info.Driver },
            { 5, info => info.EnableIPv6 },
            { 6, info => info.IPAM },
            { 7, info => info.Internal },
            { 8, info => info.Attachable },
            { 9, info => info.Ingress },
            { 10, info => info.ConfigFrom },
            { 11, info => info.ConfigOnly },
            { 12, info => info.Containers },
            { 13, info => info.Options },
            { 14, info => info.Labels },
            { 15, info => info.Peers },
            { 16, info => info.Services }
        };

        NetworksColumns =
        [
            new SchemaColumn(nameof(NetworkEntity.Name), 0, typeof(string)),
            new SchemaColumn(nameof(NetworkEntity.ID), 1, typeof(string)),
            new SchemaColumn(nameof(NetworkEntity.Created), 2, typeof(DateTime)),
            new SchemaColumn(nameof(NetworkEntity.Scope), 3, typeof(string)),
            new SchemaColumn(nameof(NetworkEntity.Driver), 4, typeof(string)),
            new SchemaColumn(nameof(NetworkEntity.EnableIPv6), 5, typeof(bool)),
            new SchemaColumn(nameof(NetworkEntity.IPAM), 6, typeof(string)),
            new SchemaColumn(nameof(NetworkEntity.Internal), 7, typeof(bool)),
            new SchemaColumn(nameof(NetworkEntity.Attachable), 8, typeof(bool)),
            new SchemaColumn(nameof(NetworkEntity.Ingress), 9, typeof(bool)),
            new SchemaColumn(nameof(NetworkEntity.ConfigFrom), 10, typeof(string)),
            new SchemaColumn(nameof(NetworkEntity.ConfigOnly), 11, typeof(bool)),
            new SchemaColumn(nameof(NetworkEntity.Containers), 12, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(NetworkEntity.Options), 13, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(NetworkEntity.Labels), 14, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(NetworkEntity.Peers), 15, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(NetworkEntity.Services), 16, typeof(IReadOnlyDictionary<string, string>))
        ];
    }
}
