using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Secrets;

internal class SecretsSource : RowSourceBase<SecretEntity>
{
    private const string SecretsSourceName = "kubernetes_secrets";
    private readonly IKubernetesApi _client;
    private readonly RuntimeContext _runtimeContext;

    public SecretsSource(IKubernetesApi client, RuntimeContext runtimeContext)
    {
        _client = client;
        _runtimeContext = runtimeContext;
    }
    
    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(SecretsSourceName);
        
        try
        {
            var secrets = _client.ListSecretsForAllNamespaces();
            _runtimeContext.ReportDataSourceRowsKnown(SecretsSourceName, secrets.Items.Count);

            chunkedSource.Add(
                secrets.Items.Select(c => new EntityResolver<SecretEntity>(MapV1SecretToSecretEntity(c), SecretsSourceHelper.SecretsNameToIndexMap, SecretsSourceHelper.SecretsIndexToMethodAccessMap)).ToList());
            
            _runtimeContext.ReportDataSourceEnd(SecretsSourceName, secrets.Items.Count);
        }
        catch
        {
            _runtimeContext.ReportDataSourceEnd(SecretsSourceName, 0);
            throw;
        }
    }

    private static SecretEntity MapV1SecretToSecretEntity(V1Secret v1Secret)
    {
        return new SecretEntity
        {
            Name = v1Secret.Metadata.Name,
            Namespace = v1Secret.Metadata.NamespaceProperty,
            Type = v1Secret.Type,
            Immutable = v1Secret.Immutable,
            Age = v1Secret.Metadata.CreationTimestamp,
        };
    }
}