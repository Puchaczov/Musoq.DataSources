using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.PodContainers;

internal class PodContainersTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => PodContainersSourceHelper.PodContainersColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(PodContainerEntity));
}