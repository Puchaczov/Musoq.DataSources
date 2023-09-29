using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbcParserLib.Model;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.CANBus.Helpers;
using Musoq.DataSources.InferrableDataSourceHelpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus;

internal class MessagesSource : AsyncRowsSourceBase<Message>
{
    private readonly ICANBusApi _canBusApi;
    private readonly RuntimeContext _runtimeContext;

    public MessagesSource(ICANBusApi canBusApi, RuntimeContext runtimeContext)
    {
        _canBusApi = canBusApi;
        _runtimeContext = runtimeContext;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var messages = await _canBusApi.GetMessagesAsync(_runtimeContext.EndWorkToken);
        
        chunkedSource.Add(
            messages.Select(f => new EntityResolver<MessageEntity>(new MessageEntity(f), MessagesSourceHelper.MessagesNameToIndexMap, MessagesSourceHelper.MessagesIndexToMethodAccessMap)).ToList());
    }
}