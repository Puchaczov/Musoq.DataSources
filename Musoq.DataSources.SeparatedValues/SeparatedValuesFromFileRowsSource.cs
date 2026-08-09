#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesFromFileRowsSource : RowSourceBase<object?[]>
{
    private const int EarlyTakeInputBufferSize = 64 * 1024;
    private const int EarlyTakeRowLimit = 4096;
    private const int SequentialInputBufferSize = 1024 * 1024;
    private const int ZeroColumnInputBufferSize = 64 * 1024;
    private const string SeparatedValuesSourceName = "separated_values";
    private readonly SourceExecutionContext _executionContext;
    private readonly bool _hasHeader;
    private readonly string _path;
    private readonly string _separator;
    private readonly byte _separatorByte;
    private readonly int _skipLines;

    public SeparatedValuesFromFileRowsSource(
        string filePath,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext executionContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        if (separator.Length != 1 || separator[0] > 0x7f)
            throw new ArgumentException("The separator must be one ASCII character.", nameof(separator));

        _path = Path.GetFullPath(filePath);
        _separator = separator;
        _separatorByte = checked((byte)separator[0]);
        _hasHeader = hasHeader;
        _skipLines = skipLines;
        _executionContext = executionContext;
    }

    protected override void CollectChunks(IChunkWriter<object?[]> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, SeparatedValuesSourceName);
        progress.Begin();
        CancellationTokenSource? linkedCancellation = null;
        long rowsEmitted = 0;

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

            var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(
                _path,
                _separator,
                _hasHeader,
                _skipLines,
                cancellationToken);
            EnsurePlanStillMatches(snapshot);
            progress.RowsKnown(snapshot.RowCount);

            var readPlan = SeparatedValuesReadPlan.From(_executionContext.Plan);
            var projectedColumns = readPlan.ProjectionAccepted
                ? _executionContext.Plan.AcceptedColumns.Count
                : _executionContext.AllColumns.Count > 0
                    ? _executionContext.AllColumns.Count
                    : snapshot.Columns.Length;
            var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
                snapshot.Identity.Length,
                projectedColumns,
                snapshot.Columns.Length,
                _executionContext.Plan.AcceptedTake,
                readPlan.HasResidualWork,
                readPlan.ProjectionAccepted));
            progress.SetRowsReadReportInterval(strategy.RowChunkSize);

            if (_executionContext.Plan.AcceptedTake is 0)
                return;

            if (CanUseZeroColumnScan(readPlan))
            {
                rowsEmitted = ProcessZeroColumnScan(
                    snapshot,
                    writer,
                    progress,
                    strategy.RowChunkSize,
                    cancellationToken);
                return;
            }

            var maximumParallelism = SeparatedValuesParallelScanOptions.Resolve(snapshot, _executionContext);
            if (maximumParallelism > 1)
            {
                rowsEmitted = OrderedParallelPartitionRunner.Run(
                    snapshot.Partitions,
                    maximumParallelism,
                    writer,
                    (partition, partitionWriter, token) => ProcessPartition(
                        snapshot,
                        partition,
                        partitionWriter,
                        strategy.RowChunkSize,
                        token),
                    progress.RowsRead,
                    cancellationToken);
            }
            else
            {
                rowsEmitted = ProcessSequential(snapshot, writer, progress, strategy.RowChunkSize, cancellationToken);
            }
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(rowsEmitted);
        }
    }

    private bool CanUseZeroColumnScan(SeparatedValuesReadPlan readPlan)
    {
        var plan = _executionContext.Plan;
        return readPlan.ProjectionAccepted &&
               !readPlan.HasResidualWork &&
               plan.AcceptedColumns.Count == 0 &&
               plan.AcceptedPredicate is null &&
               plan.AcceptedSkip is null &&
               plan.AcceptedTake is null;
    }

    private static long ProcessZeroColumnScan(
        StructuredSchemaSnapshot snapshot,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
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
                    while (pendingRows >= chunkSize)
                    {
                        WriteRepeatedRows(writer, chunkSize, chunkSize);
                        pendingRows -= chunkSize;
                    }

                    partitionIndex++;
                }
            }

            if (position != snapshot.Identity.Length ||
                partitionIndex != snapshot.Partitions.Length ||
                rowsRead != snapshot.RowCount)
                throw new StructuredSourceChangedException(snapshot.Identity.CanonicalPath);

            WriteRepeatedRows(writer, pendingRows, chunkSize);
            return rowsRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void WriteRepeatedRows(
        IChunkWriter<object?[]> writer,
        long rowCount,
        int chunkSize)
    {
        while (rowCount > 0)
        {
            var count = (int)Math.Min(chunkSize, rowCount);
            writer.Write(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), count));
            rowCount -= count;
        }
    }

    private long ProcessSequential(
        StructuredSchemaSnapshot snapshot,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        var processor = new SeparatedValuesRowProcessor(
            snapshot,
            _executionContext,
            writer,
            progress,
            chunkSize,
            cancellationToken);
        using var reader = new SeparatedValuesUtf8Reader(
            snapshot.Identity.CanonicalPath,
            _separatorByte,
            _skipLines,
            _executionContext.Plan.AcceptedTake is > 0 and <= EarlyTakeRowLimit &&
            !_executionContext.Plan.AcceptedSkip.HasValue
                ? EarlyTakeInputBufferSize
                : SequentialInputBufferSize,
            cancellationToken);
        if (_hasHeader && !reader.TryRead(out _))
            throw new StructuredSchemaDriftException(snapshot.Identity.CanonicalPath, "the header disappeared");

        while (reader.TryRead(out var record) && processor.Process(record))
        {
        }

        processor.Complete();
        return processor.RowsEmitted;
    }

    private void ProcessPartition(
        StructuredSchemaSnapshot snapshot,
        StructuredPartition partition,
        IChunkWriter<object?[]> writer,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        var processor = new SeparatedValuesRowProcessor(
            snapshot,
            _executionContext,
            writer,
            null,
            chunkSize,
            cancellationToken,
            partition.StartRow);
        using var reader = new SeparatedValuesUtf8Reader(
            snapshot.Identity.CanonicalPath,
            _separatorByte,
            partition.StartOffset,
            partition.EndOffset,
            cancellationToken);

        while (reader.TryRead(out var record) && processor.Process(record))
        {
        }

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
            !_executionContext.Plan.Properties.TryGetValue(SeparatedValuesPlanning.LayoutPropertyName, out var value) ||
            value is not StructuredExecutionLayout layout)
            return;

        layout.EnsureCompatibleWith(snapshot);
    }
}
