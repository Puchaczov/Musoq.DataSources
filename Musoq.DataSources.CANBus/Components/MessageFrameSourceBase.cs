using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.Components;

internal abstract class MessageFrameSourceBase(CancellationToken endWorkToken) : AsyncRowsSourceBase<MessageFrameEntity>(endWorkToken)
{
    protected abstract HashSet<string> AllMessagesSet { get; }

    protected abstract Task InitializeAsync(CancellationToken cancellationToken);

    protected abstract IAsyncEnumerable<SourceCanFrame> GetFramesAsync(CancellationToken cancellationToken);

    protected override async Task CollectChunksAsync(
        IChunkWriter<MessageFrameEntity> writer,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var chunk = new List<MessageFrameEntity>();

        await foreach (var frame in GetFramesAsync(cancellationToken))
        {
            chunk.Add(new MessageFrameEntity(
                frame.Timestamp,
                frame.Frame,
                frame.Message,
                AllMessagesSet));

            if (chunk.Count < RowChunking.DefaultChunkSize)
                continue;

            writer.Write(chunk);
            chunk = [];
        }

        if (chunk.Count > 0)
            writer.Write(chunk);
    }
}
