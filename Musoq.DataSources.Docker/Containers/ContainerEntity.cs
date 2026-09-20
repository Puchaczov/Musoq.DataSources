using Docker.DotNet.Models;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Containers;

public class ContainerEntity(ContainerListResponse response)
{
    [EntityProperty]
    public string? ID => response.ID;

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> Names => response.Names?.ToArray() ?? [];

    [EntityProperty]
    public string? Image => response.Image;

    [EntityProperty]
    public string? ImageID => response.ImageID;

    [EntityProperty]
    public string? Command => response.Command;

    [EntityProperty]
    public DateTime Created => response.Created;

    [EntityProperty]
    public string? State => response.State;

    [EntityProperty]
    public string? Status => response.Status;

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
    public string NetworkSettings => response.NetworkSettings?.Networks is null
        ? string.Empty
        : string.Join(
            ",",
            response.NetworkSettings.Networks.Select(pair =>
                $"{pair.Key}:{pair.Value.IPAddress}:{pair.Value.MacAddress}"));

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> Mounts => response.Mounts?.Select(mount =>
            $"{mount.Type}:{mount.Source}:{mount.Destination}:{mount.Driver}:{mount.Mode}:{mount.Name}:{mount.RW}")
        .ToArray() ?? [];

    [EntityProperty]
    public string FlattenPorts => string.Join(",", Ports);

    private static string FormatPort(Port port)
    {
        return $"{port.PrivatePort}:{port.PublicPort}";
    }
}
