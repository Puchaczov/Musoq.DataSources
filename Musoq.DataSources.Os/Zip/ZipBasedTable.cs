using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Zip;

internal class ZipBasedTable : ISchemaTable
{
    public ZipBasedTable()
    {
        Columns = SchemaZipHelper.SchemaColumns;
    }

    public ISchemaColumn[] Columns { get; }

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
}