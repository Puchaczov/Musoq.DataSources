using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Deployments;

internal class DeploymentsSource : RowSourceBase<DeploymentEntity>
{
    private readonly IKubernetesApi _client;

    public DeploymentsSource(IKubernetesApi client)
    {
        _client = client;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var deployments = _client.ListDeploymentsForAllNamespaces();
        
        chunkedSource.Add(
            deployments.Items.Select(c => 
                new EntityResolver<DeploymentEntity>(MapV1DeploymentToDeploymentEntity(c), DeploymentsSourceHelper.DeploymentsNameToIndexMap, DeploymentsSourceHelper.DeploymentsIndexToMethodAccessMap)).ToList());
    }

    private static DeploymentEntity MapV1DeploymentToDeploymentEntity(V1Deployment v1Deployment)
    {
        return new DeploymentEntity
        {
            Name = v1Deployment.Metadata.Name,
            Namespace = v1Deployment.Metadata.NamespaceProperty,
            CreationTimestamp = v1Deployment.Metadata.CreationTimestamp,
            Generation = v1Deployment.Metadata.Generation,
            ResourceVersion = v1Deployment.Metadata.ResourceVersion,
            Image = v1Deployment.Spec.Template.Spec.Containers[0].Image,
            ImagePullPolicy = v1Deployment.Spec.Template.Spec.Containers[0].ImagePullPolicy,
            RestartPolicy = v1Deployment.Spec.Template.Spec.RestartPolicy,
            Type = v1Deployment.Spec.Template.Spec.Containers[0].Name,
            Status = v1Deployment.Status.Conditions[0].Status
        };
    }
}