using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Services;

internal class ServicesSource : RowSourceBase<ServiceEntity>
{
    private readonly IKubernetesApi _client;

    public ServicesSource(IKubernetesApi client)
    {
        _client = client;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var services = _client.ListServicesForAllNamespaces();

        chunkedSource.Add(
            services.Items.Select(c => 
                new EntityResolver<ServiceEntity>(MapV1ServiceToServiceEntity(c), ServicesSourceHelper.ServicesNameToIndexMap, ServicesSourceHelper.ServicesIndexToMethodAccessMap)).ToList());
    }

    private static ServiceEntity MapV1ServiceToServiceEntity(V1Service v1Service)
    {
        return new ServiceEntity
        {
            Name = v1Service.Metadata.Name,
            Namespace = v1Service.Metadata.NamespaceProperty,
            Type = v1Service.Spec.Type,
            ClusterIP = v1Service.Spec.ClusterIP,
            ExternalIPs = v1Service.Spec.ExternalIPs != null ? string.Join(",", v1Service.Spec.ExternalIPs) : string.Empty,
            Ports = v1Service.Spec.Ports != null ? string.Join(",", v1Service.Spec.Ports.Select(c => c.Port)) : string.Empty
        };
    }
}