using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Metadata;

internal class MetadataTable : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = SchemaMetadataHelper.MetadataColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(MetadataEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}