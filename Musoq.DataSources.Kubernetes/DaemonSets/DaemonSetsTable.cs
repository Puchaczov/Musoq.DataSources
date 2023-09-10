using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.DaemonSets;

internal class DaemonSetsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => DaemonSetsSourceHelper.DaemonSetsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(DaemonSetEntity));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}