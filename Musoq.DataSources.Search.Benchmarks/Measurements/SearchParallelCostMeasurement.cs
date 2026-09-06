#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Musoq.Schema.DataSources;
using Musoq.DataSources.Search.Components.Traversal;

using Musoq.DataSources.Search.Benchmarks.Harness;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public static class SearchParallelCostMeasurement
{
    private const int MeasuredTrials = BenchmarkHarnessContract.MinimumMeasuredTrials;
    private const int FileCount = 48;
    private const int RowsPerFile = 128;
    private const int NoMatchFileCount = 256;

    public static object Run(int seed = 0x2A11C05)
    {
        var files = Enumerable.Range(0, FileCount)
            .Select(static index => $"file-{index:D3}")
            .ToArray();
        var random = new Random(seed);
        var modes = new[]
        {
            ParallelMode.Sequential,
            ParallelMode.ParallelFileOrder,
            ParallelMode.ParallelCompletionOrder
        };

        var warmup = ExecuteAll(files, modes, random);
        EnsureEquivalent("warmup", warmup);

        var trials = new List<MeasurementTrial>(MeasuredTrials);
        for (var number = 1; number <= MeasuredTrials; number++)
        {
            Shuffle(modes, random);
            var runs = ExecuteAll(files, modes, random);
            EnsureEquivalent($"trial {number}", runs);
            trials.Add(new MeasurementTrial(
                number,
                string.Join(',', modes.Select(static mode => mode.ToString())),
                runs[ParallelMode.Sequential],
                runs[ParallelMode.ParallelFileOrder],
                runs[ParallelMode.ParallelCompletionOrder]));
        }

        var noMatchFiles = Enumerable.Range(0, NoMatchFileCount)
            .Select(static index => $"empty-{index:D3}")
            .ToArray();
        var noMatch = ExecuteNoMatch(noMatchFiles);

        return new
        {
            command = "measure-parallel-cost",
            scopeId = "W11-S05",
            seed,
            resultUnit = "row",
            workload = new
            {
                id = "parallel-file-ordering",
                fileCount = FileCount,
                rowsPerFile = RowsPerFile,
                totalRows = FileCount * RowsPerFile,
                perFileWork = "deterministic bounded spin-wait plus one owned row chunk",
                outputIdentity = "file path plus row ordinal"
            },
            trialsPerCell = MeasuredTrials,
            warmupsExcluded = 1,
            timingBoundary = "Coordinator setup, worker scheduling, bounded per-file channel transport, destination drain, deterministic per-file work and output hash; JSON serialization and fixture construction excluded.",
            modes = new
            {
                sequential = Describe(warmup[ParallelMode.Sequential]),
                parallelFileOrder = Describe(warmup[ParallelMode.ParallelFileOrder]),
                parallelCompletionOrder = Describe(warmup[ParallelMode.ParallelCompletionOrder])
            },
            warmup = new
            {
                sequential = Describe(warmup[ParallelMode.Sequential]),
                parallelFileOrder = Describe(warmup[ParallelMode.ParallelFileOrder]),
                parallelCompletionOrder = Describe(warmup[ParallelMode.ParallelCompletionOrder])
            },
            trials = trials.Select(static trial => new
            {
                trial = trial.Number,
                firstInvocation = trial.FirstInvocation,
                sequential = Describe(trial.Sequential),
                parallelFileOrder = Describe(trial.ParallelFileOrder),
                parallelCompletionOrder = Describe(trial.ParallelCompletionOrder)
            }).ToArray(),
            summary = new
            {
                sequentialMedianMilliseconds = Median(trials.Select(static trial => trial.Sequential.DurationMilliseconds)),
                parallelFileOrderMedianMilliseconds = Median(trials.Select(static trial => trial.ParallelFileOrder.DurationMilliseconds)),
                parallelCompletionOrderMedianMilliseconds = Median(trials.Select(static trial => trial.ParallelCompletionOrder.DurationMilliseconds)),
                parallelFileOrderToSequentialMedianRatio = Ratio(
                    trials.Select(static trial => trial.ParallelFileOrder.DurationMilliseconds),
                    trials.Select(static trial => trial.Sequential.DurationMilliseconds)),
                parallelCompletionOrderToSequentialMedianRatio = Ratio(
                    trials.Select(static trial => trial.ParallelCompletionOrder.DurationMilliseconds),
                    trials.Select(static trial => trial.Sequential.DurationMilliseconds)),
                sortedResultMedianMilliseconds = Median(
                    trials.Select(static trial => trial.ParallelCompletionOrder.SortDurationMilliseconds)),
                sequentialMedianProgressReports = Median(
                    trials.Select(static trial => (double)trial.Sequential.ProgressReports)),
                parallelCompletionOrderMedianProgressReports = Median(
                    trials.Select(static trial => (double)trial.ParallelCompletionOrder.ProgressReports))
            },
            noMatchWorkload = new
            {
                fileCount = NoMatchFileCount,
                durationMilliseconds = noMatch.DurationMilliseconds,
                emittedRows = noMatch.EmittedRows,
                progressReports = noMatch.ProgressReports,
                complete = noMatch.EmittedRows == 0 && noMatch.ProgressReports == 0,
                terminalState = "complete"
            },
            equality = new
            {
                allRowCountsEqual = true,
                allMultisetHashesEqual = true,
                orderedModesHaveEqualOutput = true,
                completionOrderIsExplicit = true
            },
            decision = new
            {
                status = "observation",
                conclusion = "Parallel completion-order output is an explicit transport mode whose row multiset matches sequential and file-order modes. SQL ORDER BY remains the semantic ordering boundary; this measurement records its sorting cost separately and does not claim that completion order should replace the current file-order default."
            }
        };
    }

    private static Dictionary<ParallelMode, MeasurementRun> ExecuteAll(
        IReadOnlyList<string> files,
        IReadOnlyList<ParallelMode> modes,
        Random random)
    {
        var runs = new Dictionary<ParallelMode, MeasurementRun>();
        foreach (var mode in modes)
            runs.Add(mode, Execute(files, mode, random));
        return runs;
    }

    private static MeasurementRun Execute(
        IReadOnlyList<string> files,
        ParallelMode mode,
        Random random)
    {
        var writer = new RecordingChunkWriter<string>();
        var progressReports = 0;
        long reportedRows = 0;
        var options = CreateOptions(mode);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var emittedRows = SearchFileParallelCoordinator.Run(
            _ => files,
            writer,
            (file, fileWriter, cancellationToken) =>
            {
                var fileIndex = int.Parse(file.AsSpan(5));
                Thread.SpinWait(2_000 + ((files.Count - fileIndex) % 5) * 500);
                cancellationToken.ThrowIfCancellationRequested();

                var rows = new string[RowsPerFile];
                for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                    rows[rowIndex] = $"{file}:{rowIndex:D3}";
                fileWriter.Write(rows);
            },
            acceptedWindow: null,
            reportRowsRead: count =>
            {
                progressReports++;
                reportedRows = checked(reportedRows + count);
            },
            cancellationToken: default,
            options: options);
        var durationMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var outputHash = HashRows(writer.Rows);
        var sortedStarted = Stopwatch.GetTimestamp();
        var sortedRows = writer.Rows
            .OrderBy(static row => row, StringComparer.Ordinal)
            .ToArray();
        var sortDurationMilliseconds = Stopwatch.GetElapsedTime(sortedStarted).TotalMilliseconds;
        var sortedHash = HashRows(sortedRows);

        if (emittedRows != files.Count * RowsPerFile ||
            reportedRows != emittedRows ||
            writer.Rows.Count != emittedRows)
        {
            throw new InvalidOperationException(
                $"{mode} row accounting mismatch: emitted={emittedRows}, reported={reportedRows}, written={writer.Rows.Count}.");
        }

        return new MeasurementRun(
            durationMilliseconds,
            sortDurationMilliseconds,
            allocatedBytes,
            emittedRows,
            progressReports,
            outputHash,
            sortedHash,
            mode == ParallelMode.ParallelCompletionOrder &&
            !string.Equals(outputHash, sortedHash, StringComparison.Ordinal));
    }

    private static MeasurementRun ExecuteNoMatch(IReadOnlyList<string> files)
    {
        var writer = new RecordingChunkWriter<string>();
        var progressReports = 0;
        var started = Stopwatch.GetTimestamp();
        var emittedRows = SearchFileParallelCoordinator.Run(
            _ => files,
            writer,
            (_, _, cancellationToken) => cancellationToken.ThrowIfCancellationRequested(),
            acceptedWindow: null,
            reportRowsRead: _ => progressReports++,
            cancellationToken: default,
            options: new SearchFileParallelOptions(
                workerCount: 4,
                maxInFlightFiles: 8,
                bufferedChunksPerFile: 1,
                progressReportInterval: 1024,
                outputOrder: SearchFileOutputOrder.CompletionOrder));

        return new MeasurementRun(
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            0,
            0,
            emittedRows,
            progressReports,
            HashRows(writer.Rows),
            HashRows(writer.Rows),
            OutputOrderChanged: false);
    }

    private static SearchFileParallelOptions CreateOptions(ParallelMode mode)
    {
        return mode switch
        {
            ParallelMode.Sequential => SearchFileParallelOptions.Sequential,
            ParallelMode.ParallelFileOrder => new SearchFileParallelOptions(
                workerCount: 4,
                maxInFlightFiles: 8,
                bufferedChunksPerFile: 2,
                progressReportInterval: RowChunking.DefaultChunkSize,
                outputOrder: SearchFileOutputOrder.FileEnumerationOrder),
            ParallelMode.ParallelCompletionOrder => new SearchFileParallelOptions(
                workerCount: 4,
                maxInFlightFiles: 8,
                bufferedChunksPerFile: 2,
                progressReportInterval: RowChunking.DefaultChunkSize,
                outputOrder: SearchFileOutputOrder.CompletionOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static void EnsureEquivalent(
        string label,
        IReadOnlyDictionary<ParallelMode, MeasurementRun> runs)
    {
        var expectedCount = runs[ParallelMode.Sequential].EmittedRows;
        var expectedSortedHash = runs[ParallelMode.Sequential].SortedHash;
        foreach (var run in runs)
        {
            if (run.Value.EmittedRows != expectedCount ||
                !string.Equals(run.Value.SortedHash, expectedSortedHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} output mismatch for {run.Key}: {run.Value.EmittedRows}/{run.Value.SortedHash} versus {expectedCount}/{expectedSortedHash}.");
            }
        }

        if (!string.Equals(
                runs[ParallelMode.Sequential].OutputHash,
                runs[ParallelMode.ParallelFileOrder].OutputHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} file-order mismatch between sequential and parallel output.");
        }
    }

    private static void Shuffle(ParallelMode[] modes, Random random)
    {
        for (var index = modes.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (modes[index], modes[swapIndex]) = (modes[swapIndex], modes[index]);
        }
    }

    private static object Describe(MeasurementRun run)
    {
        return new
        {
            durationMilliseconds = run.DurationMilliseconds,
            sortDurationMilliseconds = run.SortDurationMilliseconds,
            allocatedBytesOnCoordinatorThread = run.AllocatedBytes,
            emittedRows = run.EmittedRows,
            progressReports = run.ProgressReports,
            outputHash = run.OutputHash,
            sortedHash = run.SortedHash,
            outputOrderChanged = run.OutputOrderChanged,
            complete = true,
            terminalState = "complete"
        };
    }

    private static double Ratio(
        IEnumerable<double> numerator,
        IEnumerable<double> denominator)
    {
        return Median(numerator) / Median(denominator);
    }

    private static string HashRows(IEnumerable<string> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
            builder.Append(row).Append('\n');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private enum ParallelMode
    {
        Sequential,
        ParallelFileOrder,
        ParallelCompletionOrder
    }

    private sealed record MeasurementRun(
        double DurationMilliseconds,
        double SortDurationMilliseconds,
        long AllocatedBytes,
        long EmittedRows,
        int ProgressReports,
        string OutputHash,
        string SortedHash,
        bool OutputOrderChanged);

    private sealed record MeasurementTrial(
        int Number,
        string FirstInvocation,
        MeasurementRun Sequential,
        MeasurementRun ParallelFileOrder,
        MeasurementRun ParallelCompletionOrder);

    private sealed class RecordingChunkWriter<T> : IChunkWriter<T>
    {
        public CancellationToken CancellationToken => default;

        public List<T> Rows { get; } = [];

        public void Write(IReadOnlyList<T> chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            Rows.AddRange(chunk);
        }
    }
}
