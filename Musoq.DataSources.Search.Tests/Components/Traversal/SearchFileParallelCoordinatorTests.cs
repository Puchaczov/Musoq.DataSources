#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchFileParallelCoordinatorTests
{
    [TestMethod]
    public void RuntimeSettings_ShouldUseDocumentedDefaultsAndRejectInvalidValues()
    {
        var defaults = SearchFileParallelOptions.FromRuntimeSettings(
            new Dictionary<string, string>());
        Assert.IsTrue(defaults.WorkerCount >= 1 && defaults.WorkerCount <= 8);
        Assert.AreEqual(32L * 1024 * 1024, defaults.BufferedOutputBytes);

        var explicitValues = SearchFileParallelOptions.FromRuntimeSettings(
            new Dictionary<string, string>
            {
                ["search.max_parallelism"] = "32",
                ["search.buffered_output_bytes"] = "65536"
            });
        Assert.AreEqual(32, explicitValues.WorkerCount);
        Assert.AreEqual(65536L, explicitValues.BufferedOutputBytes);

        Assert.ThrowsException<ArgumentException>(() =>
            SearchFileParallelOptions.FromRuntimeSettings(
                new Dictionary<string, string>
                {
                    ["search.max_parallelism"] = "33"
                }));
        Assert.ThrowsException<ArgumentException>(() =>
            SearchFileParallelOptions.FromRuntimeSettings(
                new Dictionary<string, string>
                {
                    ["search.buffered_output_bytes"] = "65535"
                }));
    }

    [TestMethod]
    public void OutputCredits_ShouldObserveCancellationWhileWaiting()
    {
        using var credits = new SearchOutputCreditPool(64 * 1024);
        var firstLease = credits.Acquire(64 * 1024, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = Task.Run(() =>
        {
            try
            {
                _ = credits.Acquire(64 * 1024, cancellation.Token);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        });

        Thread.SpinWait(10_000);
        cancellation.Cancel();
        Assert.IsTrue(waiting.GetAwaiter().GetResult());
        credits.Release(firstLease);
    }

    [TestMethod]
    public void OutputCredits_ShouldClampOneQueryToTheProcessWideAllowance()
    {
        using var credits = new SearchOutputCreditPool(512L * 1024 * 1024);

        Assert.AreEqual(256L * 1024 * 1024, credits.MaximumChunkBytes);
    }

    [TestMethod]
    public void ConcurrentCoordinators_ShouldRespectTheProcessWideWorkerLimit()
    {
        var activeWorkers = 0;
        var maximumActiveWorkers = 0;
        var options = new SearchFileParallelOptions(
            workerCount: 32,
            maxInFlightFiles: 64,
            bufferedChunksPerFile: 1);

        var runs = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() =>
                SearchFileParallelCoordinator.Run(
                    _ => Enumerable.Range(0, 64).Select(static index => $"file-{index}"),
                    new RecordingChunkWriter<int>(),
                    (_, fileWriter, cancellationToken) =>
                    {
                        var active = Interlocked.Increment(ref activeWorkers);
                        UpdateMaximum(ref maximumActiveWorkers, active);
                        try
                        {
                            Thread.Sleep(2);
                            cancellationToken.ThrowIfCancellationRequested();
                            fileWriter.Write([1]);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeWorkers);
                        }
                    },
                    acceptedWindow: null,
                    reportRowsRead: null,
                    cancellationToken: default,
                    options: options)))
            .ToArray();

        Task.WhenAll(runs).GetAwaiter().GetResult();

        Assert.IsTrue(maximumActiveWorkers <= 32, maximumActiveWorkers.ToString());
        Assert.AreEqual(0, Volatile.Read(ref activeWorkers));
    }

    [TestMethod]
    public void Coordinator_ShouldBoundWorkersAndPreserveFileOrderWithSlowConsumer()
    {
        var files = Enumerable.Range(0, 24)
            .Select(static index => $"file-{index:D2}")
            .ToArray();
        var options = new SearchFileParallelOptions(
            workerCount: 3,
            maxInFlightFiles: 6,
            bufferedChunksPerFile: 1);
        var writer = new RecordingChunkWriter<int>(delayMilliseconds: 1);
        var activeWorkers = 0;
        var maximumActiveWorkers = 0;

        var emitted = SearchFileParallelCoordinator.Run(
            _ => files,
            writer,
            (file, fileWriter, cancellationToken) =>
            {
                var active = Interlocked.Increment(ref activeWorkers);
                UpdateMaximum(ref maximumActiveWorkers, active);
                try
                {
                    var fileIndex = int.Parse(file.AsSpan(5));
                    for (var rowIndex = 0; rowIndex < 3; rowIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        fileWriter.Write([fileIndex * 3 + rowIndex]);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeWorkers);
                }
            },
            acceptedWindow: null,
            reportRowsRead: null,
            cancellationToken: default,
            options: options);

        Assert.AreEqual(72L, emitted);
        Assert.IsTrue(maximumActiveWorkers > 1);
        Assert.IsTrue(maximumActiveWorkers <= options.WorkerCount);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 72).ToArray(),
            writer.Rows.ToArray());
        Assert.AreEqual(0, Volatile.Read(ref activeWorkers));
    }

    [TestMethod]
    public void Coordinator_ShouldDrainUntilAnInFlightSlotIsReleasedWhenCreditsAreSmall()
    {
        using var cancellation = new CancellationTokenSource();
        var writer = new RecordingChunkWriter<int>(cancellation.Token);
        var options = new SearchFileParallelOptions(
            workerCount: 1,
            maxInFlightFiles: 2,
            bufferedChunksPerFile: 1,
            outputOrder: SearchFileOutputOrder.CompletionOrder,
            bufferedOutputBytes: 64 * 1024);

        var run = Task.Run(() => SearchFileParallelCoordinator.Run(
            _ => Enumerable.Range(0, 8).Select(static index => $"file-{index}"),
            writer,
            (_, fileWriter, token) =>
            {
                for (var index = 0; index < 4; index++)
                {
                    token.ThrowIfCancellationRequested();
                    fileWriter.Write([index]);
                }
            },
            acceptedWindow: null,
            reportRowsRead: null,
            cancellationToken: cancellation.Token,
            options: options,
            estimateChunkBytes: static _ => 64 * 1024));

        if (!run.Wait(TimeSpan.FromSeconds(5)))
        {
            cancellation.Cancel();
            Assert.Fail("The bounded coordinator did not release an in-flight slot.");
        }

        Assert.AreEqual(32L, run.GetAwaiter().GetResult());
    }

    [TestMethod]
    public void Coordinator_ShouldCancelBlockedWorkersAndReleaseAllHandles()
    {
        using var cancellation = new CancellationTokenSource();
        var writer = new RecordingChunkWriter<int>(
            cancellation.Token,
            afterWrite: cancellation.Cancel);
        var activeWorkers = 0;

        Assert.ThrowsException<OperationCanceledException>(() =>
            SearchFileParallelCoordinator.Run(
                _ => Enumerable.Range(0, 12).Select(static index => $"file-{index}"),
                writer,
                (_, fileWriter, cancellationToken) =>
                {
                    Interlocked.Increment(ref activeWorkers);
                    try
                    {
                        for (var rowIndex = 0; rowIndex < 100; rowIndex++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            fileWriter.Write([rowIndex]);
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref activeWorkers);
                    }
                },
                acceptedWindow: null,
                reportRowsRead: null,
                cancellationToken: cancellation.Token,
                options: new SearchFileParallelOptions(3, 6, 1)));

        Assert.AreEqual(0, Volatile.Read(ref activeWorkers));
        Assert.AreEqual(1, writer.WriteCount);
    }

    [TestMethod]
    public void Coordinator_ShouldRethrowTheFirstWorkerFailureAndReleaseAllHandles()
    {
        var writer = new RecordingChunkWriter<int>();
        var activeWorkers = 0;
        var failure = Assert.ThrowsException<InvalidOperationException>(() =>
            SearchFileParallelCoordinator.Run(
                _ => Enumerable.Range(0, 12).Select(static index => $"file-{index}"),
                writer,
                (file, fileWriter, cancellationToken) =>
                {
                    Interlocked.Increment(ref activeWorkers);
                    try
                    {
                        if (file == "file-3")
                            throw new InvalidOperationException("worker failure");

                        cancellationToken.ThrowIfCancellationRequested();
                        fileWriter.Write([int.Parse(file.AsSpan(5))]);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref activeWorkers);
                    }
                },
                acceptedWindow: null,
                reportRowsRead: null,
                cancellationToken: default,
                options: new SearchFileParallelOptions(3, 6, 1)));

        Assert.AreEqual("worker failure", failure.Message);
        Assert.AreEqual(0, Volatile.Read(ref activeWorkers));
    }

    [TestMethod]
    public void Coordinator_ShouldRepeatFailureCleanupWithoutLeakingWorkers()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var writer = new RecordingChunkWriter<int>();
            var activeWorkers = 0;

            Assert.ThrowsException<InvalidOperationException>(() =>
                SearchFileParallelCoordinator.Run(
                    _ => new[] { "first", "second", "third" },
                    writer,
                    (file, _, _) =>
                    {
                        Interlocked.Increment(ref activeWorkers);
                        try
                        {
                            if (file == "first")
                                throw new InvalidOperationException("repeatable failure");
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeWorkers);
                        }
                    },
                    acceptedWindow: null,
                    reportRowsRead: null,
                    cancellationToken: default,
                    options: new SearchFileParallelOptions(2, 4, 1)));

            Assert.AreEqual(0, Volatile.Read(ref activeWorkers));
        }
    }

    [TestMethod]
    public void CompletionOrder_ShouldPreserveTheSequentialRowMultiset()
    {
        var files = Enumerable.Range(0, 16)
            .Select(static index => $"file-{index:D2}")
            .ToArray();

        var sequential = RunRows(
            files,
            SearchFileParallelOptions.Sequential,
            delayFiles: true);
        var completionOrder = RunRows(
            files,
            new SearchFileParallelOptions(
                workerCount: 4,
                maxInFlightFiles: 8,
                bufferedChunksPerFile: 1,
                outputOrder: SearchFileOutputOrder.CompletionOrder),
            delayFiles: true);

        CollectionAssert.AreEquivalent(sequential, completionOrder);
    }

    [TestMethod]
    public void Progress_ShouldAggregateRowsAndIgnoreNoMatchFiles()
    {
        var reported = new List<long>();
        var files = Enumerable.Range(0, 128)
            .Select(static index => $"file-{index:D3}")
            .ToArray();

        var emitted = SearchFileParallelCoordinator.Run(
            _ => files,
            new RecordingChunkWriter<int>(),
            (_, _, _) => { },
            acceptedWindow: null,
            reportRowsRead: reported.Add,
            cancellationToken: default,
            options: new SearchFileParallelOptions(
                workerCount: 4,
                maxInFlightFiles: 8,
                bufferedChunksPerFile: 1,
                progressReportInterval: 16));

        Assert.AreEqual(0L, emitted);
        Assert.AreEqual(0, reported.Count);

        reported.Clear();
        emitted = SearchFileParallelCoordinator.Run(
            _ => files,
            new RecordingChunkWriter<int>(),
            (file, writer, cancellationToken) =>
            {
                var fileIndex = int.Parse(file.AsSpan(5));
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write([fileIndex]);
            },
            acceptedWindow: null,
            reportRowsRead: reported.Add,
            cancellationToken: default,
            options: new SearchFileParallelOptions(
                workerCount: 4,
                maxInFlightFiles: 8,
                bufferedChunksPerFile: 1,
                progressReportInterval: 16));

        Assert.AreEqual(128L, emitted);
        Assert.AreEqual(8, reported.Count);
        Assert.IsTrue(reported.All(static count => count == 16));
        Assert.AreEqual(128L, reported.Sum());
    }

    private static int[] RunRows(
        IReadOnlyList<string> files,
        SearchFileParallelOptions options,
        bool delayFiles)
    {
        var writer = new RecordingChunkWriter<int>();
        SearchFileParallelCoordinator.Run(
            _ => files,
            writer,
            (file, fileWriter, cancellationToken) =>
            {
                var fileIndex = int.Parse(file.AsSpan(5));
                if (delayFiles)
                    Thread.Sleep((files.Count - fileIndex) % 4);

                cancellationToken.ThrowIfCancellationRequested();
                fileWriter.Write([fileIndex * 2, fileIndex * 2 + 1]);
            },
            acceptedWindow: null,
            reportRowsRead: null,
            cancellationToken: default,
            options: options);

        return writer.Rows.ToArray();
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        while (true)
        {
            var observed = Volatile.Read(ref maximum);
            if (candidate <= observed)
                return;

            if (Interlocked.CompareExchange(ref maximum, candidate, observed) == observed)
                return;
        }
    }

    private sealed class RecordingChunkWriter<T> : IChunkWriter<T>
    {
        private readonly int _delayMilliseconds;
        private readonly Action? _afterWrite;

        public RecordingChunkWriter(
            CancellationToken cancellationToken = default,
            int delayMilliseconds = 0,
            Action? afterWrite = null)
        {
            CancellationToken = cancellationToken;
            _delayMilliseconds = delayMilliseconds;
            _afterWrite = afterWrite;
        }

        public CancellationToken CancellationToken { get; }

        public List<T> Rows { get; } = [];

        public int WriteCount { get; private set; }

        public void Write(IReadOnlyList<T> chunk)
        {
            CancellationToken.ThrowIfCancellationRequested();
            Rows.AddRange(chunk);
            WriteCount++;
            if (_delayMilliseconds > 0)
                Thread.Sleep(_delayMilliseconds);
            _afterWrite?.Invoke();
        }
    }
}
