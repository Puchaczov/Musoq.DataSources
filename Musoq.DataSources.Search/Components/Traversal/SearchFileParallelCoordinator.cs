#nullable enable

using System;
using System.Collections.Generic;
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
    private const int MaximumDefaultWorkerCount = 4;

    public SearchFileParallelOptions(
        int workerCount,
        int maxInFlightFiles,
        int bufferedChunksPerFile = 2,
        int progressReportInterval = RowChunking.DefaultChunkSize,
        SearchFileOutputOrder outputOrder = SearchFileOutputOrder.FileEnumerationOrder)
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

        WorkerCount = workerCount;
        MaxInFlightFiles = maxInFlightFiles;
        BufferedChunksPerFile = bufferedChunksPerFile;
        ProgressReportInterval = progressReportInterval;
        OutputOrder = outputOrder;
    }

    public static SearchFileParallelOptions Default { get; } = CreateDefault();

    public static SearchFileParallelOptions Sequential { get; } =
        new(workerCount: 1, maxInFlightFiles: 1, bufferedChunksPerFile: 1);

    public int WorkerCount { get; }

    public int MaxInFlightFiles { get; }

    public int BufferedChunksPerFile { get; }

    public int ProgressReportInterval { get; }

    public SearchFileOutputOrder OutputOrder { get; }

    private static SearchFileParallelOptions CreateDefault()
    {
        var workerCount = Math.Clamp(
            Environment.ProcessorCount,
            1,
            MaximumDefaultWorkerCount);
        return new(
            workerCount,
            checked(workerCount * 2),
            bufferedChunksPerFile: 2);
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
        SearchFileParallelOptions? options = null)
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

                if (active.Count >= options.MaxInFlightFiles)
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
                        FinishQuerySatisfied(stop, work, active, workers);
                        progress?.Flush();
                        firstFailure?.Throw();
                        return emittedRows;
                    }
                }

                var item = new WorkItem<T>(file, options.BufferedChunksPerFile);
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
                    FinishQuerySatisfied(stop, work, active, workers);
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

        var index = outputOrder == SearchFileOutputOrder.FileEnumerationOrder
            ? 0
            : FindCompletedItem(active, cancellationToken);
        var item = active[index];
        active.RemoveAt(index);
        return Drain(item, destination, acceptedWindow, progress, cancellationToken);
    }

    private static int FindCompletedItem<T>(
        List<WorkItem<T>> active,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < active.Count; index++)
        {
            if (active[index].CompletionTask.IsCompleted)
                return index;
        }

        var completions = new Task[active.Count];
        for (var index = 0; index < active.Count; index++)
            completions[index] = active[index].CompletionTask;

        WaitForAny(completions, cancellationToken);
        for (var index = 0; index < active.Count; index++)
        {
            if (active[index].CompletionTask.IsCompleted)
                return index;
        }

        throw new InvalidOperationException("A Search file completion was lost.");
    }

    private static void FinishQuerySatisfied<T>(
        CancellationTokenSource stop,
        Channel<WorkItem<T>> work,
        List<WorkItem<T>> active,
        Task[] workers)
    {
        stop.Cancel();
        work.Writer.TryComplete();
        WaitForWorkers(workers);
        CompleteOutstanding(work, active);
    }

    private static void RunWorker<T>(
        ChannelReader<WorkItem<T>> reader,
        Action<string, IChunkWriter<T>, CancellationToken> processFile,
        CancellationTokenSource stop,
        CancellationToken callerCancellationToken,
        ref ExceptionDispatchInfo? firstFailure)
    {
        try
        {
            while (reader.WaitToReadAsync(stop.Token).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var item))
                {
                    try
                    {
                        var writer = new ChannelChunkWriter<T>(
                            item.Output.Writer,
                            stop.Token);
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
                while (reader.TryRead(out var chunk))
                {
                    ArgumentNullException.ThrowIfNull(chunk);
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
            }

            WriteDestination(
                destination,
                stagedRows,
                cancellationToken,
                ref emittedRows);
            progress?.Add(emittedRows);
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

    private sealed class WorkItem<T> : IDisposable
    {
        public WorkItem(string filePath, int bufferedChunksPerFile)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Output = Channel.CreateBounded<IReadOnlyList<T>>(new BoundedChannelOptions(
                bufferedChunksPerFile)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
        }

        public string FilePath { get; }

        public Channel<IReadOnlyList<T>> Output { get; }

        public Task CompletionTask => _completion.Task;

        public void Complete(Exception? exception = null)
        {
            Output.Writer.TryComplete(exception);
            _completion.TrySetResult(true);
        }

        public void Dispose()
        {
            Complete();
        }

        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ChannelChunkWriter<T>(
        ChannelWriter<IReadOnlyList<T>> writer,
        CancellationToken cancellationToken) : IChunkWriter<T>
    {
        public CancellationToken CancellationToken => cancellationToken;

        public void Write(IReadOnlyList<T> chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            writer.WriteAsync(chunk, cancellationToken).AsTask().GetAwaiter().GetResult();
        }
    }
}
