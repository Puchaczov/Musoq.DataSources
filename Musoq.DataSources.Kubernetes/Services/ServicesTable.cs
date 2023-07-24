using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Services;

internal class ServicesTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => ServicesSourceHelper.ServicesColumns;
}