using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.SecretData;

internal class SecretsDataSource : RowSourceBase<SecretDataEntity>
{
    private readonly IKubernetesApi _kubernetesApi;

    public SecretsDataSource(IKubernetesApi kubernetesApi)
    {
        _kubernetesApi = kubernetesApi;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var secrets = _kubernetesApi.ListSecretsForAllNamespaces().Items.SelectMany(c => c.Data?.Select(d => new InternalSecretDataEntity
        {
            Key = d.Key,
            Value = d.Value,
            Name = c.Metadata.Name,
            Namespace = c.Metadata.NamespaceProperty,
        }) ?? Array.Empty<InternalSecretDataEntity>()).ToArray();

        chunkedSource.Add(
            secrets.Select(c => new EntityResolver<SecretDataEntity>(MapInternalSecretDataEntityToSecretDataEntity(c), SecretsDataSourceHelper.SecretsNameToIndexMap, SecretsDataSourceHelper.SecretsIndexToMethodAccessMap)).ToList());
    }
    
    private static SecretDataEntity MapInternalSecretDataEntityToSecretDataEntity(InternalSecretDataEntity v1Secret)
    {
        return new SecretDataEntity
        {
            Name = v1Secret.Name,
            Namespace = v1Secret.Namespace,
            Key = v1Secret.Key,
            Value = v1Secret.Value
        };
    }
    
    private class InternalSecretDataEntity
    {
        public string Namespace { get; init; }
        public string Name { get; init; }
        public string Key { get; init; }
        public byte[] Value { get; init; }
    }
}