using Musoq.DataSources.Airtable.Components;
using Musoq.Schema;

namespace Musoq.DataSources.Airtable.Sources.Base;

internal class AirtableBaseSchemaTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.First(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => AirtableBaseHelper.BaseColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(AirtableTable));
}