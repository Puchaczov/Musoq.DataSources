using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.SecretData;

internal class SecretsDataTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => SecretsDataSourceHelper.SecretsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(SecretDataEntity));
}