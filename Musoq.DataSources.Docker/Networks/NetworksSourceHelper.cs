using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Networks;

internal static class NetworksSourceHelper
{
    public static readonly IDictionary<string, int> NetworksNameToIndexMap;
    public static readonly IDictionary<int, Func<NetworkResponse, object>> NetworksIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] NetworksColumns;

    static NetworksSourceHelper()
    {
        NetworksNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(NetworkResponse.Name), 0},
            {nameof(NetworkResponse.ID), 1},
            {nameof(NetworkResponse.Created), 2},
            {nameof(NetworkResponse.Scope), 3},
            {nameof(NetworkResponse.Driver), 4},
            {nameof(NetworkResponse.EnableIPv6), 5},
            {nameof(NetworkResponse.IPAM), 6},
            {nameof(NetworkResponse.Internal), 7},
            {nameof(NetworkResponse.Attachable), 8},
            {nameof(NetworkResponse.Ingress), 9},
            {nameof(NetworkResponse.ConfigFrom), 10},
            {nameof(NetworkResponse.ConfigOnly), 11},
            {nameof(NetworkResponse.Containers), 12},
            {nameof(NetworkResponse.Options), 13},
            {nameof(NetworkResponse.Labels), 14},
            {nameof(NetworkResponse.Peers), 15},
            {nameof(NetworkResponse.Services), 16}
        };
        
        NetworksIndexToMethodAccessMap = new Dictionary<int, Func<NetworkResponse, object>>
        {
            {0, info => info.Name},
            {1, info => info.ID},
            {2, info => info.Created},
            {3, info => info.Scope},
            {4, info => info.Driver},
            {5, info => info.EnableIPv6},
            {6, info => info.IPAM},
            {7, info => info.Internal},
            {8, info => info.Attachable},
            {9, info => info.Ingress},
            {10, info => info.ConfigFrom},
            {11, info => info.ConfigOnly},
            {12, info => info.Containers},
            {13, info => info.Options},
            {14, info => info.Labels},
            {15, info => info.Peers},
            {16, info => info.Services}
        };
        
        NetworksColumns =
        [
            new SchemaColumn(nameof(NetworkResponse.Name), 0, typeof(string)),
            new SchemaColumn(nameof(NetworkResponse.ID), 1, typeof(string)),
            new SchemaColumn(nameof(NetworkResponse.Created), 2, typeof(DateTime)),
            new SchemaColumn(nameof(NetworkResponse.Scope), 3, typeof(string)),
            new SchemaColumn(nameof(NetworkResponse.Driver), 4, typeof(string)),
            new SchemaColumn(nameof(NetworkResponse.EnableIPv6), 5, typeof(bool)),
            new SchemaColumn(nameof(NetworkResponse.IPAM), 6, typeof(IPAM)),
            new SchemaColumn(nameof(NetworkResponse.Internal), 7, typeof(bool)),
            new SchemaColumn(nameof(NetworkResponse.Attachable), 8, typeof(bool)),
            new SchemaColumn(nameof(NetworkResponse.Ingress), 9, typeof(bool)),
            new SchemaColumn(nameof(NetworkResponse.ConfigFrom), 10, typeof(ConfigReference)),
            new SchemaColumn(nameof(NetworkResponse.ConfigOnly), 11, typeof(bool)),
            new SchemaColumn(nameof(NetworkResponse.Containers), 12, typeof(IDictionary<string, EndpointResource>)),
            new SchemaColumn(nameof(NetworkResponse.Options), 13, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(NetworkResponse.Labels), 14, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(NetworkResponse.Peers), 15, typeof(IList<PeerInfo>)),
            new SchemaColumn(nameof(NetworkResponse.Services), 16, typeof(IDictionary<string, ServiceInfo>))
        ];
    }
}