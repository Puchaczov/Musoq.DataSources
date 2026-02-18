using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Nodes;

internal class NodesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => NodesSourceHelper.NodesColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(NodeEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}