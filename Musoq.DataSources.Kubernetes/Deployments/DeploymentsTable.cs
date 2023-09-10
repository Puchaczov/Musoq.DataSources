using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Deployments;

internal class DeploymentsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => DeploymentsSourceHelper.DeploymentsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(DeploymentEntity));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}