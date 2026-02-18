using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodLogs;

internal static class PodLogsSourceHelper
{
    public static readonly ISchemaColumn[] PodLogsColumns =
    [
        new SchemaColumn(nameof(PodLogsEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(PodLogsEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(PodLogsEntity.ContainerName), 2, typeof(string)),
        new SchemaColumn(nameof(PodLogsEntity.Line), 3, typeof(string))
    ];

    public static readonly IReadOnlyDictionary<string, int> PodLogsNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(PodLogsEntity.Namespace), 0 },
        { nameof(PodLogsEntity.Name), 1 },
        { nameof(PodLogsEntity.ContainerName), 2 },
        { nameof(PodLogsEntity.Line), 3 }
    };

    public static readonly IReadOnlyDictionary<int, Func<PodLogsEntity, object?>> PodLogsIndexToMethodAccessMap =
        new Dictionary<int, Func<PodLogsEntity, object?>>
        {
            { 0, f => f.Namespace },
            { 1, f => f.Name },
            { 2, f => f.ContainerName },
            { 3, f => f.Line }
        };
}