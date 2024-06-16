using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Secrets;

internal static class SecretsSourceHelper
{
    internal static readonly IDictionary<string, int> SecretsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(SecretEntity.Namespace), 0},
        {nameof(SecretEntity.Name), 1},
        {nameof(SecretEntity.Type), 2},
        {nameof(SecretEntity.Immutable), 3},
        {nameof(SecretEntity.Age), 4}
    };

    internal static readonly IDictionary<int, Func<SecretEntity, object?>> SecretsIndexToMethodAccessMap = new Dictionary<int, Func<SecretEntity, object?>>
    {
        {0, t => t.Namespace},
        {1, t => t.Name},
        {2, t => t.Type},
        {3, t => t.Immutable},
        {4, t => t.Age}
    };
    
    internal static readonly ISchemaColumn[] SecretsColumns =
    [
        new SchemaColumn(nameof(SecretEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(SecretEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(SecretEntity.Type), 2, typeof(string)),
        new SchemaColumn(nameof(SecretEntity.Immutable), 3, typeof(bool?)),
        new SchemaColumn(nameof(SecretEntity.Age), 3, typeof(DateTime?))
    ];
}