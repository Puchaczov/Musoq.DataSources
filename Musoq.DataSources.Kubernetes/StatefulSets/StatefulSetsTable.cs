using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.StatefulSets;

internal class StatefulSetsTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => StatefulSetsSourceHelper.StatefulSetsColumns;
}