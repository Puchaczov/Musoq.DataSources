#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Components.Traversal;

internal sealed class SearchFileParallelOptions
{
    private const long DefaultBufferedOutputBytes = 32L * 1024 * 1024;
    private const long MinimumBufferedOutputBytes = 64 * 1024;
    private const long MaximumBufferedOutputBytes = 512L * 1024 * 1024;
    private const int MaximumDefaultWorkerCount = 8;

    public SearchFileParallelOptions(
        int workerCount,
        int maxInFlightFiles,
        int bufferedChunksPerFile = 2,
        int progressReportInterval = RowChunking.DefaultChunkSize,
        SearchFileOutputOrder outputOrder = SearchFileOutputOrder.FileEnumerationOrder,
        long bufferedOutputBytes = DefaultBufferedOutputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInFlightFiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferedChunksPerFile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(progressReportInterval);
        if (maxInFlightFiles < workerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInFlightFiles),
                "The in-flight file bound must be at least the worker count.");
        }

        if (outputOrder is not SearchFileOutputOrder.FileEnumerationOrder and
            not SearchFileOutputOrder.CompletionOrder)
        {
            throw new ArgumentOutOfRangeException(nameof(outputOrder));
        }

        if (bufferedOutputBytes is < MinimumBufferedOutputBytes or > MaximumBufferedOutputBytes)
            throw new ArgumentOutOfRangeException(nameof(bufferedOutputBytes));

        WorkerCount = workerCount;
        MaxInFlightFiles = maxInFlightFiles;
        BufferedChunksPerFile = bufferedChunksPerFile;
        ProgressReportInterval = progressReportInterval;
        OutputOrder = outputOrder;
        BufferedOutputBytes = bufferedOutputBytes;
    }

    public static SearchFileParallelOptions Default { get; } = CreateDefault();

    public static SearchFileParallelOptions Sequential { get; } =
        new(workerCount: 1, maxInFlightFiles: 1, bufferedChunksPerFile: 1);

    public int WorkerCount { get; }

    public int MaxInFlightFiles { get; }

    public int BufferedChunksPerFile { get; }

    public int ProgressReportInterval { get; }

    public SearchFileOutputOrder OutputOrder { get; }

    public long BufferedOutputBytes { get; }

    public static SearchFileParallelOptions FromRuntimeSettings(
        IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var workerCount = ReadWorkerCount(settings);
        return new SearchFileParallelOptions(
            workerCount,
            checked(workerCount * 2),
            bufferedChunksPerFile: 2,
            outputOrder: SearchFileOutputOrder.CompletionOrder,
            bufferedOutputBytes: ReadBufferedOutputBytes(settings));
    }

    private static SearchFileParallelOptions CreateDefault()
    {
        var workerCount = Math.Clamp(
            checked(Environment.ProcessorCount * 2),
            1,
            MaximumDefaultWorkerCount);
        return new(
            workerCount,
            checked(workerCount * 2),
            bufferedChunksPerFile: 2,
            outputOrder: SearchFileOutputOrder.CompletionOrder);
    }

    private static int ReadWorkerCount(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("search.max_parallelism", out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return Math.Clamp(checked(Environment.ProcessorCount * 2), 1, 8);
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < 0 || parsed > 32)
        {
            throw new ArgumentException(
                "Runtime setting 'search.max_parallelism' must be 0 or an integer from 1 through 32.",
                nameof(settings));
        }

        return parsed == 0
            ? Math.Clamp(checked(Environment.ProcessorCount * 2), 1, 8)
            : parsed;
    }

    private static long ReadBufferedOutputBytes(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("search.buffered_output_bytes", out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return DefaultBufferedOutputBytes;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < MinimumBufferedOutputBytes ||
            parsed > MaximumBufferedOutputBytes)
        {
            throw new ArgumentException(
                "Runtime setting 'search.buffered_output_bytes' must be between 65536 and 536870912 bytes.",
                nameof(settings));
        }

        return parsed;
    }
}

internal enum SearchFileOutputOrder
{
    FileEnumerationOrder,
    CompletionOrder
}

internal static class SearchFileParallelCoordinator
{
    public static long Run<T>(
        Func<CancellationToken, IEnumerable<string>> enumerateFiles,
        IChunkWriter<T> destination,
        Action<string, IChunkWriter<T>, CancellationToken> processFile,
        SearchSliceWindow? acceptedWindow,
        Action<long>? reportRowsRead,
        CancellationToken cancellationToken,
        SearchFileParallelOptions? options = null,
        Func<IReadOnlyList<T>, long>? estimateChunkBytes = null)
    {
        ArgumentNullException.ThrowIfNull(enumerateFiles);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(processFile);

        options ??= SearchFileParallelOptions.Default;
        cancellationToken.ThrowIfCancellationRequested();

        if (acceptedWindow?.IsSatisfied == true)
            return 0;

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var work = Channel.CreateBounded<WorkItem<T>>(new BoundedChannelOptions(
            options.MaxInFlightFiles)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false
        });
        var workers = new Task[options.WorkerCount];
        var active = new List<WorkItem<T>>(options.MaxInFlightFiles);
        using var outputCredits = new SearchOutputCreditPool(options.BufferedOutputBytes);
        estimateChunkBytes ??= static chunk =>
            checked(Math.Max(1L, chunk.Count * 256L));
        var progress = reportRowsRead is null
            ? null
            : new RowProgressAccumulator(reportRowsRead, options.ProgressReportInterval);
        ExceptionDispatchInfo? firstFailure = null;

        for (var workerIndex = 0; workerIndex < workers.Length; workerIndex++)
        {
            workers[workerIndex] = Task.Run(
                () => RunWorker(
                    work.Reader,
                    processFile,
                    stop,
                    cancellationToken,
                    ref firstFailure),
                CancellationToken.None);
        }

        long emittedRows = 0;
        try
        {
            var files = enumerateFiles(stop.Token) ?? throw new InvalidOperationException(
                "The Search file enumerator returned null.");
            foreach (var file in files)
            {
                stop.Token.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrEmpty(file);

                while (active.Count >= options.MaxInFlightFiles)
                {
                    emittedRows = checked(emittedRows + DrainNext(
                        active,
                        destination,
                        acceptedWindow,
                        progress,
                        options.OutputOrder,
                        stop.Token));

                    if (acceptedWindow?.IsSatisfied == true)
                    {
                        FinishQuerySatisfied(stop, work, active, workers, progress);
                        progress?.Flush();
                        firstFailure?.Throw();
                        return emittedRows;
                    }
                }

                var item = new WorkItem<T>(
                    file,
                    options.BufferedChunksPerFile,
                    outputCredits,
                    estimateChunkBytes);
                active.Add(item);
                work.Writer.WriteAsync(item, stop.Token).AsTask().GetAwaiter().GetResult();
            }

            work.Writer.TryComplete();
            while (active.Count > 0)
            {
                emittedRows = checked(emittedRows + DrainNext(
                    active,
                    destination,
                    acceptedWindow,
                    progress,
                    options.OutputOrder,
                    stop.Token));

                if (acceptedWindow?.IsSatisfied == true)
                {
                    FinishQuerySatisfied(stop, work, active, workers, progress);
                    progress?.Flush();
                    firstFailure?.Throw();
                    return emittedRows;
                }
            }

            progress?.Flush();
            WaitForWorkers(workers);
            firstFailure?.Throw();
            return emittedRows;
        }
        catch (Exception exception)
        {
            RecordFailure(
                exception,
                cancellationToken,
                stop.Token,
                ref firstFailure);
            stop.Cancel();
            work.Writer.TryComplete();
            AddPendingProgress(active, progress);
            progress?.Flush();
            CompleteOutstanding(work, active);
            WaitForWorkers(workers);

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            firstFailure?.Throw();
            throw;
        }
    }

    private static long DrainNext<T>(
        List<WorkItem<T>> active,
        IChunkWriter<T> destination,
        SearchSliceWindow? acceptedWindow,
        RowProgressAccumulator? progress,
        SearchFileOutputOrder outputOrder,
        CancellationToken cancellationToken)
    {
        if (active.Count == 0)
            throw new InvalidOperationException("There is no active Search file to drain.");

        if (outputOrder == SearchFileOutputOrder.CompletionOrder)
        {
            return DrainCompletionOrder(
                active,
                destination,
                acceptedWindow,
                progress,
                cancellationToken);
        }

        var item = active[0];
        active.RemoveAt(0);
        return Drain(item, destination, acceptedWindow, progress, cancellationToken);
    }

    /// <summary>
    ///     Drains whichever file has output ready instead of waiting for a
    ///     worker to complete. Waiting for completion first can deadlock when
    ///     that worker is blocked on its bounded output channel.
    /// </summary>
    private static long DrainCompletionOrder<T>(
        List<WorkItem<T>> active,
        IChunkWriter<T> destination,
        SearchSliceWindow? acceptedWindow,
        RowProgressAccumulator? progress,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            for (var index = 0; index < active.Count; index++)
            {
                var item = active[index];
                if (item.Output.Reader.TryPeek(out _))
                {
                    return DrainReady(
                        item,
                        destination,
                        acceptedWindow,
                        progress,
                        cancellationToken);
                }

                if (item.CompletionTask.IsCompleted)
                {
                    active.RemoveAt(index);
                    return Drain(item, destination, acceptedWindow, progress, cancellationToken);
                }
            }

            var waiters = new Task[active.Count * 2];
            var waiterIndex = 0;
            for (var index = 0; index < active.Count; index++)
            {
                waiters[waiterIndex++] = active[index].CompletionTask;
                waiters[waiterIndex++] = active[index]
                    .Output
                    .Reader
                    .WaitToReadAsync(cancellationToken)
                    .AsTask();
            }

            WaitForAny(waiters, cancellationToken);
        }
    }

    private static long DrainReady<T>(
        WorkItem<T> item,
        IChunkWriter<T> destination,
        SearchSliceWindow? acceptedWindow,
        RowProgressAccumulator? progress,
        CancellationToken cancellationToken)
    {
        var stagedRows = new List<T>(RowChunking.DefaultChunkSize);
        long emittedRows = 0;

        while (item.Output.Reader.TryRead(out var queued))
        {
            var chunk = queued.Rows;
            ArgumentNullException.ThrowIfNull(chunk);
            try
            {
                foreach (var row in chunk)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (acceptedWindow is not null && !acceptedWindow.TryAccept())
                        continue;

                    stagedRows.Add(row);
                    if (stagedRows.Count >= RowChunking.DefaultChunkSize)
                    {
                        WriteDestination(
                            destination,
                            stagedRows,
                            cancellationToken,
                            ref emittedRows);
                    }
                }
            }
            finally
            {
                item.ReleaseCredit(queued.CreditBytes);
            }
        }

        WriteDestination(
            destination,
            stagedRows,
            cancellationToken,
            ref emittedRows);
        item.RecordDrainedRows(emittedRows);
        return emittedRows;
    }

    private static void FinishQuerySatisfied<T>(
        CancellationTokenSource stop,
        Channel<WorkItem<T>> work,
        List<WorkItem<T>> active,
        Task[] workers,
        RowProgressAccumulator? progress)
    {
        stop.Cancel();
        work.Writer.TryComplete();
        WaitForWorkers(workers);
        AddPendingProgress(active, progress);
        CompleteOutstanding(work, active);
    }

    private static void AddPendingProgress<T>(
        List<WorkItem<T>> active,
        RowProgressAccumulator? progress)
    {
        if (progress is null)
            return;

        foreach (var item in active)
            progress.Add(item.TakeDrainedRows());
    }

    private static void RunWorker<T>(
        ChannelReader<WorkItem<T>> reader,
        Action<string, IChunkWriter<T>, CancellationToken> processFile,
        CancellationTokenSource stop,
        CancellationToken callerCancellationToken,
        ref ExceptionDispatchInfo? firstFailure)
    {
        var workerLease = false;
        try
        {
            SearchMemoryCreditGate.AcquireWorker(stop.Token);
            workerLease = true;
            while (reader.WaitToReadAsync(stop.Token).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var item))
                {
                    try
                    {
                        var writer = new ChannelChunkWriter<T>(item, stop.Token);
                        processFile(item.FilePath, writer, stop.Token);
                        item.Complete();
                    }
                    catch (Exception exception)
                    {
                        RecordFailure(
                            exception,
                            callerCancellationToken,
                            stop.Token,
                            ref firstFailure);
                        item.Complete(exception);
                        stop.Cancel();
                        return;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            RecordFailure(
                exception,
                callerCancellationToken,
                stop.Token,
                ref firstFailure);
            stop.Cancel();
        }
        finally
        {
            if (workerLease)
                SearchMemoryCreditGate.ReleaseWorker();
        }
    }

    private static long Drain<T>(
        WorkItem<T> item,
        IChunkWriter<T> destination,
        SearchSliceWindow? acceptedWindow,
        RowProgressAccumulator? progress,
        CancellationToken cancellationToken)
    {
        var stagedRows = new List<T>(RowChunking.DefaultChunkSize);
        long emittedRows = 0;

        try
        {
            var reader = item.Output.Reader;
            while (reader.WaitToReadAsync(cancellationToken).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var queued))
                {
                    var chunk = queued.Rows;
                    ArgumentNullException.ThrowIfNull(chunk);
                    try
                    {
                        foreach (var row in chunk)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (acceptedWindow is not null && !acceptedWindow.TryAccept())
                                continue;

                            stagedRows.Add(row);
                            if (stagedRows.Count >= RowChunking.DefaultChunkSize)
                            {
                                WriteDestination(
                                    destination,
                                    stagedRows,
                                    cancellationToken,
                                    ref emittedRows);
                            }
                        }
                    }
                    finally
                    {
                        item.ReleaseCredit(queued.CreditBytes);
                    }
                }
            }

            WriteDestination(
                destination,
                stagedRows,
                cancellationToken,
                ref emittedRows);
            item.RecordDrainedRows(emittedRows);
            progress?.Add(item.TakeDrainedRows());
            return emittedRows;
        }
        finally
        {
            // Cleanup is deliberately idempotent because the normal drain and
            // the failure path can both release the same work item.
            item.Dispose();
        }
    }

    private sealed class RowProgressAccumulator
    {
        private readonly Action<long> _reportRowsRead;
        private readonly long _reportInterval;
        private long _pendingRows;

        public RowProgressAccumulator(
            Action<long> reportRowsRead,
            int reportInterval)
        {
            _reportRowsRead = reportRowsRead ?? throw new ArgumentNullException(nameof(reportRowsRead));
            _reportInterval = reportInterval;
        }

        public void Add(long rows)
        {
            if (rows <= 0)
                return;

            _pendingRows = checked(_pendingRows + rows);
            if (_pendingRows >= _reportInterval)
                Flush();
        }

        public void Flush()
        {
            if (_pendingRows == 0)
                return;

            var rows = _pendingRows;
            _pendingRows = 0;
            _reportRowsRead(rows);
        }
    }

    private static void WriteDestination<T>(
        IChunkWriter<T> destination,
        List<T> stagedRows,
        CancellationToken cancellationToken,
        ref long emittedRows)
    {
        if (stagedRows.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            destination.Write(stagedRows.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SearchOutputException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SearchOutputException(
                SearchDiagnosticCatalog.OutputFailed(),
                exception);
        }

        emittedRows = checked(emittedRows + stagedRows.Count);
        stagedRows.Clear();
    }

    private static void RecordFailure(
        Exception exception,
        CancellationToken callerCancellationToken,
        CancellationToken stopToken,
        ref ExceptionDispatchInfo? firstFailure)
    {
        if (callerCancellationToken.IsCancellationRequested ||
            stopToken.IsCancellationRequested)
        {
            return;
        }

        Interlocked.CompareExchange(
            ref firstFailure,
            ExceptionDispatchInfo.Capture(exception),
            null);
    }

    private static void CompleteOutstanding<T>(
        Channel<WorkItem<T>> work,
        List<WorkItem<T>> active)
    {
        while (work.Reader.TryRead(out var queued))
            queued.Dispose();

        while (active.Count > 0)
        {
            var last = active.Count - 1;
            active[last].Dispose();
            active.RemoveAt(last);
        }
    }

    private static void WaitForWorkers(Task[] workers)
    {
        try
        {
            Task.WhenAll(workers).GetAwaiter().GetResult();
        }
        catch (AggregateException)
        {
            // Worker failures are captured before their output channel is
            // completed, so the first failure can be rethrown with its type.
        }
    }

    private static void WaitForAny(
        Task[] tasks,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            Task.WhenAny(tasks).GetAwaiter().GetResult();
            return;
        }

        var cancellation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            cancellation);
        var waiters = new Task[tasks.Length + 1];
        Array.Copy(tasks, waiters, tasks.Length);
        waiters[^1] = cancellation.Task;
        Task.WhenAny(waiters).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private readonly record struct QueuedChunk<T>(
        IReadOnlyList<T> Rows,
        long CreditBytes);

    private sealed class WorkItem<T> : IDisposable
    {
        public WorkItem(
            string filePath,
            int bufferedChunksPerFile,
            SearchOutputCreditPool outputCredits,
            Func<IReadOnlyList<T>, long> estimateChunkBytes)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _outputCredits = outputCredits ?? throw new ArgumentNullException(nameof(outputCredits));
            _estimateChunkBytes = estimateChunkBytes ?? throw new ArgumentNullException(nameof(estimateChunkBytes));
            Output = Channel.CreateBounded<QueuedChunk<T>>(new BoundedChannelOptions(
                bufferedChunksPerFile)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
        }

        public string FilePath { get; }

        public Channel<QueuedChunk<T>> Output { get; }

        public Task CompletionTask => _completion.Task;

        public long EstimateChunkBytes(IReadOnlyList<T> chunk)
        {
            return Math.Max(1L, _estimateChunkBytes(chunk));
        }

        public long MaximumChunkBytes => _outputCredits.MaximumChunkBytes;

        public long AcquireCredit(long requestedBytes, CancellationToken cancellationToken)
        {
            return _outputCredits.Acquire(requestedBytes, cancellationToken);
        }

        public void ReleaseCredit(long creditedBytes)
        {
            _outputCredits.Release(creditedBytes);
        }

        public void Complete(Exception? exception = null)
        {
            Output.Writer.TryComplete(exception);
            _completion.TrySetResult(true);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Complete();
            while (Output.Reader.TryRead(out var queued))
                ReleaseCredit(queued.CreditBytes);
        }

        public void RecordDrainedRows(long rows)
        {
            _drainedRows = checked(_drainedRows + rows);
        }

        public long TakeDrainedRows()
        {
            var rows = _drainedRows;
            _drainedRows = 0;
            return rows;
        }

        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SearchOutputCreditPool _outputCredits;
        private readonly Func<IReadOnlyList<T>, long> _estimateChunkBytes;
        private long _drainedRows;
        private bool _disposed;
    }

    private sealed class ChannelChunkWriter<T>(
        WorkItem<T> item,
        CancellationToken cancellationToken) : IChunkWriter<T>
    {
        public CancellationToken CancellationToken => cancellationToken;

        public void Write(IReadOnlyList<T> chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            var offset = 0;
            while (offset < chunk.Count)
            {
                var count = chunk.Count - offset;
                var segment = Slice(chunk, offset, count);
                while (count > 1 && item.EstimateChunkBytes(segment) > item.MaximumChunkBytes)
                {
                    count = (count + 1) / 2;
                    segment = Slice(chunk, offset, count);
                }

                var requestedBytes = item.EstimateChunkBytes(segment);
                var creditedBytes = item.AcquireCredit(requestedBytes, cancellationToken);
                try
                {
                    item.Output.Writer
                        .WriteAsync(new QueuedChunk<T>(segment, creditedBytes), cancellationToken)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                }
                catch
                {
                    item.ReleaseCredit(creditedBytes);
                    throw;
                }

                offset = checked(offset + count);
            }
        }

        private static IReadOnlyList<T> Slice(
            IReadOnlyList<T> source,
            int offset,
            int count)
        {
            if (offset == 0 && count == source.Count)
                return source;

            var result = new T[count];
            for (var index = 0; index < count; index++)
                result[index] = source[offset + index];

            return result;
        }
    }
}
