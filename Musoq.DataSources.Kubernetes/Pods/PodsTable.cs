using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Pods;

internal class PodsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => PodsSourceHelper.PodsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(PodEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}