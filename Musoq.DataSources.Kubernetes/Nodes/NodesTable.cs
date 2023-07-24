using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Nodes;

internal class NodesTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => NodesSourceHelper.NodesColumns;
}