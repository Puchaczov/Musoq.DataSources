using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.ReplicaSets;

internal class ReplicaSetsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ReplicaSetEntity));

    public ISchemaColumn[] Columns => ReplicaSetsSourceHelper.ReplicaSetsColumns;
}