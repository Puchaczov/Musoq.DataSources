using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Ingresses;

internal class IngressesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => IngressesSourceHelper.IngressesColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(IngressEntity));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}