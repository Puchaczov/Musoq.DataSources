using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbcParserLib.Model;
using Musoq.DataSources.CANBus.Components;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Signals;

internal class SignalsSource(ICANBusApi canBusApi, RuntimeContext runtimeContext)
    : AsyncRowsSourceBase<Signal>(runtimeContext.EndWorkToken)
{
    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        var signals = await canBusApi.GetMessagesSignalsAsync(cancellationToken);
        
        chunkedSource.Add(
            signals.Select(f => new EntityResolver<SignalEntity>(
                new SignalEntity(f.Signal, f.Message), 
                SignalsSourceHelper.SignalsNameToIndexMap, 
                SignalsSourceHelper.SignalsIndexToMethodAccessMap)
            ).ToList(), 
        cancellationToken);
    }
}