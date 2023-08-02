using System.IO;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Directories;

internal class DirectoriesBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = SchemaDirectoriesHelper.DirectoriesColumns;

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(DirectoryInfo));
}