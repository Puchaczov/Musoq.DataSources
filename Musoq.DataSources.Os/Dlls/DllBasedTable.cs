using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Dlls;

internal class DllBasedTable : ISchemaTable
{
    public DllBasedTable()
    {
        Columns = DllInfosHelper.DllInfosColumns;
    }

    public ISchemaColumn[] Columns { get; }

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(DllInfo));
}