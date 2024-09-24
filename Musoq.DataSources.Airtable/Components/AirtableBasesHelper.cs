using Musoq.DataSources.Airtable.Sources.Bases;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable.Components;

internal static class AirtableBasesHelper
{
    public static readonly IReadOnlyDictionary<string, int> BasesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<AirtableBase, object>> BasesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] BasesColumns;

    static AirtableBasesHelper()
    {
        BasesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(AirtableBase.Id), 0},
            {nameof(AirtableBase.Name), 1},
            {nameof(AirtableBase.PermissionLevel), 2},
        };

        BasesIndexToMethodAccessMap = new Dictionary<int, Func<AirtableBase, object>>
        {
            {0, @base => @base.Id},
            {1, @base => @base.Name},
            {2, @base => @base.PermissionLevel},
        };

        BasesColumns =
        [
            new SchemaColumn(nameof(AirtableBase.Id), 0, typeof(string)),
            new SchemaColumn(nameof(AirtableBase.Name), 1, typeof(string)),
            new SchemaColumn(nameof(AirtableBase.PermissionLevel), 2, typeof(string))
        ];
    }
}