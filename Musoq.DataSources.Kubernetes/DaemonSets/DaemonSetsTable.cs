using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.DaemonSets;

internal class DaemonSetsTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => DaemonSetsSourceHelper.DaemonSetsColumns;
}