using Docker.DotNet.Models;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworkEntity(NetworkResponse response)
{
    [EntityProperty]
    public string? Name => response.Name;

    [EntityProperty]
    public string? ID => response.ID;

    [EntityProperty]
    public DateTime Created => response.Created;

    [EntityProperty]
    public string? Scope => response.Scope;

    [EntityProperty]
    public string? Driver => response.Driver;

    [EntityProperty]
    public bool EnableIPv6 => response.EnableIPv6;

    [EntityProperty]
    public string IPAM => response.IPAM?.Driver ?? string.Empty;

    [EntityProperty]
    public bool Internal => response.Internal;

    [EntityProperty]
    public bool Attachable => response.Attachable;

    [EntityProperty]
    public bool Ingress => response.Ingress;

    [EntityProperty]
    public string ConfigFrom => response.ConfigFrom?.Network ?? string.Empty;

    [EntityProperty]
    public bool ConfigOnly => response.ConfigOnly;

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Containers => response.Containers?.ToDictionary(
        pair => pair.Key,
        pair => $"{pair.Value.Name}:{pair.Value.EndpointID}:{pair.Value.IPv4Address}:{pair.Value.IPv6Address}") ??
        new Dictionary<string, string>();

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Options => response.Options?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Labels => response.Labels?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public IReadOnlyList<string> Peers => response.Peers?.Select(peer => $"{peer.Name}:{peer.IP}").ToArray() ?? [];

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Services => response.Services?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.VIP) ?? new Dictionary<string, string>();
}
