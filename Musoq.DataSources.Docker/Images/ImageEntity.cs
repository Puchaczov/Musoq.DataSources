using Docker.DotNet.Models;
using Musoq.Plugins.Attributes;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Docker.Images;

public class ImageEntity(ImagesListResponse response)
{
    [EntityProperty]
    public long Containers => response.Containers;

    [EntityProperty]
    public DateTime Created => response.Created;

    [EntityProperty]
    public string? ID => response.ID;

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyDictionary<string, string> Labels => response.Labels?.ToDictionary(
        pair => pair.Key,
        pair => pair.Value) ?? new Dictionary<string, string>();

    [EntityProperty]
    public string? ParentID => response.ParentID;

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> RepoDigests => response.RepoDigests?.ToArray() ?? [];

    [EntityProperty]
    [BindablePropertyAsTable]
    public IReadOnlyList<string> RepoTags => response.RepoTags?.ToArray() ?? [];

    [EntityProperty]
    public long SharedSize => response.SharedSize;

    [EntityProperty]
    public long Size => response.Size;

    [EntityProperty]
    public long VirtualSize => response.VirtualSize;
}
