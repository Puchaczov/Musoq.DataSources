using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Secrets;

internal class SecretsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => SecretsSourceHelper.SecretsColumns;
}