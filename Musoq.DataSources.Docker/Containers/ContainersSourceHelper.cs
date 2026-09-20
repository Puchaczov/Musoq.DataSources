using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Containers;

internal static class ContainersSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> ContainersNameToIndexMap;

    public static readonly IReadOnlyDictionary<int, Func<ContainerEntity, object?>>
        ContainersIndexToMethodAccessMap;

    public static readonly ISchemaColumn[] ContainersColumns;

    static ContainersSourceHelper()
    {
        ContainersNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ContainerEntity.ID), 0 },
            { nameof(ContainerEntity.Names), 1 },
            { nameof(ContainerEntity.Image), 2 },
            { nameof(ContainerEntity.ImageID), 3 },
            { nameof(ContainerEntity.Command), 4 },
            { nameof(ContainerEntity.Created), 5 },
            { nameof(ContainerEntity.Ports), 6 },
            { nameof(ContainerEntity.SizeRw), 7 },
            { nameof(ContainerEntity.SizeRootFs), 8 },
            { nameof(ContainerEntity.Labels), 9 },
            { nameof(ContainerEntity.State), 10 },
            { nameof(ContainerEntity.Status), 11 },
            { nameof(ContainerEntity.NetworkSettings), 12 },
            { nameof(ContainerEntity.Mounts), 13 },
            { nameof(ContainerEntity.FlattenPorts), 14 }
        };

        ContainersIndexToMethodAccessMap = new Dictionary<int, Func<ContainerEntity, object?>>
        {
            { 0, info => info.ID },
            { 1, info => info.Names },
            { 2, info => info.Image },
            { 3, info => info.ImageID },
            { 4, info => info.Command },
            { 5, info => info.Created },
            { 6, info => info.Ports },
            { 7, info => info.SizeRw },
            { 8, info => info.SizeRootFs },
            { 9, info => info.Labels },
            { 10, info => info.State },
            { 11, info => info.Status },
            { 12, info => info.NetworkSettings },
            { 13, info => info.Mounts },
            { 14, info => info.FlattenPorts }
        };

        ContainersColumns =
        [
            new SchemaColumn(nameof(ContainerEntity.ID), 0, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.Names), 1, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(ContainerEntity.Image), 2, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.ImageID), 3, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.Command), 4, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.Created), 5, typeof(DateTime)),
            new SchemaColumn(nameof(ContainerEntity.Ports), 6, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(ContainerEntity.SizeRw), 7, typeof(long)),
            new SchemaColumn(nameof(ContainerEntity.SizeRootFs), 8, typeof(long)),
            new SchemaColumn(nameof(ContainerEntity.Labels), 9, typeof(IReadOnlyDictionary<string, string>)),
            new SchemaColumn(nameof(ContainerEntity.State), 10, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.Status), 11, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.NetworkSettings), 12, typeof(string)),
            new SchemaColumn(nameof(ContainerEntity.Mounts), 13, typeof(IReadOnlyList<string>)),
            new SchemaColumn(nameof(ContainerEntity.FlattenPorts), 14, typeof(string))
        ];
    }
}
