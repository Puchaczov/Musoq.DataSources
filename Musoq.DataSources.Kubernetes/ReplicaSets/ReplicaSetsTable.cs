using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.ReplicaSets;

internal class ReplicaSetsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => ReplicaSetsSourceHelper.ReplicaSetsColumns;
}