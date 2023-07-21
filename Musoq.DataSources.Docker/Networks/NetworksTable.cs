using Musoq.Schema;

namespace Musoq.DataSources.Docker.Networks;

public class NetworksTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => NetworksSourceHelper.NetworksColumns;
}