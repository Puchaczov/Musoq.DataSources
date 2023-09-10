using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Process;

internal class ProcessBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns => ProcessHelper.ProcessColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(System.Diagnostics.Process));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}