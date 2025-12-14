using System.Collections.Concurrent;
using Musoq.DataSources.Airtable.Components;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable.Sources.Bases;

internal class AirtableBasesRowSource : RowSourceBase<AirtableBase>
{
    private const string AirtableBasesSourceName = "airtable_bases";
    private readonly IAirtableApi _airtableApi;
    private readonly RuntimeContext _runtimeContext;

    public AirtableBasesRowSource(IAirtableApi airtableApi, RuntimeContext runtimeContext)
    {
        _airtableApi = airtableApi;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(AirtableBasesSourceName);
        long totalRowsProcessed = 0;
        
        try
        {
            foreach (var bases in _airtableApi.GetBases(_runtimeContext.QueryInformation.Columns.Select(f => f.ColumnName)))
            {
                var chunk = bases
                    .Select(@base => new EntityResolver<AirtableBase>(@base, AirtableBasesHelper.BasesNameToIndexMap, AirtableBasesHelper.BasesIndexToMethodAccessMap))
                    .ToList();
            
                totalRowsProcessed += chunk.Count;
                chunkedSource.Add(chunk);
            }
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(AirtableBasesSourceName, totalRowsProcessed);
        }
    }
}