using System.IO;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Directories;

internal class DirectoriesBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = SchemaDirectoriesHelper.DirectoriesColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(DirectoryInfo));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}