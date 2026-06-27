using Docker.DotNet.Models;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Volumes;

internal class VolumeEntity(VolumeResponse response)
{
    [EntityProperty]
    public string? CreatedAt => response.CreatedAt;

    [EntityProperty]
    public string? Driver => response.Driver;

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Labels => response.Labels?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string? Mountpoint => response.Mountpoint;

    [EntityProperty]
    public string? Name => response.Name;

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Options => response.Options?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string? Scope => response.Scope;

    [EntityProperty]
    public IReadOnlyDictionary<string, string> Status => response.Status?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string UsageData => response.UsageData is null
        ? string.Empty
        : $"{response.UsageData.Size}:{response.UsageData.RefCount}";
}
