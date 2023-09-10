using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public abstract class GenericRowSource<TType> : AsyncRowsSourceBase<TType>
{
    protected abstract IAsyncEnumerable<TType> GetDataAsync();
    
    protected abstract IObjectResolver CreateResolver(TType item);
    
    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        const int chunkSize = 1000;
        var chunk = new List<IObjectResolver>();
        
        await foreach (var item in GetDataAsync())
        {
            chunk.Add(CreateResolver(item));
            
            if (chunk.Count < chunkSize)
                continue;
            
            chunkedSource.Add(chunk);
            chunk = new List<IObjectResolver>();
        }
    }
}