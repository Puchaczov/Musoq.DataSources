using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.PodContainers;

internal static class PodContainersSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> PodContainersNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(PodContainerEntity.Namespace), 0 },
        { nameof(PodContainerEntity.Name), 1 },
        { nameof(PodContainerEntity.ContainerName), 2 },
        { nameof(PodContainerEntity.Image), 3 },
        { nameof(PodContainerEntity.ImagePullPolicy), 4 },
        { nameof(PodContainerEntity.Age), 5 },
        { nameof(PodContainerEntity.SecurityContext), 6 },
        { nameof(PodContainerEntity.Stdin), 7 },
        { nameof(PodContainerEntity.StdinOnce), 8 },
        { nameof(PodContainerEntity.TerminationMessagePath), 9 },
        { nameof(PodContainerEntity.TerminationMessagePolicy), 10 },
        { nameof(PodContainerEntity.Tty), 11 },
        { nameof(PodContainerEntity.WorkingDir), 12 }
    };

    public static readonly IReadOnlyDictionary<int, Func<PodContainerEntity, object?>>
        PodContainersIndexToMethodAccessMap =
            new Dictionary<int, Func<PodContainerEntity, object?>>
            {
                { 0, f => f.Namespace },
                { 1, f => f.Name },
                { 2, f => f.ContainerName },
                { 3, f => f.Image },
                { 4, f => f.ImagePullPolicy },
                { 5, f => f.Age },
                { 6, f => f.SecurityContext },
                { 7, f => f.Stdin },
                { 8, f => f.StdinOnce },
                { 9, f => f.TerminationMessagePath },
                { 10, f => f.TerminationMessagePolicy },
                { 11, f => f.Tty },
                { 12, f => f.WorkingDir }
            };

    public static readonly ISchemaColumn[] PodContainersColumns =
    [
        new SchemaColumn(nameof(PodContainerEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.ContainerName), 2, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Image), 3, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.ImagePullPolicy), 4, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Age), 5, typeof(DateTime?)),
        new SchemaColumn(nameof(PodContainerEntity.SecurityContext), 6, typeof(V1SecurityContext)),
        new SchemaColumn(nameof(PodContainerEntity.Stdin), 7, typeof(bool?)),
        new SchemaColumn(nameof(PodContainerEntity.StdinOnce), 8, typeof(bool?)),
        new SchemaColumn(nameof(PodContainerEntity.TerminationMessagePath), 9, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.TerminationMessagePolicy), 10, typeof(string)),
        new SchemaColumn(nameof(PodContainerEntity.Tty), 11, typeof(bool?)),
        new SchemaColumn(nameof(PodContainerEntity.WorkingDir), 12, typeof(string))
    ];
}