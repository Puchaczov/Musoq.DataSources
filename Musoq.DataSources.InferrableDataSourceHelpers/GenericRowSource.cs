using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public abstract class GenericRowSource<TType> : AsyncRowsSourceBase<TType>
{
    protected GenericRowSource(CancellationToken endWorkToken) 
        : base(endWorkToken)
    {
    }
    
    protected abstract IAsyncEnumerable<TType> GetDataAsync(CancellationToken cancellationToken);
    
    protected abstract IObjectResolver CreateResolver(TType item);
    
    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        const int chunkSize = 1000;
        var chunk = new List<IObjectResolver>();
        
        await foreach (var item in GetDataAsync(cancellationToken))
        {
            chunk.Add(CreateResolver(item));
            
            if (chunk.Count < chunkSize)
                continue;
            
            chunkedSource.Add(chunk, cancellationToken);
            chunk = new List<IObjectResolver>();
        }
    }
}