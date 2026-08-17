#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesParallelBlockScanPipeline
{
    public const int DefaultBlockSize = 2 * 1024 * 1024;
    public const int MaximumRecordSize = 256 * 1024 * 1024;
    private const int ZeroColumnChunkRows = 1024 * 1024;
    private const int MaximumReadAhead = 32;
    private const int MaximumReorderDepth = 32;
    private const int MaximumFramingWorkers = 4;

    private readonly ISeparatedValuesRecordBoundaryAnalyzer _boundaryAnalyzer;
    private readonly ISeparatedValuesByteBlockSourceFactory _blockSourceFactory;
    private readonly int _blockSize;

    public SeparatedValuesParallelBlockScanPipeline(
        ISeparatedValuesByteBlockSourceFactory? blockSourceFactory = null,
        ISeparatedValuesRecordBoundaryAnalyzer? boundaryAnalyzer = null,
        int blockSize = DefaultBlockSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
        _blockSourceFactory = blockSourceFactory ?? new RandomAccessSeparatedValuesByteBlockSourceFactory();
        _boundaryAnalyzer = boundaryAnalyzer ?? new QuoteParitySeparatedValuesRecordBoundaryAnalyzer();
        _blockSize = blockSize;
    }

    public long Execute(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        int workerCount,
        CancellationToken cancellationToken)
    {
        try
        {
            return RunAsync(
                    request,
                    contract,
                    writer,
                    progress,
                    chunkSize,
                    workerCount,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private async Task<long> RunAsync(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        int workerCount,
        CancellationToken cancellationToken)
    {
        var framingOnly = CanUseDeclaredFramingKernel(request, contract);
        if (framingOnly)
            workerCount = Math.Min(workerCount, MaximumFramingWorkers);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var reorderSlots = new SemaphoreSlim(
            Math.Clamp(workerCount * 2, 4, MaximumReorderDepth),
            Math.Clamp(workerCount * 2, 4, MaximumReorderDepth));
        var work = Channel.CreateBounded<SeparatedValuesBlockWorkItem>(new BoundedChannelOptions(
            Math.Clamp(workerCount, 2, MaximumReadAhead))
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        var results = Channel.CreateUnbounded<SeparatedValuesBlockWorkResult>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });

        var framingOptions = new SeparatedValuesFramingAnalysisOptions(
            request.SeparatorByte,
            contract.Snapshot.Columns.Length);
        var dialect = request.Dialect ?? contract.Dialect;
        var producer = ProduceAsync(
            contract,
            dialect,
            work.Writer,
            workerCount,
            reorderSlots,
            framingOnly,
            framingOptions,
            stop.Token);
        var workers = new Task[workerCount];
        for (var index = 0; index < workers.Length; index++)
        {
            workers[index] = Task.Run(() => WorkerAsync(
                request,
                contract,
                work.Reader,
                results.Writer,
                chunkSize,
                stop));
        }

        var completion = CompleteAsync(producer, workers, results.Writer, stop);
        var pending = new SortedDictionary<long, SeparatedValuesBlockWorkResult>();
        var summaryBuilder = new SeparatedValuesStructuralSummaryBuilder(
            contract.Snapshot.Identity,
            contract.DataStartOffset);
        var nextSequence = 0L;
        var emitted = 0L;

        try
        {
            await foreach (var result in results.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                pending.Add(result.Sequence, result);
                while (pending.Remove(nextSequence, out var ready))
                {
                    if (ready.Exception is not null)
                    {
                        reorderSlots.Release();
                        stop.Cancel();
                        ExceptionDispatchInfo.Capture(ready.Exception).Throw();
                    }

                    summaryBuilder.ObserveRange(
                        ready.StartRow,
                        ready.RowsRead,
                        ready.FirstRecordOffset,
                        ready.LastRecordEndOffset);
                    foreach (var chunk in ready.Chunks)
                        writer.Write(chunk);
                    progress.RowsRead(ready.RowsRead);
                    emitted += ready.RowsEmitted;
                    nextSequence++;
                    reorderSlots.Release();
                }
            }

            await completion.ConfigureAwait(false);
            if (pending.Count != 0)
                throw new InvalidOperationException("Separated-values ordered output ended with missing block results.");
            SeparatedValuesStructuralSummaryCache.Store(summaryBuilder.Build());
            return emitted;
        }
        finally
        {
            stop.Cancel();
            try
            {
                await completion.ConfigureAwait(false);
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
            }

            while (work.Reader.TryRead(out var item))
                item.Dispose();
        }
    }

    private async Task ProduceAsync(
        SeparatedValuesSourceContract contract,
        SeparatedValuesDialect dialect,
        ChannelWriter<SeparatedValuesBlockWorkItem> writer,
        int workerCount,
        SemaphoreSlim reorderSlots,
        bool framingOnly,
        SeparatedValuesFramingAnalysisOptions framingOptions,
        CancellationToken cancellationToken)
    {
        var snapshot = contract.Snapshot;
        var pending = new Queue<Task<SeparatedValuesBlockAnalysis>>();
        var finalized = new Queue<Task<SeparatedValuesFinalizedBlock>>();
        using var source = _blockSourceFactory.Open(
            snapshot.Identity.CanonicalPath,
            snapshot.Identity.Length);
        using var carry = new PooledRecordAccumulator(MaximumRecordSize);
        var readAhead = Math.Clamp(workerCount, 4, MaximumReadAhead);
        var nextOffset = contract.DataStartOffset;
        var blockSequence = 0L;
        var workSequence = 0L;
        var startRow = 0L;
        var incomingQuoted = false;
        var carryStartOffset = contract.DataStartOffset;

        try
        {
            while (pending.Count < readAhead && nextOffset < snapshot.Identity.Length)
                ScheduleNext();

            while (pending.Count > 0 || finalized.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (finalized.Count < readAhead && pending.Count > 0)
                {
                    var readAnalysis = await pending.Dequeue().ConfigureAwait(false);
                    while (pending.Count < readAhead && nextOffset < snapshot.Identity.Length)
                        ScheduleNext();
                    var startsQuoted = incomingQuoted;
                    incomingQuoted ^= readAnalysis.QuoteParity;
                    finalized.Enqueue(FinalizeAnalysisAsync(readAnalysis, startsQuoted, cancellationToken));
                }

                if (finalized.Count == 0)
                    continue;

                var finalizedBlock = await finalized.Dequeue().ConfigureAwait(false);
                var analysis = finalizedBlock.Analysis;
                var block = analysis.Block;
                SeparatedValuesBlockWorkItem? workItem = null;
                var reorderSlotOwned = false;
                try
                {
                    if (analysis.NewlineCount == 0)
                    {
                        carry.Append(block.Span);
                        block.Dispose();
                        analysis.Dispose();
                        continue;
                    }

                    var firstBoundary = finalizedBlock.FirstBoundary;
                    var prefix = carry.TakeRecord(block.Span[..firstBoundary]);
                    var prefixStartOffset = carryStartOffset;
                    var prefixEndOffset = block.Offset + firstBoundary + 1L;
                    var rowCount = finalizedBlock.TailRowCount + (prefix.Length > 0 ? 1 : 0);
                    var firstRecordOffset = prefix.Length > 0 ? prefixStartOffset : 0L;
                    var lastRecordEndOffset = prefix.Length > 0 ? prefixEndOffset : 0L;
                    if (finalizedBlock.TailRowCount > 0)
                    {
                        if (prefix.Length == 0)
                            firstRecordOffset = block.Offset + finalizedBlock.FirstTailRecordOffset;
                        lastRecordEndOffset = block.Offset + finalizedBlock.LastTailRecordEndOffset;
                    }

                    var lastBoundary = finalizedBlock.LastBoundary;
                    carryStartOffset = block.Offset + lastBoundary + 1L;
                    carry.Append(block.Span[(lastBoundary + 1)..]);

                    var newlineBuffer = analysis.DetachNewlines();
                    workItem = new SeparatedValuesBlockWorkItem(
                        workSequence,
                        startRow,
                        rowCount,
                        block,
                        newlineBuffer,
                        analysis.NewlineCount,
                        firstBoundary,
                        lastBoundary,
                        analysis.IsCompact,
                        analysis.TailIsAscii,
                        analysis.ValidationError,
                        analysis.ValidationErrorOffset,
                        analysis.ValidationErrorTailRow,
                        prefix,
                        prefixStartOffset,
                        prefixEndOffset,
                        firstRecordOffset,
                        lastRecordEndOffset);
                    analysis.Dispose();

                    if (rowCount == 0)
                    {
                        workItem.Dispose();
                        workItem = null;
                    }
                    else
                    {
                        await reorderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                        reorderSlotOwned = true;
                        await writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
                        workItem = null;
                        reorderSlotOwned = false;
                        startRow += rowCount;
                        workSequence++;
                    }
                }
                catch
                {
                    if (reorderSlotOwned)
                        reorderSlots.Release();
                    workItem?.Dispose();
                    analysis.Dispose();
                    block.Dispose();
                    throw;
                }
            }

            if (incomingQuoted)
            {
                throw new InvalidDataException(
                    $"The final quoted field in '{snapshot.Identity.CanonicalPath}' is not terminated.");
            }

            var finalRecord = carry.TakeFinalRecord();
            if (finalRecord.Length > 0)
            {
                var finalItem = new SeparatedValuesBlockWorkItem(
                    workSequence,
                    startRow,
                    1,
                    null,
                    null,
                    0,
                    -1,
                    -1,
                    false,
                    false,
                    SeparatedValuesCompactValidationError.None,
                    -1,
                    0,
                    finalRecord,
                    carryStartOffset,
                    snapshot.Identity.Length,
                    carryStartOffset,
                    snapshot.Identity.Length);
                var reorderSlotOwned = false;
                try
                {
                    await reorderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                    reorderSlotOwned = true;
                    await writer.WriteAsync(finalItem, cancellationToken).ConfigureAwait(false);
                    reorderSlotOwned = false;
                }
                catch
                {
                    if (reorderSlotOwned)
                        reorderSlots.Release();
                    finalItem.Dispose();
                    throw;
                }
            }

            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
            throw;
        }
        finally
        {
            while (pending.Count > 0)
            {
                try
                {
                    var analysis = await pending.Dequeue().ConfigureAwait(false);
                    analysis.Block.Dispose();
                    analysis.Dispose();
                }
                catch
                {
                }
            }

            while (finalized.Count > 0)
            {
                try
                {
                    var finalizedBlock = await finalized.Dequeue().ConfigureAwait(false);
                    finalizedBlock.Analysis.Block.Dispose();
                    finalizedBlock.Analysis.Dispose();
                }
                catch
                {
                }
            }
        }

        void ScheduleNext()
        {
            var count = (int)Math.Min(_blockSize, snapshot.Identity.Length - nextOffset);
            pending.Enqueue(ReadAndAnalyzeAsync(
                source,
                blockSequence++,
                nextOffset,
                count,
                framingOnly,
                framingOptions,
                dialect,
                cancellationToken));
            nextOffset += count;
        }
    }

    private static async Task<SeparatedValuesFinalizedBlock> FinalizeAnalysisAsync(
        SeparatedValuesBlockAnalysis analysis,
        bool startsQuoted,
        CancellationToken cancellationToken)
    {
        try
        {
            if (analysis.IsCompact)
            {
                analysis.SelectRecordBoundaries(startsQuoted);
                return new SeparatedValuesFinalizedBlock(
                    analysis,
                    analysis.NewlineCount == 0 ? 0 : analysis.TailRowCount,
                    analysis.FirstTailRecordOffset,
                    analysis.LastTailRecordEndOffset,
                    analysis.FirstBoundary,
                    analysis.LastBoundary);
            }

            using var lease = await SeparatedValuesCpuBudget.AcquireAsync(cancellationToken).ConfigureAwait(false);
            await Task.Yield();
            analysis.SelectRecordBoundaries(startsQuoted);
            var tailRowCount = 0L;
            var firstTailRecordOffset = 0;
            var lastTailRecordEndOffset = 0;
            if (analysis.NewlineCount > 1)
            {
                var boundaries = analysis.Newlines;
                var recordStart = boundaries[0] + 1;
                for (var index = 1; index < boundaries.Length; index++)
                {
                    var recordEnd = TrimCarriageReturn(analysis.Block.Span, recordStart, boundaries[index]);
                    if (recordEnd > recordStart)
                    {
                        if (tailRowCount == 0)
                            firstTailRecordOffset = recordStart;
                        lastTailRecordEndOffset = boundaries[index] + 1;
                        tailRowCount++;
                    }

                    recordStart = boundaries[index] + 1;
                }
            }

            return new SeparatedValuesFinalizedBlock(
                analysis,
                tailRowCount,
                firstTailRecordOffset,
                lastTailRecordEndOffset,
                analysis.NewlineCount == 0 ? -1 : analysis.Newlines[0],
                analysis.NewlineCount == 0 ? -1 : analysis.Newlines[^1]);
        }
        catch
        {
            analysis.Block.Dispose();
            analysis.Dispose();
            throw;
        }
    }

    private async Task<SeparatedValuesBlockAnalysis> ReadAndAnalyzeAsync(
        ISeparatedValuesByteBlockSource source,
        long sequence,
        long offset,
        int count,
        bool framingOnly,
        SeparatedValuesFramingAnalysisOptions framingOptions,
        SeparatedValuesDialect dialect,
        CancellationToken cancellationToken)
    {
        var block = await source.ReadAsync(sequence, offset, count, cancellationToken).ConfigureAwait(false);
        if (block.Length != count)
        {
            block.Dispose();
            throw new EndOfStreamException("Separated-values source ended during a random-access block read.");
        }

        try
        {
            using var lease = await SeparatedValuesCpuBudget.AcquireAsync(cancellationToken).ConfigureAwait(false);
            await Task.Yield();
            return framingOnly
                ? _boundaryAnalyzer.AnalyzeFraming(block, framingOptions, dialect)
                : _boundaryAnalyzer.Analyze(block, dialect);
        }
        catch
        {
            block.Dispose();
            throw;
        }
    }

    private static async Task WorkerAsync(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        ChannelReader<SeparatedValuesBlockWorkItem> work,
        ChannelWriter<SeparatedValuesBlockWorkResult> results,
        int chunkSize,
        CancellationTokenSource stop)
    {
        try
        {
            await foreach (var item in work.ReadAllAsync(stop.Token).ConfigureAwait(false))
            {
                try
                {
                    using var lease = await SeparatedValuesCpuBudget.AcquireAsync(stop.Token).ConfigureAwait(false);
                    results.TryWrite(ProcessWorkItem(request, contract, item, chunkSize, stop.Token));
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    // A deterministic producer or ordered-consumer failure may
                    // cancel work that has not reached its block yet. Do not
                    // publish that shutdown cancellation as a competing block
                    // error; the ordered consumer must see the original cause.
                }
                catch (Exception exception)
                {
                    results.TryWrite(SeparatedValuesBlockWorkResult.Failed(item.Sequence, exception));
                }
                finally
                {
                    item.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    private static SeparatedValuesBlockWorkResult ProcessWorkItem(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        SeparatedValuesBlockWorkItem item,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        if (CanUseDeclaredFramingKernel(request, contract))
            return ProcessFramingWorkItem(request, contract, item, chunkSize, cancellationToken);

        var dialect = request.Dialect ?? contract.Dialect;

        var output = new BufferedChunkWriter(cancellationToken);
        var processor = new SeparatedValuesRowProcessor(
            contract,
            request.ExecutionContext,
            output,
            null,
            chunkSize,
            cancellationToken,
            item.StartRow);

        if (item.Prefix.Length > 0)
            ProcessRecord(
                item.Prefix.Span,
                item.PrefixStartOffset,
                item.PrefixEndOffset,
                request.SeparatorByte,
                dialect,
                processor);

        if (item.Block is not null && item.NewlineCount > 1)
        {
            var bytes = item.Block.Span;
            var boundaries = item.Newlines;
            var recordStart = boundaries[0] + 1;
            for (var index = 1; index < boundaries.Length; index++)
            {
                var recordEnd = TrimCarriageReturn(bytes, recordStart, boundaries[index]);
                if (recordEnd > recordStart)
                {
                    ProcessRecord(
                        bytes[recordStart..recordEnd],
                        item.Block.Offset + recordStart,
                        item.Block.Offset + boundaries[index] + 1L,
                        request.SeparatorByte,
                        dialect,
                        processor);
                }

                recordStart = boundaries[index] + 1;
            }
        }

        processor.Complete();
        if (processor.RowsRead != item.RowCount)
        {
            throw new InvalidDataException(
                $"Separated-values block expected {item.RowCount:N0} records but processed {processor.RowsRead:N0}.");
        }

        return new SeparatedValuesBlockWorkResult(
            item.Sequence,
            item.StartRow,
            processor.RowsRead,
            processor.RowsEmitted,
            item.FirstRecordOffset,
            item.LastRecordEndOffset,
            output.Chunks,
            null);
    }

    private static bool CanUseDeclaredFramingKernel(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract)
    {
        var plan = request.ExecutionContext.Plan;
        var readPlan = SeparatedValuesReadPlan.From(plan);
        return contract.Mode == SeparatedValuesSchemaResolutionMode.Declared &&
               (request.Dialect ?? contract.Dialect).IsStrict &&
               readPlan.ProjectionAccepted &&
               !readPlan.HasResidualWork &&
               plan.AcceptedColumns.Count == 0 &&
               plan.AcceptedPredicate is null &&
               plan.AcceptedSkip is null &&
               plan.AcceptedTake is null;
    }

    private static SeparatedValuesBlockWorkResult ProcessFramingWorkItem(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        SeparatedValuesBlockWorkItem item,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = 0L;
        if (item.Prefix.Length > 0)
        {
            SeparatedValuesFramingKernel.ValidateRecord(
                item.Prefix.Span,
                request.SeparatorByte,
                contract.Snapshot.Columns.Length,
                contract.Snapshot.Identity.CanonicalPath,
                item.StartRow + 1,
                item.PrefixStartOffset);
            validated++;
        }

        if (item.Block is not null && item.NewlineCount > 1)
        {
            var start = item.FirstBoundary + 1;
            var end = item.LastBoundary + 1;
            if (item.CompactTailValidated)
            {
                if (item.CompactValidationError != SeparatedValuesCompactValidationError.None)
                {
                    var rowNumber = item.StartRow + validated + item.CompactValidationErrorTailRow;
                    var message = item.CompactValidationError == SeparatedValuesCompactValidationError.ExcessColumns
                        ? $"contains more than the bound {contract.Snapshot.Columns.Length:N0} columns"
                        : "contains a carriage return outside a quoted field that is not followed by a line feed";
                    throw new InvalidDataException(
                        $"Separated-values source '{contract.Snapshot.Identity.CanonicalPath}' row {rowNumber:N0} " +
                        $"{message}. Byte offset: {item.Block.Offset + item.CompactValidationErrorOffset:N0}.");
                }

                if (!item.CompactTailIsAscii)
                    SeparatedValuesUtf8Reader.ValidateUtf8(item.Block.Span[start..end]);
                validated += item.RowCount - validated;
            }
            else
            {
                validated += SeparatedValuesFramingKernel.ValidateTerminatedRecords(
                    item.Block.Span[start..end],
                    request.SeparatorByte,
                    contract.Snapshot.Columns.Length,
                    contract.Snapshot.Identity.CanonicalPath,
                    item.StartRow + validated,
                    item.Block.Offset + start);
            }
        }

        if (validated != item.RowCount)
        {
            throw new InvalidDataException(
                $"Separated-values block expected {item.RowCount:N0} records but validated {validated:N0}.");
        }

        var chunks = new List<IReadOnlyList<object?[]>>(1);
        var remaining = validated;
        while (remaining > 0)
        {
            var count = (int)Math.Min(Math.Max(chunkSize, ZeroColumnChunkRows), remaining);
            chunks.Add(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), count));
            remaining -= count;
        }

        return new SeparatedValuesBlockWorkResult(
            item.Sequence,
            item.StartRow,
            validated,
            validated,
            item.FirstRecordOffset,
            item.LastRecordEndOffset,
            chunks,
            null);
    }

    private static void ProcessRecord(
        ReadOnlySpan<byte> bytes,
        long startOffset,
        long endOffset,
        byte separator,
        SeparatedValuesDialect dialect,
        SeparatedValuesRowProcessor processor)
    {
        SeparatedValuesUtf8Reader.ValidateUtf8(bytes);
        var record = new SeparatedValuesUtf8Record(bytes, separator, startOffset, endOffset, dialect);
        _ = processor.Process(record);
    }

    private static async Task CompleteAsync(
        Task producer,
        IReadOnlyCollection<Task> workers,
        ChannelWriter<SeparatedValuesBlockWorkResult> results,
        CancellationTokenSource stop)
    {
        Exception? completionException = null;
        try
        {
            await producer.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // A worker may cancel the producer after publishing a deterministic
            // block failure. The ordered consumer must observe that block error,
            // not an incidental TaskCanceledException from shutdown.
        }
        catch (Exception exception)
        {
            completionException = exception;
            stop.Cancel();
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // See the producer cancellation note above.
        }
        catch (Exception exception)
        {
            completionException ??= exception;
            stop.Cancel();
        }

        results.TryComplete(completionException);
    }

    private static int TrimCarriageReturn(ReadOnlySpan<byte> bytes, int start, int end)
    {
        return end > start && bytes[end - 1] == (byte)'\r' ? end - 1 : end;
    }

    private sealed class BufferedChunkWriter(CancellationToken cancellationToken) : IChunkWriter<object?[]>
    {
        private readonly List<IReadOnlyList<object?[]>> _chunks = [];

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public IReadOnlyList<IReadOnlyList<object?[]>> Chunks => _chunks;

        public void Write(IReadOnlyList<object?[]> chunk)
        {
            _chunks.Add(chunk);
        }
    }
}

internal sealed class SeparatedValuesBlockWorkItem : IDisposable
{
    private int _disposed;
    private int[]? _newlines;

    public SeparatedValuesBlockWorkItem(
        long sequence,
        long startRow,
        long rowCount,
        SeparatedValuesByteBlock? block,
        int[]? newlines,
        int newlineCount,
        int firstBoundary,
        int lastBoundary,
        bool compactTailValidated,
        bool compactTailIsAscii,
        SeparatedValuesCompactValidationError compactValidationError,
        int compactValidationErrorOffset,
        long compactValidationErrorTailRow,
        PooledRecord prefix,
        long prefixStartOffset,
        long prefixEndOffset,
        long firstRecordOffset,
        long lastRecordEndOffset)
    {
        Sequence = sequence;
        StartRow = startRow;
        RowCount = rowCount;
        Block = block;
        _newlines = newlines;
        NewlineCount = newlineCount;
        FirstBoundary = firstBoundary;
        LastBoundary = lastBoundary;
        CompactTailValidated = compactTailValidated;
        CompactTailIsAscii = compactTailIsAscii;
        CompactValidationError = compactValidationError;
        CompactValidationErrorOffset = compactValidationErrorOffset;
        CompactValidationErrorTailRow = compactValidationErrorTailRow;
        Prefix = prefix;
        PrefixStartOffset = prefixStartOffset;
        PrefixEndOffset = prefixEndOffset;
        FirstRecordOffset = firstRecordOffset;
        LastRecordEndOffset = lastRecordEndOffset;
    }

    public long Sequence { get; }

    public long StartRow { get; }

    public long RowCount { get; }

    public SeparatedValuesByteBlock? Block { get; }

    public int NewlineCount { get; }

    public int FirstBoundary { get; }

    public int LastBoundary { get; }

    public bool CompactTailValidated { get; }

    public bool CompactTailIsAscii { get; }

    public SeparatedValuesCompactValidationError CompactValidationError { get; }

    public int CompactValidationErrorOffset { get; }

    public long CompactValidationErrorTailRow { get; }

    public ReadOnlySpan<int> Newlines => (_newlines ?? []).AsSpan(0, NewlineCount);

    public PooledRecord Prefix { get; }

    public long PrefixStartOffset { get; }

    public long PrefixEndOffset { get; }

    public long FirstRecordOffset { get; }

    public long LastRecordEndOffset { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Block?.Dispose();
        var newlines = Interlocked.Exchange(ref _newlines, null);
        if (newlines is not null)
            ArrayPool<int>.Shared.Return(newlines);
        Prefix.Dispose();
    }
}

internal sealed record SeparatedValuesFinalizedBlock(
    SeparatedValuesBlockAnalysis Analysis,
    long TailRowCount,
    int FirstTailRecordOffset,
    int LastTailRecordEndOffset,
    int FirstBoundary,
    int LastBoundary);

internal sealed record SeparatedValuesBlockWorkResult(
    long Sequence,
    long StartRow,
    long RowsRead,
    long RowsEmitted,
    long FirstRecordOffset,
    long LastRecordEndOffset,
    IReadOnlyList<IReadOnlyList<object?[]>> Chunks,
    Exception? Exception)
{
    public static SeparatedValuesBlockWorkResult Failed(long sequence, Exception exception)
    {
        return new SeparatedValuesBlockWorkResult(sequence, 0, 0, 0, 0, 0, [], exception);
    }
}

internal sealed class PooledRecordAccumulator : IDisposable
{
    private readonly int _maximumLength;
    private byte[]? _buffer;
    private SeparatedValuesStructuralMemoryBudget.Lease? _memoryLease;

    public PooledRecordAccumulator(int maximumLength)
    {
        _maximumLength = maximumLength;
    }

    public int Length { get; private set; }

    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return;
        var required = checked(Length + bytes.Length);
        if (required > _maximumLength)
            throw new InvalidDataException($"A separated-values record exceeds the {_maximumLength:N0}-byte safety limit.");
        EnsureCapacity(required);
        bytes.CopyTo(_buffer.AsSpan(Length));
        Length = required;
    }

    public PooledRecord TakeRecord(ReadOnlySpan<byte> suffix)
    {
        var trimCarriageReturn = suffix.Length > 0
            ? suffix[^1] == (byte)'\r'
            : Length > 0 && _buffer![Length - 1] == (byte)'\r';
        var length = checked(Length + suffix.Length - (trimCarriageReturn ? 1 : 0));
        if (length == 0)
        {
            Reset();
            return default;
        }

        var record = ArrayPool<byte>.Shared.Rent(length);
        var memoryLease = SeparatedValuesStructuralMemoryBudget.TryAcquire(length);
        if (memoryLease is null)
        {
            ArrayPool<byte>.Shared.Return(record);
            throw new InvalidDataException(
                $"Separated-values overflow storage exceeded the process-wide " +
                $"{SeparatedValuesStructuralMemoryBudget.CapacityBytes:N0}-byte memory budget.");
        }
        var prefixLength = Length;
        if (trimCarriageReturn && suffix.IsEmpty)
            prefixLength--;
        if (prefixLength > 0)
            _buffer.AsSpan(0, prefixLength).CopyTo(record);
        var suffixLength = suffix.Length - (trimCarriageReturn && !suffix.IsEmpty ? 1 : 0);
        if (suffixLength > 0)
            suffix[..suffixLength].CopyTo(record.AsSpan(prefixLength));
        Reset();
        return new PooledRecord(record, length, memoryLease);
    }

    public PooledRecord TakeFinalRecord()
    {
        if (Length == 0)
            return default;
        var buffer = _buffer!;
        var length = Length;
        var memoryLease = _memoryLease;
        _buffer = null;
        _memoryLease = null;
        Length = 0;
        return new PooledRecord(buffer, length, memoryLease);
    }

    public void Dispose()
    {
        Reset();
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer is not null && _buffer.Length >= required)
            return;
        var capacity = Math.Min(
            _maximumLength,
            Math.Max(required, _buffer is null ? 64 * 1024 : checked(_buffer.Length * 2)));
        var memoryLease = SeparatedValuesStructuralMemoryBudget.TryAcquire(capacity);
        if (memoryLease is null)
            throw new InvalidDataException(
                $"Separated-values overflow storage exceeded the process-wide " +
                $"{SeparatedValuesStructuralMemoryBudget.CapacityBytes:N0}-byte memory budget.");
        byte[] replacement;
        try
        {
            replacement = ArrayPool<byte>.Shared.Rent(capacity);
        }
        catch
        {
            memoryLease.Dispose();
            throw;
        }
        if (Length > 0)
            _buffer.AsSpan(0, Length).CopyTo(replacement);
        if (_buffer is not null)
            ArrayPool<byte>.Shared.Return(_buffer);
        _memoryLease?.Dispose();
        _buffer = replacement;
        _memoryLease = memoryLease;
    }

    private void Reset()
    {
        if (_buffer is not null)
            ArrayPool<byte>.Shared.Return(_buffer);
        _memoryLease?.Dispose();
        _buffer = null;
        _memoryLease = null;
        Length = 0;
    }
}

internal readonly struct PooledRecord : IDisposable
{
    private readonly byte[]? _buffer;
    private readonly SeparatedValuesStructuralMemoryBudget.Lease? _memoryLease;

    public PooledRecord(
        byte[] buffer,
        int length,
        SeparatedValuesStructuralMemoryBudget.Lease? memoryLease = null)
    {
        _buffer = buffer;
        _memoryLease = memoryLease;
        Length = length;
    }

    public int Length { get; }

    public ReadOnlySpan<byte> Span => _buffer.AsSpan(0, Length);

    public void Dispose()
    {
        if (_buffer is not null)
            ArrayPool<byte>.Shared.Return(_buffer);
        _memoryLease?.Dispose();
    }
}
