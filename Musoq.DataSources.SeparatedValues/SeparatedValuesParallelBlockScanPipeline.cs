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
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesParallelBlockScanPipeline : ISeparatedValuesParallelQueryScanPipeline
{
    public const int DefaultBlockSize = 2 * 1024 * 1024;
    public const int DefaultIoDepth = 4;
    public const int MaximumRecordSize = 256 * 1024 * 1024;
    private const int MaximumReadAhead = 32;
    private const int MaximumReorderDepth = 32;

    private readonly ISeparatedValuesRecordBoundaryAnalyzer _boundaryAnalyzer;
    private readonly ISeparatedValuesByteBlockSourceFactory _blockSourceFactory;
    private readonly int _blockSize;
    private readonly int _ioDepth;
    private readonly ISeparatedValuesOutputMemoryBudget _outputMemoryBudget;
    private readonly bool _yieldBeforeCpuWork;

    public SeparatedValuesParallelBlockScanPipeline(
        ISeparatedValuesByteBlockSourceFactory? blockSourceFactory = null,
        ISeparatedValuesRecordBoundaryAnalyzer? boundaryAnalyzer = null,
        int blockSize = DefaultBlockSize,
        ISeparatedValuesOutputMemoryBudget? outputMemoryBudget = null,
        int ioDepth = DefaultIoDepth,
        bool yieldBeforeCpuWork = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ioDepth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ioDepth, MaximumReadAhead);
        _blockSourceFactory = blockSourceFactory ?? new RandomAccessSeparatedValuesByteBlockSourceFactory();
        _boundaryAnalyzer = boundaryAnalyzer ?? new QuoteParitySeparatedValuesRecordBoundaryAnalyzer();
        _blockSize = blockSize;
        _outputMemoryBudget = outputMemoryBudget ?? SeparatedValuesOutputMemoryBudget.Shared;
        _ioDepth = ioDepth;
        _yieldBeforeCpuWork = yieldBeforeCpuWork;
    }

    public long Execute<TRow, TMaterializer>(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        SeparatedValuesQueryShapeMapping mapping,
        QueryRowShape shape,
        IChunkWriter<TRow> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        int workerCount,
        CancellationToken cancellationToken)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        var materialization = new QueryParallelMaterialization<TRow, TMaterializer>(
            SeparatedValuesRecordProgram.CompileQuery(contract, request.ExecutionContext, mapping),
            SeparatedValuesQueryOutputMemoryEstimator.Create<TRow>(shape));
        try
        {
            return RunAsync<TRow, QueryParallelMaterialization<TRow, TMaterializer>>(
                    request,
                    contract,
                    writer,
                    progress,
                    chunkSize,
                    workerCount,
                    materialization,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private async Task<long> RunAsync<TRow, TMaterialization>(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        IChunkWriter<TRow> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        int workerCount,
        TMaterialization materialization,
        CancellationToken cancellationToken)
        where TMaterialization : struct, IParallelMaterialization<TRow>
    {
        var dialect = request.Dialect ?? contract.Dialect;
        var slice = ParallelSlice.From(request.ExecutionContext.Plan);
        var useCompactAnalysis = dialect.IsStrict;
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
        var results = Channel.CreateUnbounded<SeparatedValuesBlockWorkResult<TRow>>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });

        var framingOptions = new SeparatedValuesFramingAnalysisOptions(
            request.SeparatorByte,
            contract.Snapshot.Columns.Length);
        var producer = ProduceAsync<TRow, TMaterialization>(
            contract,
            dialect,
            work.Writer,
            workerCount,
            reorderSlots,
            useCompactAnalysis,
            framingOptions,
            materialization,
            slice,
            stop.Token);
        var workers = new Task[workerCount];
        for (var index = 0; index < workers.Length; index++)
        {
            workers[index] = Task.Run(() => WorkerAsync<TRow, TMaterialization>(
                request,
                contract,
                work.Reader,
                results.Writer,
                chunkSize,
                materialization,
                stop));
        }

        var completion = CompleteAsync(producer, workers, results.Writer, stop);
        var pending = new SortedDictionary<long, SeparatedValuesBlockWorkResult<TRow>>();
        var nextSequence = 0L;
        var emitted = 0L;

        try
        {
            await foreach (var result in results.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                pending.Add(result.Sequence, result);
                while (pending.Remove(nextSequence, out var ready))
                {
                    try
                    {
                        if (ready.Exception is not null)
                        {
                            stop.Cancel();
                            ExceptionDispatchInfo.Capture(ready.Exception).Throw();
                        }

                        foreach (var chunk in ready.Chunks)
                            writer.Write(chunk);
                        progress.RowsRead(ready.RowsRead);
                        emitted += ready.RowsEmitted;
                        nextSequence++;
                    }
                    finally
                    {
                        ready.Dispose();
                        reorderSlots.Release();
                    }
                }
            }

            var completedSummary = await completion.ConfigureAwait(false);
            if (pending.Count != 0)
                throw new InvalidOperationException("Separated-values ordered output ended with missing block results.");
            if (completedSummary is not null)
                SeparatedValuesStructuralSummaryCache.Store(completedSummary);
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
            foreach (var result in pending.Values)
                result.Dispose();
            while (results.Reader.TryRead(out var result))
                result.Dispose();
        }
    }

    private async Task<SeparatedValuesStructuralSummary?> ProduceAsync<TRow, TMaterialization>(
        SeparatedValuesSourceContract contract,
        SeparatedValuesDialect dialect,
        ChannelWriter<SeparatedValuesBlockWorkItem> writer,
        int workerCount,
        SemaphoreSlim reorderSlots,
        bool useCompactAnalysis,
        SeparatedValuesFramingAnalysisOptions framingOptions,
        TMaterialization materialization,
        ParallelSlice slice,
        CancellationToken cancellationToken)
        where TMaterialization : struct, IParallelMaterialization<TRow>
    {
        var snapshot = contract.Snapshot;
        var pending = new Queue<Task<SeparatedValuesBlockAnalysis>>();
        var finalized = new Queue<Task<SeparatedValuesFinalizedBlock>>();
        using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var source = _blockSourceFactory.Open(
            snapshot.Identity.CanonicalPath,
            snapshot.Identity.Length);
        using var carry = new PooledRecordAccumulator(MaximumRecordSize);
        var readAhead = _ioDepth;
        var nextOffset = contract.DataStartOffset;
        var blockSequence = 0L;
        var workSequence = 0L;
        var startRow = 0L;
        var incomingQuoted = false;
        var carryStartOffset = contract.DataStartOffset;
        var sliceComplete = false;
        var summaryBuilder = contract.StructuralSummary is null
            ? new SeparatedValuesStructuralSummaryBuilder(snapshot.Identity, contract.DataStartOffset)
            : null;

        if (slice.Enabled && slice.StartRow > 0 && contract.StructuralSummary is not null)
        {
            if (!contract.StructuralSummary.TryFindRow(slice.StartRow, out var summaryBlock))
            {
                writer.TryComplete();
                return null;
            }

            nextOffset = summaryBlock.FirstRecordOffset;
            startRow = summaryBlock.StartRow;
            carryStartOffset = summaryBlock.FirstRecordOffset;
        }

        try
        {
            while (pending.Count < readAhead && nextOffset < snapshot.Identity.Length)
                ScheduleNext();

            while (pending.Count > 0 || finalized.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Await a pending analysis only when there is no finalized block available to
                // drain. A later read may be waiting for structural-memory permits held by the
                // already finalized blocks, so filling this queue eagerly can deadlock read-ahead.
                if (finalized.Count == 0 && pending.Count > 0)
                    await QueueNextFinalizedAsync().ConfigureAwait(false);

                while (finalized.Count < readAhead &&
                       pending.Count > 0 &&
                       pending.Peek().IsCompletedSuccessfully)
                {
                    await QueueNextFinalizedAsync().ConfigureAwait(false);
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
                    var blockEndRow = checked(startRow + rowCount);
                    var selectedStartRow = Math.Max(startRow, slice.StartRow);
                    var selectedEndRow = Math.Min(blockEndRow, slice.EndRowExclusive);
                    var selectedRowOffset = Math.Max(0, selectedStartRow - startRow);
                    var selectedRowCount = Math.Max(0, selectedEndRow - selectedStartRow);
                    var firstRecordOffset = prefix.Length > 0 ? prefixStartOffset : 0L;
                    var lastRecordEndOffset = prefix.Length > 0 ? prefixEndOffset : 0L;
                    if (finalizedBlock.TailRowCount > 0)
                    {
                        if (prefix.Length == 0)
                            firstRecordOffset = block.Offset + finalizedBlock.FirstTailRecordOffset;
                        lastRecordEndOffset = block.Offset + finalizedBlock.LastTailRecordEndOffset;
                    }
                    if (rowCount > 0)
                    {
                        summaryBuilder?.ObserveRange(
                            startRow,
                            rowCount,
                            firstRecordOffset,
                            lastRecordEndOffset);
                    }

                    var lastBoundary = finalizedBlock.LastBoundary;
                    carryStartOffset = block.Offset + lastBoundary + 1L;
                    carry.Append(block.Span[(lastBoundary + 1)..]);

                    var newlineBuffer = analysis.DetachNewlines();
                    var newlineMemoryLease = analysis.DetachNewlineMemoryLease();
                    workItem = new SeparatedValuesBlockWorkItem(
                        workSequence,
                        startRow,
                        rowCount,
                        selectedRowOffset,
                        selectedRowCount,
                        block,
                        newlineBuffer,
                        newlineMemoryLease,
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

                    if (selectedRowCount == 0)
                    {
                        workItem.Dispose();
                        workItem = null;
                    }
                    else
                    {
                        await reorderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                        reorderSlotOwned = true;
                        var tailEncodedBytes = finalizedBlock.TailRowCount == 0
                            ? 0L
                            : finalizedBlock.LastTailRecordEndOffset - finalizedBlock.FirstTailRecordOffset;
                        var estimatedOutputBytes = materialization.EstimateRetainedOutputBytes(
                            selectedRowCount,
                            prefix.Length + tailEncodedBytes);
                        workItem.AttachOutputMemoryLease(
                            await _outputMemoryBudget.AcquireAsync(
                                    estimatedOutputBytes,
                                    cancellationToken)
                                .ConfigureAwait(false));
                        await writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
                        workItem = null;
                        reorderSlotOwned = false;
                        workSequence++;
                    }

                    startRow = blockEndRow;
                    if (slice.HasEnd && startRow >= slice.EndRowExclusive)
                    {
                        sliceComplete = true;
                        readCancellation.Cancel();
                        break;
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

            if (!sliceComplete && incomingQuoted)
            {
                throw new InvalidDataException(
                    $"The final quoted field in '{snapshot.Identity.CanonicalPath}' is not terminated.");
            }

            var finalRecord = sliceComplete ? default : carry.TakeFinalRecord();
            if (finalRecord.Length > 0)
            {
                var selectedRowCount = startRow >= slice.StartRow && startRow < slice.EndRowExclusive ? 1L : 0L;
                summaryBuilder?.ObserveRange(
                    startRow,
                    1,
                    carryStartOffset,
                    snapshot.Identity.Length);
                var finalItem = new SeparatedValuesBlockWorkItem(
                    workSequence,
                    startRow,
                    1,
                    0,
                    selectedRowCount,
                    null,
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
                if (selectedRowCount == 0)
                {
                    finalItem.Dispose();
                }
                else
                {
                    var reorderSlotOwned = false;
                    try
                    {
                        await reorderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                        reorderSlotOwned = true;
                        finalItem.AttachOutputMemoryLease(
                            await _outputMemoryBudget.AcquireAsync(
                                    materialization.EstimateRetainedOutputBytes(1, finalRecord.Length),
                                    cancellationToken)
                                .ConfigureAwait(false));
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
            }

            writer.TryComplete();
            return sliceComplete ? null : summaryBuilder?.Build();
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
                useCompactAnalysis,
                framingOptions,
                dialect,
                readCancellation.Token));
            nextOffset += count;
        }

        async Task QueueNextFinalizedAsync()
        {
            var readAnalysis = await pending.Dequeue().ConfigureAwait(false);
            while (pending.Count < readAhead && nextOffset < snapshot.Identity.Length)
                ScheduleNext();

            var startsQuoted = incomingQuoted;
            incomingQuoted ^= readAnalysis.QuoteParity;
            finalized.Enqueue(FinalizeAnalysisAsync(
                readAnalysis,
                startsQuoted,
                _yieldBeforeCpuWork,
                cancellationToken));
        }
    }

    private static async Task<SeparatedValuesFinalizedBlock> FinalizeAnalysisAsync(
        SeparatedValuesBlockAnalysis analysis,
        bool startsQuoted,
        bool yieldBeforeCpuWork,
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
            if (yieldBeforeCpuWork)
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
        bool useCompactAnalysis,
        SeparatedValuesFramingAnalysisOptions framingOptions,
        SeparatedValuesDialect dialect,
        CancellationToken cancellationToken)
    {
        SeparatedValuesByteBlock? block = null;
        SeparatedValuesStructuralMemoryBudget.Lease? inputMemoryLease = null;
        SeparatedValuesStructuralMemoryBudget.Lease? newlineMemoryLease = null;
        SeparatedValuesBlockAnalysis? analysis = null;
        try
        {
            var inputBytes = SeparatedValuesStructuralMemoryBudget.EstimatePooledByteArrayBytes(count);
            var worstCaseNewlineBytes = SeparatedValuesStructuralMemoryBudget.EstimatePooledInt32ArrayBytes(
                count);
            var memoryLeases = await SeparatedValuesStructuralMemoryBudget.AcquirePairAsync(
                    inputBytes,
                    worstCaseNewlineBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            inputMemoryLease = memoryLeases.First;
            newlineMemoryLease = memoryLeases.Second;
            block = await source.ReadAsync(sequence, offset, count, cancellationToken).ConfigureAwait(false);
            block.AttachMemoryLease(inputMemoryLease);
            inputMemoryLease = null;
            if (block.Length != count)
                throw new EndOfStreamException("Separated-values source ended during a random-access block read.");

            using var lease = await SeparatedValuesCpuBudget.AcquireAsync(cancellationToken).ConfigureAwait(false);
            if (_yieldBeforeCpuWork)
                await Task.Yield();
            analysis = useCompactAnalysis
                ? _boundaryAnalyzer.AnalyzeFraming(block, framingOptions, dialect)
                : _boundaryAnalyzer.Analyze(block, dialect);
            analysis.AttachNewlineMemoryLease(newlineMemoryLease);
            newlineMemoryLease = null;
            if (analysis.IsCompact)
                analysis.ReleaseNewlineMemoryLease();
            return analysis;
        }
        catch
        {
            analysis?.Dispose();
            inputMemoryLease?.Dispose();
            newlineMemoryLease?.Dispose();
            block?.Dispose();
            throw;
        }
    }

    private static async Task WorkerAsync<TRow, TMaterialization>(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        ChannelReader<SeparatedValuesBlockWorkItem> work,
        ChannelWriter<SeparatedValuesBlockWorkResult<TRow>> results,
        int chunkSize,
        TMaterialization materialization,
        CancellationTokenSource stop)
        where TMaterialization : struct, IParallelMaterialization<TRow>
    {
        var recordExecutor = materialization.RecordProgram.CreateExecutor();
        try
        {
            await foreach (var item in work.ReadAllAsync(stop.Token).ConfigureAwait(false))
            {
                try
                {
                    using var lease = await SeparatedValuesCpuBudget.AcquireAsync(stop.Token).ConfigureAwait(false);
                    var result = materialization.ProcessWorkItem(
                        request,
                        contract,
                        item,
                        chunkSize,
                        recordExecutor,
                        stop.Token);
                    result.AttachOutputMemoryLease(item.DetachOutputMemoryLease());
                    if (!results.TryWrite(result))
                        result.Dispose();
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
                    var result = SeparatedValuesBlockWorkResult<TRow>.Failed(item.Sequence, exception);
                    result.AttachOutputMemoryLease(item.DetachOutputMemoryLease());
                    if (!results.TryWrite(result))
                        result.Dispose();
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

    private static SeparatedValuesBlockWorkResult<TRow> ProcessProjectedWorkItem<TRow, TProjector>(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        SeparatedValuesBlockWorkItem item,
        int chunkSize,
        SeparatedValuesRecordKernel recordExecutor,
        TProjector projector,
        CancellationToken cancellationToken)
        where TProjector : struct, ISeparatedValuesRowProjector<TRow>
    {
        var dialect = request.Dialect ?? contract.Dialect;

        var output = new BufferedChunkWriter<TRow>(cancellationToken);
        var processor = new SeparatedValuesProjectedRowProcessor<TRow, TProjector>(
            contract,
            request.ExecutionContext,
            output,
            null,
            chunkSize,
            cancellationToken,
            recordExecutor,
            projector,
            item.StartRow + item.SelectedRowOffset,
            sliceAlreadyApplied: true);

        var selectedEnd = checked(item.SelectedRowOffset + item.SelectedRowCount);
        var prefixRows = item.Prefix.Length > 0 ? 1L : 0L;

        if (prefixRows > 0 && item.SelectedRowOffset == 0 && selectedEnd > 0)
            ProcessRecord(
                item.Prefix.Span,
                item.PrefixStartOffset,
                item.PrefixEndOffset,
                request.SeparatorByte,
                dialect,
                processor,
                contract.Snapshot.Identity.CanonicalPath,
                item.StartRow + item.SelectedRowOffset + 1);

        if (item.Block is not null && item.NewlineCount > 1)
        {
            var bytes = item.Block.Span;
            if (item.CompactTailValidated)
            {
                var validationRow = prefixRows + item.CompactValidationErrorTailRow - 1;
                if (item.CompactValidationError == SeparatedValuesCompactValidationError.BareCarriageReturn &&
                    validationRow >= item.SelectedRowOffset &&
                    validationRow < selectedEnd)
                {
                    ThrowCompactValidationError(contract, item, prefixRows);
                }

                var start = item.FirstBoundary + 1;
                var end = item.LastBoundary + 1;
                var tailSkip = Math.Max(0, item.SelectedRowOffset - prefixRows);
                var selectedPrefixRows = item.SelectedRowOffset == 0 && prefixRows > 0 ? 1L : 0L;
                var tailTake = item.SelectedRowCount - selectedPrefixRows;
                ProcessUnquotedTerminatedRecords(
                    bytes,
                    start,
                    end,
                    item.Block.Offset,
                    request.SeparatorByte,
                    dialect,
                    processor,
                    item.CompactTailIsAscii,
                    tailSkip,
                    tailTake,
                    contract.Snapshot.Identity.CanonicalPath,
                    item.StartRow + item.SelectedRowOffset + 1);
            }
            else
            {
                var boundaries = item.Newlines;
                var recordStart = boundaries[0] + 1;
                var physicalRow = prefixRows;
                for (var index = 1; index < boundaries.Length; index++)
                {
                    var recordEnd = TrimCarriageReturn(bytes, recordStart, boundaries[index]);
                    if (recordEnd > recordStart)
                    {
                        if (physicalRow >= item.SelectedRowOffset && physicalRow < selectedEnd)
                        {
                            ProcessRecord(
                                bytes[recordStart..recordEnd],
                                item.Block.Offset + recordStart,
                                item.Block.Offset + boundaries[index] + 1L,
                                request.SeparatorByte,
                                dialect,
                                processor,
                                contract.Snapshot.Identity.CanonicalPath,
                                item.StartRow + item.SelectedRowOffset + processor.RowsRead + 1);
                        }

                        physicalRow++;
                    }

                    recordStart = boundaries[index] + 1;
                }
            }
        }

        processor.Complete();
        if (processor.RowsRead != item.SelectedRowCount)
        {
            throw new InvalidDataException(
                $"Separated-values block expected {item.SelectedRowCount:N0} selected records " +
                $"but processed {processor.RowsRead:N0}.");
        }

        return new SeparatedValuesBlockWorkResult<TRow>(
            item.Sequence,
            item.StartRow + item.SelectedRowOffset,
            processor.RowsRead,
            processor.RowsEmitted,
            item.FirstRecordOffset,
            item.LastRecordEndOffset,
            output.Chunks,
            null);
    }

    private static void ProcessRecord<TRow, TProjector>(
        ReadOnlySpan<byte> bytes,
        long startOffset,
        long endOffset,
        byte separator,
        SeparatedValuesDialect dialect,
        SeparatedValuesProjectedRowProcessor<TRow, TProjector> processor,
        string sourcePath,
        long rowNumber)
        where TProjector : struct, ISeparatedValuesRowProjector<TRow>
    {
        try
        {
            SeparatedValuesUtf8Reader.ValidateUtf8(bytes);
        }
        catch (InvalidDataException exception) when
            (exception.Message.Contains("not valid UTF-8", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Separated-values source '{sourcePath}' row {rowNumber:N0} " +
                "column '<malformed UTF-8 field>' is not valid UTF-8.",
                exception);
        }

        var record = new SeparatedValuesUtf8Record(bytes, separator, startOffset, endOffset, dialect);
        _ = processor.Process(record);
    }

    private static void ProcessUnquotedTerminatedRecords<TRow, TProjector>(
        ReadOnlySpan<byte> bytes,
        int start,
        int end,
        long blockOffset,
        byte separator,
        SeparatedValuesDialect dialect,
        SeparatedValuesProjectedRowProcessor<TRow, TProjector> processor,
        bool recordsAreAscii,
        long skipRecords,
        long takeRecords,
        string sourcePath,
        long firstRowNumber)
        where TProjector : struct, ISeparatedValuesRowProjector<TRow>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skipRecords);
        ArgumentOutOfRangeException.ThrowIfNegative(takeRecords);
        if (takeRecords == 0)
            return;

        var selectedStart = -1;
        var selectedEnd = -1;
        var recordStart = start;
        var recordIndex = 0L;
        var selectedRecords = 0L;
        while (recordStart < end)
        {
            var relativeNewline = bytes[recordStart..end].IndexOf((byte)'\n');
            if (relativeNewline < 0)
                throw new InvalidDataException("A compact separated-values block ended without its expected record boundary.");

            var boundary = recordStart + relativeNewline;
            var recordEnd = TrimCarriageReturn(bytes, recordStart, boundary);
            if (recordEnd > recordStart)
            {
                if (recordIndex >= skipRecords && selectedRecords < takeRecords)
                {
                    if (selectedStart < 0)
                        selectedStart = recordStart;
                    selectedEnd = boundary + 1;
                    selectedRecords++;
                }

                recordIndex++;
            }

            recordStart = boundary + 1;
        }

        if (selectedRecords != takeRecords || selectedStart < 0)
        {
            throw new InvalidDataException(
                $"A compact separated-values block expected {takeRecords:N0} selected records but located " +
                $"{selectedRecords:N0}.");
        }

        if (!recordsAreAscii)
        {
            try
            {
                SeparatedValuesUtf8Reader.ValidateUtf8(bytes[selectedStart..selectedEnd]);
            }
            catch (InvalidDataException exception) when
                (exception.Message.Contains("not valid UTF-8", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Separated-values source '{sourcePath}' row {firstRowNumber:N0} " +
                    "column '<malformed UTF-8 field>' is not valid UTF-8.",
                    exception);
            }
        }

        recordStart = selectedStart;
        while (recordStart < selectedEnd)
        {
            var relativeNewline = bytes[recordStart..selectedEnd].IndexOf((byte)'\n');
            if (relativeNewline < 0)
                throw new InvalidDataException("A compact selected range ended without its expected record boundary.");

            var boundary = recordStart + relativeNewline;
            var recordEnd = TrimCarriageReturn(bytes, recordStart, boundary);
            if (recordEnd > recordStart)
            {
                var record = new SeparatedValuesUtf8Record(
                    bytes[recordStart..recordEnd],
                    separator,
                    blockOffset + recordStart,
                    blockOffset + boundary + 1L,
                    dialect);
                _ = processor.ProcessUnquoted(record, separator);
            }

            recordStart = boundary + 1;
        }
    }

    private static void ThrowCompactValidationError(
        SeparatedValuesSourceContract contract,
        SeparatedValuesBlockWorkItem item,
        long precedingRows)
    {
        var rowNumber = item.StartRow + precedingRows + item.CompactValidationErrorTailRow;
        var message = item.CompactValidationError == SeparatedValuesCompactValidationError.ExcessColumns
            ? $"contains more than the bound {contract.Snapshot.Columns.Length:N0} columns"
            : "contains a carriage return outside a quoted field that is not followed by a line feed";
        throw new InvalidDataException(
            $"Separated-values source '{contract.Snapshot.Identity.CanonicalPath}' row {rowNumber:N0} " +
            $"{message}. Byte offset: {item.Block!.Offset + item.CompactValidationErrorOffset:N0}.");
    }

    private static async Task<SeparatedValuesStructuralSummary?> CompleteAsync<TRow>(
        Task<SeparatedValuesStructuralSummary?> producer,
        IReadOnlyCollection<Task> workers,
        ChannelWriter<SeparatedValuesBlockWorkResult<TRow>> results,
        CancellationTokenSource stop)
    {
        Exception? completionException = null;
        SeparatedValuesStructuralSummary? completedSummary = null;
        try
        {
            completedSummary = await producer.ConfigureAwait(false);
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
        return completedSummary;
    }

    private static int TrimCarriageReturn(ReadOnlySpan<byte> bytes, int start, int end)
    {
        return end > start && bytes[end - 1] == (byte)'\r' ? end - 1 : end;
    }

    private interface IParallelMaterialization<TRow>
    {
        SeparatedValuesRecordProgram RecordProgram { get; }

        long EstimateRetainedOutputBytes(long rowCount, long encodedBytes);

        SeparatedValuesBlockWorkResult<TRow> ProcessWorkItem(
            SeparatedValuesScanRequest request,
            SeparatedValuesSourceContract contract,
            SeparatedValuesBlockWorkItem item,
            int chunkSize,
            SeparatedValuesRecordKernel recordExecutor,
            CancellationToken cancellationToken);
    }

    private readonly struct QueryParallelMaterialization<TRow, TMaterializer>
        : IParallelMaterialization<TRow>
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        private readonly SeparatedValuesQueryOutputMemoryEstimator _outputMemoryEstimator;

        public QueryParallelMaterialization(
            SeparatedValuesRecordProgram recordProgram,
            SeparatedValuesQueryOutputMemoryEstimator outputMemoryEstimator)
        {
            RecordProgram = recordProgram;
            _outputMemoryEstimator = outputMemoryEstimator;
        }

        public SeparatedValuesRecordProgram RecordProgram { get; }

        public long EstimateRetainedOutputBytes(long rowCount, long encodedBytes)
        {
            return _outputMemoryEstimator.Estimate(rowCount, encodedBytes);
        }

        public SeparatedValuesBlockWorkResult<TRow> ProcessWorkItem(
            SeparatedValuesScanRequest request,
            SeparatedValuesSourceContract contract,
            SeparatedValuesBlockWorkItem item,
            int chunkSize,
            SeparatedValuesRecordKernel recordExecutor,
            CancellationToken cancellationToken)
        {
            return ProcessProjectedWorkItem<
                TRow,
                SeparatedValuesQueryRowProjector<TRow, TMaterializer>>(
                request,
                contract,
                item,
                chunkSize,
                recordExecutor,
                recordExecutor.CreateQueryProjector<TRow, TMaterializer>(),
                cancellationToken);
        }
    }

    private readonly record struct ParallelSlice(
        bool Enabled,
        long StartRow,
        long EndRowExclusive,
        bool HasEnd)
    {
        public static ParallelSlice From(SourceExecutionPlan plan)
        {
            var start = Math.Max(0, plan.AcceptedSkip.GetValueOrDefault());
            if (!plan.AcceptedTake.HasValue)
                return new ParallelSlice(start > 0, start, long.MaxValue, false);

            var take = Math.Max(0, plan.AcceptedTake.Value);
            var end = start > long.MaxValue - take ? long.MaxValue : start + take;
            return new ParallelSlice(true, start, end, true);
        }
    }

    private sealed class BufferedChunkWriter<TRow>(CancellationToken cancellationToken) : IChunkWriter<TRow>
    {
        private readonly List<IReadOnlyList<TRow>> _chunks = [];

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public IReadOnlyList<IReadOnlyList<TRow>> Chunks => _chunks;

        public void Write(IReadOnlyList<TRow> chunk)
        {
            _chunks.Add(chunk);
        }
    }
}

internal sealed class SeparatedValuesBlockWorkItem : IDisposable
{
    private int _disposed;
    private SeparatedValuesStructuralMemoryBudget.Lease? _newlineMemoryLease;
    private int[]? _newlines;
    private ISeparatedValuesOutputMemoryLease? _outputMemoryLease;

    public SeparatedValuesBlockWorkItem(
        long sequence,
        long startRow,
        long rowCount,
        long selectedRowOffset,
        long selectedRowCount,
        SeparatedValuesByteBlock? block,
        int[]? newlines,
        SeparatedValuesStructuralMemoryBudget.Lease? newlineMemoryLease,
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
        SelectedRowOffset = selectedRowOffset;
        SelectedRowCount = selectedRowCount;
        Block = block;
        _newlines = newlines;
        _newlineMemoryLease = newlineMemoryLease;
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

    public long SelectedRowOffset { get; }

    public long SelectedRowCount { get; }

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

    public void AttachOutputMemoryLease(ISeparatedValuesOutputMemoryLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (Interlocked.CompareExchange(ref _outputMemoryLease, lease, null) is not null)
        {
            lease.Dispose();
            throw new InvalidOperationException("An output-memory lease is already attached.");
        }
    }

    public ISeparatedValuesOutputMemoryLease? DetachOutputMemoryLease()
    {
        return Interlocked.Exchange(ref _outputMemoryLease, null);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Block?.Dispose();
        var newlines = Interlocked.Exchange(ref _newlines, null);
        if (newlines is not null)
            ArrayPool<int>.Shared.Return(newlines);
        Interlocked.Exchange(ref _newlineMemoryLease, null)?.Dispose();
        Interlocked.Exchange(ref _outputMemoryLease, null)?.Dispose();
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

internal sealed class SeparatedValuesBlockWorkResult<TRow> : IDisposable
{
    private ISeparatedValuesOutputMemoryLease? _outputMemoryLease;

    public SeparatedValuesBlockWorkResult(
        long sequence,
        long startRow,
        long rowsRead,
        long rowsEmitted,
        long firstRecordOffset,
        long lastRecordEndOffset,
        IReadOnlyList<IReadOnlyList<TRow>> chunks,
        Exception? exception)
    {
        Sequence = sequence;
        StartRow = startRow;
        RowsRead = rowsRead;
        RowsEmitted = rowsEmitted;
        FirstRecordOffset = firstRecordOffset;
        LastRecordEndOffset = lastRecordEndOffset;
        Chunks = chunks;
        Exception = exception;
    }

    public long Sequence { get; }

    public long StartRow { get; }

    public long RowsRead { get; }

    public long RowsEmitted { get; }

    public long FirstRecordOffset { get; }

    public long LastRecordEndOffset { get; }

    public IReadOnlyList<IReadOnlyList<TRow>> Chunks { get; }

    public Exception? Exception { get; }

    public void AttachOutputMemoryLease(ISeparatedValuesOutputMemoryLease? lease)
    {
        if (lease is null)
            return;
        if (Interlocked.CompareExchange(ref _outputMemoryLease, lease, null) is not null)
        {
            lease.Dispose();
            throw new InvalidOperationException("An output-memory lease is already attached to the result.");
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _outputMemoryLease, null)?.Dispose();
    }

    public static SeparatedValuesBlockWorkResult<TRow> Failed(long sequence, Exception exception)
    {
        return new SeparatedValuesBlockWorkResult<TRow>(sequence, 0, 0, 0, 0, 0, [], exception);
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
