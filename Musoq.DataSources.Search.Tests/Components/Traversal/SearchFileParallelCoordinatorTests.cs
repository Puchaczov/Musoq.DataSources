#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchFileParallelCoordinatorTests
{
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
