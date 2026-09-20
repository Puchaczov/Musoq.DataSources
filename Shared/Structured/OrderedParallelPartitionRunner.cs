#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Structured;

internal static class OrderedParallelPartitionRunner
{
    private const int BufferedChunksPerPartition = 2;

    public static long Run<T>(
        ImmutableArray<StructuredPartition> partitions,
        int maximumParallelism,
        IChunkWriter<T> destination,
        Action<StructuredPartition, IChunkWriter<T>, CancellationToken> processPartition,
        Action<long>? reportRowsRead,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumParallelism);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(processPartition);

        if (partitions.IsDefaultOrEmpty)
            return 0;

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channels = CreateChannels<T>(partitions.Length);
        var workerCount = Math.Min(maximumParallelism, partitions.Length);
        var workers = new Task[workerCount];
        ExceptionDispatchInfo? firstFailure = null;
        var nextPartition = -1;

        for (var workerIndex = 0; workerIndex < workers.Length; workerIndex++)
        {
            workers[workerIndex] = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    var partitionIndex = Interlocked.Increment(ref nextPartition);
                    if (partitionIndex >= partitions.Length)
                        return;

                    var channel = channels[partitionIndex];
                    try
                    {
                        var chunkWriter = new ChannelChunkWriter<T>(channel.Writer, stop.Token);
                        processPartition(partitions[partitionIndex], chunkWriter, stop.Token);
                        channel.Writer.TryComplete();
                    }
                    catch (Exception exception)
                    {
                        Interlocked.CompareExchange(
                            ref firstFailure,
                            ExceptionDispatchInfo.Capture(exception),
                            null);
                        channel.Writer.TryComplete(exception);
                        stop.Cancel();
                        return;
                    }
                }
            }, CancellationToken.None);
        }

        long emittedRows = 0;
        try
        {
            for (var partitionIndex = 0; partitionIndex < channels.Length; partitionIndex++)
            {
                var reader = channels[partitionIndex].Reader;
                while (reader.WaitToReadAsync(stop.Token).AsTask().GetAwaiter().GetResult())
                {
                    while (reader.TryRead(out var chunk))
                    {
                        destination.Write(chunk);
                        emittedRows = checked(emittedRows + chunk.Count);
                    }
                }

                reportRowsRead?.Invoke(partitions[partitionIndex].RowCount);
            }

            Task.WaitAll(workers);
            firstFailure?.Throw();
            return emittedRows;
        }
        catch
        {
            stop.Cancel();
            try
            {
                Task.WaitAll(workers);
            }
            catch (AggregateException)
            {
                // Worker failures are captured above so the original exception can be preserved.
            }

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            firstFailure?.Throw();
            throw;
        }
    }

    private static Channel<IReadOnlyList<T>>[] CreateChannels<T>(int count)
    {
        var channels = new Channel<IReadOnlyList<T>>[count];
        for (var index = 0; index < channels.Length; index++)
        {
            channels[index] = Channel.CreateBounded<IReadOnlyList<T>>(new BoundedChannelOptions(
                BufferedChunksPerPartition)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
        }

        return channels;
    }

    private sealed class ChannelChunkWriter<T>(
        ChannelWriter<IReadOnlyList<T>> writer,
        CancellationToken cancellationToken) : IChunkWriter<T>
    {
        public CancellationToken CancellationToken => cancellationToken;

        public void Write(IReadOnlyList<T> chunk)
        {
            writer.WriteAsync(chunk, cancellationToken).AsTask().GetAwaiter().GetResult();
        }
    }
}
