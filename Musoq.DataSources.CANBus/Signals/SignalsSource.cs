using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbcParserLib.Model;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.InferrableDataSourceHelpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Signals;

internal class SignalsSource : AsyncRowsSourceBase<Signal>
{
    private readonly ICANBusApi _canBusApi;

    public SignalsSource(ICANBusApi canBusApi, RuntimeContext runtimeContext)
        : base(runtimeContext.EndWorkToken)
    {
        _canBusApi = canBusApi;
    }

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        var signals = await _canBusApi.GetMessagesSignalsAsync(cancellationToken);
        
        chunkedSource.Add(
            signals.Select(f => new EntityResolver<SignalEntity>(
                new SignalEntity(f.Signal, f.MessageName), 
                SignalsSourceHelper.SignalsNameToIndexMap, 
                SignalsSourceHelper.SignalsIndexToMethodAccessMap)
            ).ToList(), 
        cancellationToken);
    }
}