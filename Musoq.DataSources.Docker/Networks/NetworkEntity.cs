using Docker.DotNet.Models;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Networks;

public class NetworkEntity(NetworkResponse response)
{
    [EntityProperty]
    public string? Name => ReadName(response);

    [EntityProperty]
    public string? ID => ReadID(response);

    [EntityProperty]
    public DateTime Created => response.Created;

    [EntityProperty]
    public string? Scope => ReadScope(response);

    [EntityProperty]
    public string? Driver => ReadDriver(response);

    [EntityProperty]
    public bool EnableIPv6 => response.EnableIPv6;

    [EntityProperty]
    public string IPAM => ReadIPAM(response);

    [EntityProperty]
    public bool Internal => response.Internal;

    [EntityProperty]
    public bool Attachable => response.Attachable;

    [EntityProperty]
    public bool Ingress => response.Ingress;

    [EntityProperty]
    public string ConfigFrom => ReadConfigFrom(response);

    [EntityProperty]
    public bool ConfigOnly => response.ConfigOnly;

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Containers => response.Containers?.ToDictionary(
        pair => pair.Key,
        pair => $"{pair.Value.Name}:{pair.Value.EndpointID}:{pair.Value.IPv4Address}:{pair.Value.IPv6Address}") ??
        new Dictionary<string, string>();

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Options => response.Options?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Labels => response.Labels?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> Peers => response.Peers?.Select(peer => $"{peer.Name}:{peer.IP}").ToArray() ?? [];

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Services => response.Services?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.VIP) ?? new Dictionary<string, string>();

    internal static string? ReadName(NetworkResponse value) => value.Name;
    internal static string? ReadID(NetworkResponse value) => value.ID;
    internal static string? ReadScope(NetworkResponse value) => value.Scope;
    internal static string? ReadDriver(NetworkResponse value) => value.Driver;
    internal static string ReadIPAM(NetworkResponse value) => value.IPAM?.Driver ?? string.Empty;
    internal static string ReadConfigFrom(NetworkResponse value) => value.ConfigFrom?.Network ?? string.Empty;
}
