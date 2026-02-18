using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Deployments;

internal static class DeploymentsSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> DeploymentsNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<DeploymentEntity, object?>> DeploymentsIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] DeploymentsColumns;

    static DeploymentsSourceHelper()
    {
        DeploymentsNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(DeploymentEntity.Namespace), 0 },
            { nameof(DeploymentEntity.Name), 1 },
            { nameof(DeploymentEntity.CreationTimestamp), 2 },
            { nameof(DeploymentEntity.Generation), 3 },
            { nameof(DeploymentEntity.ResourceVersion), 4 },
            { nameof(DeploymentEntity.Images), 5 },
            { nameof(DeploymentEntity.ImagePullPolicies), 6 },
            { nameof(DeploymentEntity.RestartPolicy), 7 },
            { nameof(DeploymentEntity.ContainersNames), 8 },
            { nameof(DeploymentEntity.Status), 9 }
        };

        DeploymentsIndexToMethodAccessMap = new Dictionary<int, Func<DeploymentEntity, object?>>
        {
            { 0, info => info.Namespace },
            { 1, info => info.Name },
            { 2, info => info.CreationTimestamp },
            { 3, info => info.Generation },
            { 4, info => info.ResourceVersion },
            { 5, info => info.Images },
            { 6, info => info.ImagePullPolicies },
            { 7, info => info.RestartPolicy },
            { 8, info => info.ContainersNames },
            { 9, info => info.Status }
        };

        DeploymentsColumns =
        [
            new SchemaColumn(nameof(DeploymentEntity.Namespace), 0, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.CreationTimestamp), 2, typeof(DateTime)),
            new SchemaColumn(nameof(DeploymentEntity.Generation), 3, typeof(long)),
            new SchemaColumn(nameof(DeploymentEntity.ResourceVersion), 4, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Images), 5, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.ImagePullPolicies), 6, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.RestartPolicy), 7, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.ContainersNames), 8, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Status), 9, typeof(string))
        ];
    }
}