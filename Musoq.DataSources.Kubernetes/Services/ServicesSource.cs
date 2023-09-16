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
            Metadata = v1Service.Metadata,
            Spec = v1Service.Spec,
            Kind = v1Service.Kind,
            Status = v1Service.Status
        };
    }
}