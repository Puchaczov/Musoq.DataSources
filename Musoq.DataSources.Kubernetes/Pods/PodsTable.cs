using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Pods;

internal class PodsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => PodsSourceHelper.PodsColumns;
}