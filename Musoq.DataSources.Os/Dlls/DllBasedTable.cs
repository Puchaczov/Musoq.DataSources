using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Dlls;

internal class DllBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns => DllInfosHelper.DllInfosColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(DllInfo));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}