using System.Collections.Concurrent;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable.Sources.Table;

internal class AirtableTableRowSource : RowSourceBase<dynamic>
{
    private readonly IAirtableApi _api;
    private readonly RuntimeContext _runtimeContext;

    public AirtableTableRowSource(IAirtableApi api, RuntimeContext runtimeContext)
    {
        _api = api;
        _runtimeContext = runtimeContext;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var columns = _runtimeContext.QueryInformation.Columns.Select(f => f.ColumnName).ToArray();
        var columnsHashSet = new HashSet<string>(columns.Distinct());
        
        using var enumeratorChunks = _api.GetRecordsChunks(columns).GetEnumerator();
        
        if (!enumeratorChunks.MoveNext())
            return;

        if (enumeratorChunks.Current is not { } firstChunk)
            return;
        
        using var firstChunkEnumerator = firstChunk.GetEnumerator();
        
        if (!firstChunkEnumerator.MoveNext())
            return;
        
        var index = 0;
        var firstRow = firstChunkEnumerator.Current;
        var indexToNameMap = columns.ToDictionary(_ => index++, column => column);
        
        var evaluatorChunk = new List<IObjectResolver> {new AirtableObjectResolver(firstRow.Fields, indexToNameMap, columnsHashSet)};
        
        while (firstChunkEnumerator.MoveNext())
        {
            var current = firstChunkEnumerator.Current;
            var row = current.Fields;

            row.Add(nameof(current.Id), current.Id);
            
            evaluatorChunk.Add(new AirtableObjectResolver(row, indexToNameMap, columnsHashSet));
        }

        chunkedSource.Add(evaluatorChunk);
        
        while (enumeratorChunks.MoveNext())
        {
            var currentChunk = enumeratorChunks.Current;
            evaluatorChunk = [];
            
            foreach (var record in currentChunk)
            {
                var row = record.Fields.ToDictionary(field => field.Key, field => field.Value);
                
                row.Add(nameof(record.Id), record.Id);
                
                evaluatorChunk.Add(new AirtableObjectResolver(row, indexToNameMap, columnsHashSet));
            }
            
            chunkedSource.Add(evaluatorChunk);
        }
    }
}