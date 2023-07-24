using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Ingresses;

public class IngressesTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => IngressesSourceHelper.IngressesColumns;
}