using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Deployments;

internal class DeploymentsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => DeploymentsSourceHelper.DeploymentsColumns;
}