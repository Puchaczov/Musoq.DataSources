using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Ingresses;

internal class IngressesSource : RowSourceBase<IngressEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public IngressesSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var ingresses = _kubernetesApi.ListIngressesForAllNamespaces();

        chunkedSource.Add(
            ingresses.Items.Select(c => new EntityResolver<IngressEntity>(MapV1IngressToIngressEntity(c), IngressesSourceHelper.IngressesNameToIndexMap, IngressesSourceHelper.IngressesIndexToMethodAccessMap)).ToList());
    }

    private static IngressEntity MapV1IngressToIngressEntity(V1Ingress v1Ingress)
    {
        return new IngressEntity
        {
            Name = v1Ingress.Metadata.Name,
            Namespace = v1Ingress.Metadata.NamespaceProperty,
            Class = v1Ingress.Spec.IngressClassName,
            Hosts = string.Join(",", v1Ingress.Spec.Rules.Select(c => c.Host)),
            Address = string.Join(",", v1Ingress.Status.LoadBalancer.Ingress.Select(c => c.Hostname ?? c.Ip)),
            Ports = string.Join(",", v1Ingress.Spec.Tls.SelectMany(c => c.Hosts)),
            Age = v1Ingress.Metadata.CreationTimestamp
        };
    }
}