using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Deployments;

internal class DeploymentsSource : RowSourceBase<DeploymentEntity>
{
    private const string DeploymentsSourceName = "kubernetes_deployments";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public DeploymentsSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(DeploymentsSourceName);

        try
        {
            var deployments = _client.ListDeploymentsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(DeploymentsSourceName, deployments.Items.Count);

            chunkedSource.Add(
                deployments.Items.Select(c =>
                    new EntityResolver<DeploymentEntity>(MapV1DeploymentToDeploymentEntity(c),
                        DeploymentsSourceHelper.DeploymentsNameToIndexMap,
                        DeploymentsSourceHelper.DeploymentsIndexToMethodAccessMap)).ToList());

            _runtimeContext.ReportDataSourceEnd(DeploymentsSourceName, deployments.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(DeploymentsSourceName, 0);
            throw;
        }
    }

    private static DeploymentEntity MapV1DeploymentToDeploymentEntity(V1Deployment v1Deployment)
    {
        return new DeploymentEntity(v1Deployment)
        {
            Name = v1Deployment.Metadata.Name,
            Namespace = v1Deployment.Metadata.NamespaceProperty,
            CreationTimestamp = v1Deployment.Metadata.CreationTimestamp,
            Generation = v1Deployment.Metadata.Generation,
            ResourceVersion = v1Deployment.Metadata.ResourceVersion,
            Images = string.Join(',', v1Deployment.Spec.Template.Spec.Containers.Select(f => f.Image)),
            ImagePullPolicies =
                string.Join(',', v1Deployment.Spec.Template.Spec.Containers.Select(f => f.ImagePullPolicy)),
            RestartPolicy = v1Deployment.Spec.Template.Spec.RestartPolicy,
            ContainersNames = string.Join(',', v1Deployment.Spec.Template.Spec.Containers.Select(f => f.Name)),
            Status = v1Deployment.Status.Conditions != null
                ? v1Deployment.Status.Conditions.Select(f => f.Status).ElementAt(0)
                : string.Empty
        };
    }
}