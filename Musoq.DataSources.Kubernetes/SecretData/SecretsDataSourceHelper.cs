using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.SecretData;

internal static class SecretsDataSourceHelper
{
    internal static readonly IDictionary<string, int> SecretsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(SecretDataEntity.Namespace), 0},
        {nameof(SecretDataEntity.Name), 1},
        {nameof(SecretDataEntity.Key), 2},
        {nameof(SecretDataEntity.Value), 3}
    };

    internal static readonly IDictionary<int, Func<SecretDataEntity, object?>> SecretsIndexToMethodAccessMap = new Dictionary<int, Func<SecretDataEntity, object?>>
    {
        {0, t => t.Namespace},
        {1, t => t.Name},
        {2, t => t.Key},
        {3, t => t.Value}
    };
    
    internal static readonly ISchemaColumn[] SecretsColumns = new ISchemaColumn[]
    {
        new SchemaColumn(nameof(SecretDataEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(SecretDataEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(SecretDataEntity.Key), 2, typeof(string)),
        new SchemaColumn(nameof(SecretDataEntity.Value), 3, typeof(byte[]))
    };
}