using System.Collections.Concurrent;
using Musoq.DataSources.Airtable.Components;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable.Sources.Base;

internal class AirtableBaseRowSource : RowSourceBase<AirtableTable>
{
    private readonly IAirtableApi _airtableApi;
    private readonly RuntimeContext _runtimeContext;

    public AirtableBaseRowSource(IAirtableApi airtableApi, RuntimeContext runtimeContext)
    {
        _airtableApi = airtableApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        foreach (var tables in _airtableApi.GetTables(_runtimeContext.QueryInformation.Columns.Select(f => f.ColumnName)))
        {
            var chunk = tables
                .Select(@base => new EntityResolver<AirtableTable>(@base, AirtableBaseHelper.BasesNameToIndexMap, AirtableBaseHelper.BasesIndexToMethodAccessMap))
                .ToList();
            
            chunkedSource.Add(chunk);
        }
    }
}