using Docker.DotNet.Models;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Containers;

public class ContainerEntity(ContainerListResponse response)
{
    [EntityProperty]
    public string? ID => ReadID(response);

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> Names => response.Names?.ToArray() ?? [];

    [EntityProperty]
    public string? Image => ReadImage(response);

    [EntityProperty]
    public string? ImageID => ReadImageID(response);

    [EntityProperty]
    public string? Command => ReadCommand(response);

    [EntityProperty]
    public DateTime Created => response.Created;

    [EntityProperty]
    public string? State => ReadState(response);

    [EntityProperty]
    public string? Status => ReadStatus(response);

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> Ports => response.Ports?.Select(FormatPort).ToArray() ?? [];

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Labels => response.Labels?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public long SizeRw => response.SizeRw;

    [EntityProperty]
    public long SizeRootFs => response.SizeRootFs;

    [EntityProperty]
    public string NetworkSettings => ReadNetworkSettings(response);

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> Mounts => response.Mounts?.Select(mount =>
            $"{mount.Type}:{mount.Source}:{mount.Destination}:{mount.Driver}:{mount.Mode}:{mount.Name}:{mount.RW}")
        .ToArray() ?? [];

    [EntityProperty]
    public string FlattenPorts => ReadFlattenPorts(response);

    private static string FormatPort(Port port)
    {
        return $"{port.PrivatePort}:{port.PublicPort}";
    }

    internal static string? ReadID(ContainerListResponse value) => value.ID;
    internal static string? ReadImage(ContainerListResponse value) => value.Image;
    internal static string? ReadImageID(ContainerListResponse value) => value.ImageID;
    internal static string? ReadCommand(ContainerListResponse value) => value.Command;
    internal static string? ReadState(ContainerListResponse value) => value.State;
    internal static string? ReadStatus(ContainerListResponse value) => value.Status;
    internal static string ReadNetworkSettings(ContainerListResponse value) => value.NetworkSettings?.Networks is null
        ? string.Empty
        : string.Join(
            ",",
            value.NetworkSettings.Networks.Select(pair =>
                $"{pair.Key}:{pair.Value.IPAddress}:{pair.Value.MacAddress}"));
    internal static string ReadFlattenPorts(ContainerListResponse value) => string.Join(",", value.Ports?.Select(FormatPort).ToArray() ?? []);
}
