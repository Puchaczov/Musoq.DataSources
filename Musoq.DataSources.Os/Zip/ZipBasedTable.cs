using System.IO.Compression;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Zip;

internal class ZipBasedTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SchemaZipHelper.SchemaColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ZipArchiveEntry));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}