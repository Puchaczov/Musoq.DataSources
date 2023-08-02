using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodContainers;

internal static class PodContainersSourceHelper
{
    public static readonly IDictionary<string, int> PodContainersNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(PodContainerEntity.Namespace), 0},
        {nameof(PodContainerEntity.Name), 1},
        {nameof(PodContainerEntity.ContainerName), 2},
        {nameof(PodContainerEntity.Image), 3},
        {nameof(PodContainerEntity.ImagePullPolicy), 4},
        {nameof(PodContainerEntity.Age), 5},
    };

    public static readonly IDictionary<int, Func<PodContainerEntity, object?>> PodContainersIndexToMethodAccessMap =
        new Dictionary<int, Func<PodContainerEntity, object?>>
        {
            {0, f => f.Namespace},
            {1, f => f.Name},
            {2, f => f.ContainerName},
            {3, f => f.Image},
            {4, f => f.ImagePullPolicy},
            {5, f => f.Age},
        };
    
    public static readonly ISchemaColumn[] PodContainersColumns = {
        new SchemaColumn(nameof(PodContainerEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.ContainerName), 2, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Image), 3, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.ImagePullPolicy), 4, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Age), 5, typeof(DateTime?)),
    };
}