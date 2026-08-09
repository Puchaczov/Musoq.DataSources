using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

/// <summary>
///     Streams rows from a strict UTF-8 JSON file.
/// </summary>
public sealed class JsonSource : RowSourceBase<object[]>
{
    private const int ZeroColumnInputBufferSize = 256 * 1024;
    private const string JsonSourceName = "json";
    private readonly SourceExecutionContext _executionContext;
    private readonly string _path;

    /// <summary>
    ///     Initializes a JSON file source.
    /// </summary>
    /// <param name="path">Path to strict UTF-8 JSON content.</param>
    /// <param name="executionContext">Source execution context.</param>
    public JsonSource(string path, SourceExecutionContext executionContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(executionContext);
        _path = Path.GetFullPath(path);
        _executionContext = executionContext;
    }

    /// <summary>
    ///     Streams bounded chunks to the current Musoq row contract.
    /// </summary>
    /// <param name="writer">Chunk writer.</param>
    protected override void CollectChunks(IChunkWriter<object[]> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, JsonSourceName);
        progress.Begin();
        CancellationTokenSource linkedCancellation = null;
        long rowsRead = 0;

        try
        {
            if (_executionContext.EndWorkToken.IsCancellationRequested || writer.CancellationToken.IsCancellationRequested)
                return;

            var cancellationToken = writer.CancellationToken;
            if (_executionContext.EndWorkToken.CanBeCanceled &&
                !_executionContext.EndWorkToken.Equals(writer.CancellationToken))
            {
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    writer.CancellationToken,
                    _executionContext.EndWorkToken);
                cancellationToken = linkedCancellation.Token;
            }

            var snapshot = JsonSchemaDiscovery.GetSnapshot(_path, cancellationToken);
            EnsurePlanStillMatches(snapshot);
            progress.RowsKnown(snapshot.RowCount);

            if (_executionContext.Plan.AcceptedTake is 0)
                return;

            if (CanUseZeroColumnScan())
            {
                rowsRead = ProcessZeroColumnScan(snapshot, writer, progress, cancellationToken);
                return;
            }

            var maximumParallelism = JsonParallelScanOptions.Resolve(snapshot, _executionContext);
            if (maximumParallelism > 1)
            {
                _ = OrderedParallelPartitionRunner.Run(
                    snapshot.Partitions,
                    maximumParallelism,
                    writer,
                    (partition, partitionWriter, token) => ProcessPartition(
                        snapshot,
                        partition,
                        partitionWriter,
                        token),
                    progress.RowsRead,
                    cancellationToken);
                rowsRead = snapshot.RowCount;
            }
            else
            {
                rowsRead = ProcessSequential(snapshot, writer, progress, cancellationToken);
            }
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(rowsRead);
        }
    }

    private bool CanUseZeroColumnScan()
    {
        var plan = _executionContext.Plan;
        return JsonSourcePlanner.IsProjectionAccepted(plan) &&
               plan.AcceptedColumns.Count == 0 &&
               plan.AcceptedPredicate is null &&
               plan.AcceptedSkip is null &&
               plan.AcceptedTake is null;
    }

    private static long ProcessZeroColumnScan(
        StructuredSchemaSnapshot snapshot,
        IChunkWriter<object[]> writer,
        DataSourceProgressReporter progress,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            snapshot.Identity.CanonicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1,
            FileOptions.SequentialScan);
        if (stream.Length != snapshot.Identity.Length)
            throw new StructuredSourceChangedException(snapshot.Identity.CanonicalPath);

        var buffer = ArrayPool<byte>.Shared.Rent(ZeroColumnInputBufferSize);
        var position = 0L;
        var partitionIndex = 0;
        var rowsRead = 0L;
        var pendingRows = 0L;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;

                position += read;
                while (partitionIndex < snapshot.Partitions.Length &&
                       position >= snapshot.Partitions[partitionIndex].EndOffset)
                {
                    var partitionRows = snapshot.Partitions[partitionIndex].RowCount;
                    progress.RowsRead(partitionRows);
                    rowsRead += partitionRows;
                    pendingRows += partitionRows;
                    while (pendingRows >= RowChunking.DefaultChunkSize)
                    {
                        WriteRepeatedRows(writer, RowChunking.DefaultChunkSize);
                        pendingRows -= RowChunking.DefaultChunkSize;
                    }

                    partitionIndex++;
                }
            }

            if (position != snapshot.Identity.Length ||
                partitionIndex != snapshot.Partitions.Length ||
                rowsRead != snapshot.RowCount)
                throw new StructuredSourceChangedException(snapshot.Identity.CanonicalPath);

            WriteRepeatedRows(writer, pendingRows);
            return rowsRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void WriteRepeatedRows(IChunkWriter<object[]> writer, long rowCount)
    {
        while (rowCount > 0)
        {
            var count = (int)Math.Min(RowChunking.DefaultChunkSize, rowCount);
            writer.Write(new RepeatedValueChunk<object[]>(Array.Empty<object>(), count));
            rowCount -= count;
        }
    }

    private long ProcessSequential(
        StructuredSchemaSnapshot snapshot,
        IChunkWriter<object[]> writer,
        DataSourceProgressReporter progress,
        CancellationToken cancellationToken)
    {
        var processor = new JsonRowProcessor(snapshot, _executionContext, writer, progress, cancellationToken);
        JsonRecordFramer.Read(_path, processor, cancellationToken);
        processor.Complete();
        return processor.RowsRead;
    }

    private void ProcessPartition(
        StructuredSchemaSnapshot snapshot,
        StructuredPartition partition,
        IChunkWriter<object[]> writer,
        CancellationToken cancellationToken)
    {
        var processor = new JsonRowProcessor(snapshot, _executionContext, writer, null, cancellationToken);
        JsonRecordFramer.ReadPartition(_path, partition, processor, cancellationToken);
        processor.Complete();
        if (processor.RowsRead != partition.RowCount)
        {
            throw new StructuredSchemaDriftException(
                snapshot.Identity.CanonicalPath,
                $"partition expected {partition.RowCount:N0} rows but read {processor.RowsRead:N0}");
        }
    }

    private void EnsurePlanStillMatches(StructuredSchemaSnapshot snapshot)
    {
        if (_executionContext.Plan.Properties is null ||
            !_executionContext.Plan.Properties.TryGetValue(JsonPlanning.LayoutPropertyName, out var value) ||
            value is not StructuredExecutionLayout layout)
            return;

        layout.EnsureCompatibleWith(snapshot);
    }
}
