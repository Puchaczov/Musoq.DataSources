using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.Components;

internal abstract class MessageFrameSourceBase(SourceExecutionContext executionContext)
    : AsyncRowsSourceBase<MessageFrameEntity>(executionContext.EndWorkToken)
{
    private readonly IReadOnlySet<string>? _requestedColumns = GetRequestedColumns(executionContext);
    private readonly SourcePredicateExpression? _acceptedPredicate = executionContext.Plan.AcceptedPredicate;

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
            if (!CANBusSourcePlanner.MatchesFrame(_acceptedPredicate, frame))
                continue;

            chunk.Add(new MessageFrameEntity(
                frame.Timestamp,
                frame.Frame,
                frame.Message,
                AllMessagesSet,
                _requestedColumns));

            if (chunk.Count < RowChunking.DefaultChunkSize)
                continue;

            writer.Write(chunk);
            chunk = [];
        }

        if (chunk.Count > 0)
            writer.Write(chunk);
    }

    private static IReadOnlySet<string>? GetRequestedColumns(SourceExecutionContext executionContext)
    {
        var acceptedColumns = executionContext.Plan.AcceptedColumns;

        if (acceptedColumns.Count == 0)
            return null;

        var requestedColumns = new HashSet<string>(StringComparer.Ordinal);

        foreach (var acceptedColumn in acceptedColumns)
        {
            requestedColumns.Add(acceptedColumn.Name);

            foreach (var part in acceptedColumn.Name.Split('.'))
                requestedColumns.Add(part);
        }

        return requestedColumns;
    }
}
