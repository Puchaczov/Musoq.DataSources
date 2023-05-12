using Musoq.DataSources.Airtable.Components;
using Musoq.Schema;

namespace Musoq.DataSources.Airtable.Sources.Bases;

internal class AirtableBasesSchemaTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.First(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => AirtableBasesHelper.BasesColumns;
}