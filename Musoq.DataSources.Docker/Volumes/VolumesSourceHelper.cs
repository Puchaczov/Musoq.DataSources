using Docker.DotNet.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Docker.Volumes;

internal static class VolumesSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> VolumesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<VolumeResponse, object>> VolumesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] VolumesColumns;

    static VolumesSourceHelper()
    {
        VolumesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(VolumeResponse.CreatedAt), 0},
            {nameof(VolumeResponse.Driver), 1},
            {nameof(VolumeResponse.Labels), 2},
            {nameof(VolumeResponse.Mountpoint), 3},
            {nameof(VolumeResponse.Name), 4},
            {nameof(VolumeResponse.Options), 5},
            {nameof(VolumeResponse.Scope), 6},
            {nameof(VolumeResponse.Status), 7},
            {nameof(VolumeResponse.UsageData), 8}
        };
        
        VolumesIndexToMethodAccessMap = new Dictionary<int, Func<VolumeResponse, object>>
        {
            {0, info => info.CreatedAt},
            {1, info => info.Driver},
            {2, info => info.Labels},
            {3, info => info.Mountpoint},
            {4, info => info.Name},
            {5, info => info.Options},
            {6, info => info.Scope},
            {7, info => info.Status},
            {8, info => info.UsageData}
        };
        
        VolumesColumns =
        [
            new SchemaColumn(nameof(VolumeResponse.CreatedAt), 0, typeof(string)),
            new SchemaColumn(nameof(VolumeResponse.Driver), 1, typeof(string)),
            new SchemaColumn(nameof(VolumeResponse.Labels), 2, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(VolumeResponse.Mountpoint), 3, typeof(string)),
            new SchemaColumn(nameof(VolumeResponse.Name), 4, typeof(string)),
            new SchemaColumn(nameof(VolumeResponse.Options), 5, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(VolumeResponse.Scope), 6, typeof(string)),
            new SchemaColumn(nameof(VolumeResponse.Status), 7, typeof(IDictionary<string, string>)),
            new SchemaColumn(nameof(VolumeResponse.UsageData), 8, typeof(VolumeUsageData))
        ];
    }
}