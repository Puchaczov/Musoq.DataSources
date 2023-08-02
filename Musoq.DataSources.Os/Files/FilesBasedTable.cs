using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Files;

internal class FilesBasedTable : ISchemaTable
{
    public FilesBasedTable()
    {
        Columns = SchemaFilesHelper.FilesColumns;
    }

    public ISchemaColumn[] Columns { get; }

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ExtendedFileInfo));
}