using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbcParserLib.Model;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.CANBus.Components;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Messages;

internal class MessagesSource(ICANBusApi canBusApi, RuntimeContext runtimeContext)
    : AsyncRowsSourceBase<Message>(runtimeContext.EndWorkToken)
{
    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var messages = await canBusApi.GetMessagesAsync(runtimeContext.EndWorkToken);

        chunkedSource.Add(
            messages.Select(f => new EntityResolver<MessageEntity>(
                new MessageEntity(f),
                MessagesSourceHelper.MessagesNameToIndexMap,
                MessagesSourceHelper.MessagesIndexToMethodAccessMap)
            ).ToList(),
            cancellationToken);
    }
}