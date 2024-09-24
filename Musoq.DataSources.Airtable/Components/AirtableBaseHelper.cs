using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable.Components;

internal static class AirtableBaseHelper
{
    public static readonly IReadOnlyDictionary<string, int> BasesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<AirtableTable, object>> BasesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] BaseColumns;

    static AirtableBaseHelper()
    {
        BasesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(AirtableTable.Id), 0},
            {nameof(AirtableTable.Name), 1},
            {nameof(AirtableTable.PrimaryFieldId), 2},
        };

        BasesIndexToMethodAccessMap = new Dictionary<int, Func<AirtableTable, object>>
        {
            {0, @base => @base.Id},
            {1, @base => @base.Name},
            {2, @base => @base.PrimaryFieldId},
        };

        BaseColumns =
        [
            new SchemaColumn(nameof(AirtableTable.Id), 0, typeof(string)),
            new SchemaColumn(nameof(AirtableTable.Name), 1, typeof(string)),
            new SchemaColumn(nameof(AirtableTable.PrimaryFieldId), 2, typeof(string))
        ];
    }
}