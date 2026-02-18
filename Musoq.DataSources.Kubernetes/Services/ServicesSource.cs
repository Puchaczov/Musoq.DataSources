using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Services;

internal class ServicesSource : RowSourceBase<ServiceEntity>
{
    private const string ServicesSourceName = "kubernetes_services";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public ServicesSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(ServicesSourceName);

        try
        {
            var services = _client.ListServicesForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(ServicesSourceName, services.Items.Count);

            chunkedSource.Add(
                services.Items.Select(c =>
                    new EntityResolver<ServiceEntity>(MapV1ServiceToServiceEntity(c),
                        ServicesSourceHelper.ServicesNameToIndexMap,
                        ServicesSourceHelper.ServicesIndexToMethodAccessMap)).ToList());

            _runtimeContext.ReportDataSourceEnd(ServicesSourceName, services.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(ServicesSourceName, 0);
            throw;
        }
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