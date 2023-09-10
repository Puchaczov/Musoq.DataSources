using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.PodContainers;

internal class PodContainersTable : ISchemaTable
{
    public ISchemaColumn[] Columns => PodContainersSourceHelper.PodContainersColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(PodContainerEntity));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}