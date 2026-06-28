using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.CANBus.Components;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.Signals;

internal class SignalsSource(ICANBusApi canBusApi, SourceExecutionContext executionContext)
    : AsyncRowsSourceBase<SignalEntity>(executionContext.EndWorkToken)
{
    protected override async Task CollectChunksAsync(
        IChunkWriter<SignalEntity> writer,
        CancellationToken cancellationToken)
    {
        var signals = await canBusApi.GetMessagesSignalsAsync(cancellationToken);
        var orderMap = new Dictionary<string, int>();
        var acceptedPredicate = executionContext.Plan.AcceptedPredicate;

        writer.Write(signals
            .Select((f, _) =>
            {
                if (!orderMap.TryAdd(f.Message.Name, 0))
                    orderMap[f.Message.Name]++;

                return new SignalEntity(f.Signal, f.Message, orderMap[f.Message.Name]);
            })
            .Where(entity => CANBusSourcePlanner.MatchesSignal(acceptedPredicate, entity))
            .ToList());
    }
}
