using Docker.DotNet.Models;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Volumes;

public class VolumeEntity(VolumeResponse response)
{
    [EntityProperty]
    public string? CreatedAt => ReadCreatedAt(response);

    [EntityProperty]
    public string? Driver => ReadDriver(response);

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Labels => response.Labels?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string? Mountpoint => ReadMountpoint(response);

    [EntityProperty]
    public string? Name => ReadName(response);

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Options => response.Options?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string? Scope => ReadScope(response);

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Status => response.Status?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string UsageData => ReadUsageData(response);

    internal static string? ReadCreatedAt(VolumeResponse value) => value.CreatedAt;
    internal static string? ReadDriver(VolumeResponse value) => value.Driver;
    internal static string? ReadMountpoint(VolumeResponse value) => value.Mountpoint;
    internal static string? ReadName(VolumeResponse value) => value.Name;
    internal static string? ReadScope(VolumeResponse value) => value.Scope;
    internal static string ReadUsageData(VolumeResponse value) => value.UsageData is null
        ? string.Empty
        : $"{value.UsageData.Size}:{value.UsageData.RefCount}";
}
