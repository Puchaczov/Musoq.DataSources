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

namespace Musoq.DataSources.CANBus.Signals;

internal class SignalsSource : AsyncRowsSourceBase<Signal>
{
    private readonly ICANBusApi _canBusApi;
    private readonly RuntimeContext _runtimeContext;

    public SignalsSource(ICANBusApi canBusApi, RuntimeContext runtimeContext)
    {
        _canBusApi = canBusApi;
        _runtimeContext = runtimeContext;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var signals = await _canBusApi.GetMessagesSignalsAsync(_runtimeContext.EndWorkToken);
        
        chunkedSource.Add(
            signals.Select(f => new EntityResolver<SignalEntity>(new SignalEntity(f), SignalsSourceHelper.SignalsNameToIndexMap, SignalsSourceHelper.SignalsIndexToMethodAccessMap)).ToList());
    }
}