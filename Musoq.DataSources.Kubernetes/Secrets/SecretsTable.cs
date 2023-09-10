using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Secrets;

internal class SecretsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SecretsSourceHelper.SecretsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(SecretEntity));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}