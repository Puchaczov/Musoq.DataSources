using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.CANBus.Components;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.Messages;

internal class MessagesSource(ICANBusApi canBusApi, SourceExecutionContext executionContext)
    : AsyncRowsSourceBase<MessageEntity>(executionContext.EndWorkToken)
{
    protected override async Task CollectChunksAsync(
        IChunkWriter<MessageEntity> writer,
        CancellationToken cancellationToken)
    {
        var messages = await canBusApi.GetMessagesAsync(cancellationToken);
        var acceptedPredicate = executionContext.Plan.AcceptedPredicate;

        writer.Write(messages
            .Select(f => new MessageEntity(f))
            .Where(entity => CANBusSourcePlanner.MatchesMessage(acceptedPredicate, entity))
            .ToList());
    }
}
