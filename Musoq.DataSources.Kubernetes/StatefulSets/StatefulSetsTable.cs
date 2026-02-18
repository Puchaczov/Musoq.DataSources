using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.StatefulSets;

internal class StatefulSetsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => StatefulSetsSourceHelper.StatefulSetsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(StatefulSetEntity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}