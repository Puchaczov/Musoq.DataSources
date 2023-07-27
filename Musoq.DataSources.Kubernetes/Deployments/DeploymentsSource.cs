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
            Images = string.Join(',', v1Deployment.Spec.Template.Spec.Containers.Select(f => f.Image)),
            ImagePullPolicies = string.Join(',', v1Deployment.Spec.Template.Spec.Containers.Select(f => f.ImagePullPolicy)),
            RestartPolicy = v1Deployment.Spec.Template.Spec.RestartPolicy,
            ContainersNames = string.Join(',', v1Deployment.Spec.Template.Spec.Containers.Select(f => f.Name)),
            Status = v1Deployment.Status.Conditions != null ? v1Deployment.Status.Conditions.Select(f => f.Status).ElementAt(0) : string.Empty
        };
    }
}