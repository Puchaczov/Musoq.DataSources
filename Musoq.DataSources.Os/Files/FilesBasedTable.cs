using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Files;

internal class FilesBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SchemaFilesHelper.FilesColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ExtendedFileInfo));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}