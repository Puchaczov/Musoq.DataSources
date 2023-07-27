using System.Collections.Concurrent;
using k8s.Models;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Secrets;

internal class SecretsSource : RowSourceBase<SecretEntity>
{
    private readonly IKubernetesApi _client;

    public SecretsSource(IKubernetesApi client)
    {
        _client = client;
    }
    
    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var secrets = _client.ListSecretsForAllNamespaces();

        chunkedSource.Add(
            secrets.Items.Select(c => new EntityResolver<SecretEntity>(MapV1SecretToSecretEntity(c), SecretsSourceHelper.SecretsNameToIndexMap, SecretsSourceHelper.SecretsIndexToMethodAccessMap)).ToList());
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