using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Musoq.DataSources.InferrableDataSourceHelpers;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Components;

internal abstract class MessageFrameSourceBase : AsyncRowsSourceBase<MessageFrameEntity>
{
    protected abstract Task InitializeAsync();
    
    protected abstract IAsyncEnumerable<SourceCanFrame> GetFramesAsync();
    
    protected abstract HashSet<string> AllMessagesSet { get; }
    
    protected abstract IReadOnlyDictionary<string, int> MessagesNameToIndexMap { get; }
    
    protected abstract IReadOnlyDictionary<int, Func<MessageFrameEntity, object?>> MessagesIndexToMethodAccessMap { get; }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        await InitializeAsync();
        
        var itemsAdded = 0;
        const int maxItems = 1000;
        var chunk = new List<IObjectResolver>();

        await foreach (var frame in GetFramesAsync())
        {
            var messageFrame = new MessageFrameEntity(
                frame.Timestamp, 
                frame.Frame, 
                frame.Message,
                AllMessagesSet);

            var nameToIndexMap = messageFrame.CreateMessageNameToIndexMap();
            var nameToIndexMapFinal = new Dictionary<string, int>(nameToIndexMap);
            var addedKeysIndexes = new List<(string Key, int Index)>();
            foreach (var keyValuePair in MessagesNameToIndexMap)
            {
                var count = nameToIndexMap.Count;
                if (nameToIndexMapFinal.TryAdd(keyValuePair.Key, count))
                {
                    addedKeysIndexes.Add((keyValuePair.Key, count));
                }
            }
                
            var indexToMethodAccessMap = messageFrame.CreateMessageIndexToMethodAccessMap();
            var indexToMethodAccessMapFinal = new Dictionary<int, Func<MessageFrameEntity, object?>>(indexToMethodAccessMap);
            
            foreach (var grouping in addedKeysIndexes.GroupBy(f => f.Index))
                indexToMethodAccessMapFinal.Add(grouping.Key, _ => null);
            
            if (itemsAdded != maxItems)
            {
                chunk.Add(new EntityResolver<MessageFrameEntity>(messageFrame, nameToIndexMapFinal, indexToMethodAccessMapFinal));
                itemsAdded += 1;
                continue;
            }
            
            chunk.Add(new EntityResolver<MessageFrameEntity>(messageFrame, nameToIndexMapFinal, indexToMethodAccessMapFinal));
            chunkedSource.Add(chunk);
            chunk = new List<IObjectResolver>();
            itemsAdded = 0;
        }
        
        if (chunk.Count > 0)
            chunkedSource.Add(chunk);
    }
}