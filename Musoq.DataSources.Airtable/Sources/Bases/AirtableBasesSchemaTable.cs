using Musoq.DataSources.Airtable.Components;
using Musoq.Schema;

namespace Musoq.DataSources.Airtable.Sources.Bases;

internal class AirtableBasesSchemaTable : ISchemaTable
{
    public ISchemaColumn[] Columns => AirtableBasesHelper.BasesColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(AirtableBase));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.First(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}