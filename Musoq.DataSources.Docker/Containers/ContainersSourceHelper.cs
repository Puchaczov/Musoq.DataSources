using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Containers;

internal static class ContainersSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ContainersNameToIndexMap;

    public static readonly IReadOnlyDictionary<int, Func<ContainerListResponse, object>>
        ContainersIndexToMethodAccessMap;

    public static readonly ISchemaColumn[] ContainersColumns;

    static ContainersSourceHelper()
    {
        ContainersNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ContainerListResponse.ID), 0 },
            { nameof(ContainerListResponse.Names), 1 },
            { nameof(ContainerListResponse.Image), 2 },
            { nameof(ContainerListResponse.ImageID), 3 },
            { nameof(ContainerListResponse.Command), 4 },
            { nameof(ContainerListResponse.Created), 5 },
            { nameof(ContainerListResponse.Ports), 6 },
            { nameof(ContainerListResponse.SizeRw), 7 },
            { nameof(ContainerListResponse.SizeRootFs), 8 },
            { nameof(ContainerListResponse.Labels), 9 },
            { nameof(ContainerListResponse.State), 10 },
            { nameof(ContainerListResponse.Status), 11 },
            { nameof(ContainerListResponse.NetworkSettings), 12 },
            { nameof(ContainerListResponse.Mounts), 13 },
            { "FlattenPorts", 14 }
        };

        ContainersIndexToMethodAccessMap = new Dictionary<int, Func<ContainerListResponse, object>>
        {
            { 0, info => info.ID },
            { 1, info => info.Names },
            { 2, info => info.Image },
            { 3, info => info.ImageID },
            { 4, info => info.Command },
            { 5, info => info.Created },
            { 6, info => info.Ports.Select(f => $"{f.PrivatePort}:{f.PublicPort}").ToList() },
            { 7, info => info.SizeRw },
            { 8, info => info.SizeRootFs },
            { 9, info => info.Labels },
            { 10, info => info.State },
            { 11, info => info.Status },
            { 12, info => info.NetworkSettings },
            { 13, info => info.Mounts },
            { 14, info => string.Join(",", info.Ports.Select(f => $"{f.PrivatePort}:{f.PublicPort}").ToList()) }
        };

        ContainersColumns =
        [
            new SchemaColumn(nameof(ContainerListResponse.ID), 0, typeof(string)),
            new SchemaColumn(nameof(ContainerListResponse.Names), 1, typeof(IList<string>)),
            new SchemaColumn(nameof(ContainerListResponse.Image), 2, typeof(string)),
            new SchemaColumn(nameof(ContainerListResponse.ImageID), 3, typeof(string)),
            new SchemaColumn(nameof(ContainerListResponse.Command), 4, typeof(string)),
            new SchemaColumn(nameof(ContainerListResponse.Created), 5, typeof(DateTime)),
            new SchemaColumn(nameof(ContainerListResponse.Ports), 6, typeof(IList<string>)),
            new SchemaColumn(nameof(ContainerListResponse.SizeRw), 7, typeof(long)),
            new SchemaColumn(nameof(ContainerListResponse.SizeRootFs), 8, typeof(long)),
            new SchemaColumn(nameof(ContainerListResponse.Labels), 9, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(ContainerListResponse.State), 10, typeof(string)),
            new SchemaColumn(nameof(ContainerListResponse.Status), 11, typeof(string)),
            new SchemaColumn(nameof(ContainerListResponse.NetworkSettings), 12, typeof(SummaryNetworkSettings)),
            new SchemaColumn(nameof(ContainerListResponse.Mounts), 13, typeof(IList<MountPoint>)),
            new SchemaColumn("FlattenPorts", 14, typeof(string))
        ];
    }
}