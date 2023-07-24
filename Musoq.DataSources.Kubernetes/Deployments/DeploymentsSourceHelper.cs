using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Deployments;

internal static class DeploymentsSourceHelper
{
    public static readonly IDictionary<string, int> DeploymentsNameToIndexMap;
    public static readonly IDictionary<int, Func<DeploymentEntity, object?>> DeploymentsIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] DeploymentsColumns;

    static DeploymentsSourceHelper()
    {
        DeploymentsNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(DeploymentEntity.Namespace), 0},
            {nameof(DeploymentEntity.Name), 1},
            {nameof(DeploymentEntity.CreationTimestamp), 2},
            {nameof(DeploymentEntity.Generation), 3},
            {nameof(DeploymentEntity.ResourceVersion), 4},
            {nameof(DeploymentEntity.Image), 5},
            {nameof(DeploymentEntity.ImagePullPolicy), 6},
            {nameof(DeploymentEntity.RestartPolicy), 7},
            {nameof(DeploymentEntity.Type), 8},
            {nameof(DeploymentEntity.Status), 9},
            {nameof(DeploymentEntity.ClusterIP), 10},
            {nameof(DeploymentEntity.ExternalIP), 11},
            {nameof(DeploymentEntity.Ports), 12}
        };
        
        DeploymentsIndexToMethodAccessMap = new Dictionary<int, Func<DeploymentEntity, object?>>
        {
            {0, info => info.Namespace},
            {1, info => info.Name},
            {2, info => info.CreationTimestamp},
            {3, info => info.Generation},
            {4, info => info.ResourceVersion},
            {5, info => info.Image},
            {6, info => info.ImagePullPolicy},
            {7, info => info.RestartPolicy},
            {8, info => info.Type},
            {9, info => info.Status},
            {10, info => info.ClusterIP},
            {11, info => info.ExternalIP},
            {12, info => info.Ports}
        };
        
        DeploymentsColumns = new ISchemaColumn[]
        {
            new SchemaColumn(nameof(DeploymentEntity.Namespace), 0, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Name), 1, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.CreationTimestamp), 2, typeof(DateTime)),
            new SchemaColumn(nameof(DeploymentEntity.Generation), 3, typeof(long)),
            new SchemaColumn(nameof(DeploymentEntity.ResourceVersion), 4, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Image), 5, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.ImagePullPolicy), 6, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.RestartPolicy), 7, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Type), 8, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Status), 9, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.ClusterIP), 10, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.ExternalIP), 11, typeof(string)),
            new SchemaColumn(nameof(DeploymentEntity.Ports), 12, typeof(string))
        };
    }
}