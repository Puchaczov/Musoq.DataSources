using Musoq.DataSources.Airtable.Components;
using Musoq.Schema;

namespace Musoq.DataSources.Airtable.Sources.Base;

internal class AirtableBaseSchemaTable : ISchemaTable
{
    public ISchemaColumn[] Columns => AirtableBaseHelper.BaseColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(AirtableTable));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.First(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}