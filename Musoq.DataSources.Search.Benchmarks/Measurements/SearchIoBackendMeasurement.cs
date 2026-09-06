#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;

using Musoq.DataSources.Search.Benchmarks.Harness;
using Musoq.DataSources.Search.Benchmarks.Probes;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public static class SearchIoBackendMeasurement
{
    private const int MeasuredTrials = BenchmarkHarnessContract.MinimumMeasuredTrials;
    private const int IterationsPerTrial = 2;
    private const int LargeFileBytes = 16 * 1024 * 1024;

    public static object Run(int seed = 0x11503001)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-io-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var random = new Random(seed);
            var measurements = new List<object>();
            foreach (var workload in CreateWorkloads(root, seed))
            {
                var expected = SearchIoBackendProbe.Read(
                    workload.Path,
                    SearchIoBackend.BufferedSequential);
                AssertCompleted(workload, "reference", expected);

                var warmupBaseline = Measure(workload, SearchIoBackend.BufferedSequential);
                var warmupCandidate = Measure(workload, SearchIoBackend.MemoryMapped);
                AssertEquivalent(workload, "warmup baseline", expected, warmupBaseline.Result);
                AssertEquivalent(workload, "warmup candidate", expected, warmupCandidate.Result);

                var trials = new List<object>(MeasuredTrials);
                var baselineDurations = new List<double>(MeasuredTrials);
                var candidateDurations = new List<double>(MeasuredTrials);
                var baselineAllocations = new List<double>(MeasuredTrials);
                var candidateAllocations = new List<double>(MeasuredTrials);
                for (var trial = 1; trial <= MeasuredTrials; trial++)
                {
                    var baselineFirst = random.Next(2) == 0;
                    TimedRun baseline;
                    TimedRun candidate;
                    if (baselineFirst)
                    {
                        baseline = Measure(workload, SearchIoBackend.BufferedSequential);
                        candidate = Measure(workload, SearchIoBackend.MemoryMapped);
                    }
                    else
                    {
                        candidate = Measure(workload, SearchIoBackend.MemoryMapped);
                        baseline = Measure(workload, SearchIoBackend.BufferedSequential);
                    }

                    AssertEquivalent(workload, $"trial {trial} baseline", expected, baseline.Result);
                    AssertEquivalent(workload, $"trial {trial} candidate", expected, candidate.Result);
                    baselineDurations.Add(baseline.MillisecondsPerInvocation);
                    candidateDurations.Add(candidate.MillisecondsPerInvocation);
                    baselineAllocations.Add(baseline.AllocatedBytesPerInvocation);
                    candidateAllocations.Add(candidate.AllocatedBytesPerInvocation);
                    trials.Add(new
                    {
                        trial,
                        firstInvocation = baselineFirst
                            ? "buffered,memory-mapped"
                            : "memory-mapped,buffered",
                        baselineMilliseconds = baseline.MillisecondsPerInvocation,
                        candidateMilliseconds = candidate.MillisecondsPerInvocation,
                        baselineAllocatedBytes = baseline.AllocatedBytesPerInvocation,
                        candidateAllocatedBytes = candidate.AllocatedBytesPerInvocation,
                        baselineStatus = baseline.Result.Status.ToString(),
                        candidateStatus = candidate.Result.Status.ToString(),
                        baselineBytesRead = baseline.Result.BytesRead,
                        candidateBytesRead = candidate.Result.BytesRead,
                        baselineSha256 = baseline.Result.Sha256,
                        candidateSha256 = candidate.Result.Sha256
                    });
                }

                var baselineMedian = Median(baselineDurations);
                var candidateMedian = Median(candidateDurations);
                var baselineAllocatedMedian = Median(baselineAllocations);
                var candidateAllocatedMedian = Median(candidateAllocations);
                measurements.Add(new
                {
                    id = workload.Id,
                    description = workload.Description,
                    fileBytes = expected.BytesRead,
                    expectedSha256 = expected.Sha256,
                    trials,
                    baselineMedianMilliseconds = baselineMedian,
                    candidateMedianMilliseconds = candidateMedian,
                    candidateToBaselineMedianRatio = candidateMedian / baselineMedian,
                    baselineAllocatedBytesMedian = baselineAllocatedMedian,
                    candidateAllocatedBytesMedian = candidateAllocatedMedian,
                    candidateToBaselineAllocatedBytesRatio = candidateAllocatedMedian /
                        baselineAllocatedMedian
                });
            }

            return new
            {
                command = "measure-io-backends",
                scopeId = "W11-S03",
                seed,
                trialPolicy = new
                {
                    measuredTrials = MeasuredTrials,
                    iterationsPerTrial = IterationsPerTrial,
                    warmupsExcluded = 1,
                    order = "randomized buffered/memory-mapped invocation order per trial"
                },
                baseline = new
                {
                    id = "buffered-sequential",
                    algorithm = "FileStream with FileOptions.SequentialScan and a fixed byte buffer",
                    status = "production-baseline"
                },
                candidate = new
                {
                    id = "memory-mapped-view-stream",
                    algorithm = "Read-only MemoryMappedFile view stream with the same byte buffer",
                    status = "evaluation-only"
                },
                backendPolicy = new
                {
                    selectedProductionBackend = "buffered-sequential",
                    emptyFile = "completed without creating a zero-length mapping",
                    mutation = "a changed length or last-write timestamp is reported as MutationDetected; no partial result is accepted as stable",
                    mappingFailure = "reported as Failed without silently falling back to buffered I/O",
                    cancellation = "reported as Cancelled and never converted to a successful result"
                },
                featurePolicy = new
                {
                    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    osArchitecture = RuntimeInformation.OSArchitecture.ToString()
                },
                timingBoundary = "Stopwatch and GC.GetAllocatedBytesForCurrentThread surround file open, backend setup, complete raw-byte read and SHA-256 normalization per invocation. Search decoding, matching, traversal and output serialization are excluded.",
                workloads = measurements,
                decision = new
                {
                    status = "retain-buffered-baseline",
                    reason = "Memory mapping is not promoted by this isolated measurement. The current Search contract requires safe handling of changing files, truncation, decoding coordinates, cancellation and mapping failure; a view stream alone does not establish that contract."
                }
            };
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TimedRun Measure(
        Workload workload,
        SearchIoBackend backend)
    {
        var start = Stopwatch.GetTimestamp();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        SearchIoReadResult? result = null;
        for (var iteration = 0; iteration < IterationsPerTrial; iteration++)
            result = SearchIoBackendProbe.Read(workload.Path, backend);

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new(
            Stopwatch.GetElapsedTime(start).TotalMilliseconds / IterationsPerTrial,
            (double)allocatedBytes / IterationsPerTrial,
            result ?? throw new InvalidOperationException("The I/O measurement did not run."));
    }

    private static IReadOnlyList<Workload> CreateWorkloads(string root, int seed)
    {
        var tinyPath = Path.Combine(root, "tiny.bin");
        File.WriteAllBytes(tinyPath, [0x00, 0x01, 0x7f, 0x80, 0xff, 0x42, 0x00]);

        var largePath = Path.Combine(root, "large.bin");
        var large = new byte[LargeFileBytes];
        for (var index = 0; index < large.Length; index++)
            large[index] = unchecked((byte)(index * 31 + index / 17 + seed));
        File.WriteAllBytes(largePath, large);

        return
        [
            new("tiny", "A seven-byte file exercises setup and empty-adjacent behavior.", tinyPath),
            new("large", "A 16 MiB deterministic file exercises sustained sequential reads.", largePath)
        ];
    }

    private static void AssertCompleted(
        Workload workload,
        string label,
        SearchIoReadResult actual)
    {
        if (actual.Status != SearchIoReadStatus.Completed)
        {
            throw new InvalidOperationException(
                $"{workload.Id} {label} did not complete: {actual.Status} ({actual.ExceptionType ?? "no exception"}).");
        }
    }

    private static void AssertEquivalent(
        Workload workload,
        string label,
        SearchIoReadResult expected,
        SearchIoReadResult actual)
    {
        if (actual.Status != expected.Status ||
            actual.BytesRead != expected.BytesRead ||
            !string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{workload.Id} {label} mismatch: expected {expected.Status}/{expected.BytesRead}/{expected.Sha256}, got {actual.Status}/{actual.BytesRead}/{actual.Sha256}.");
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private sealed record Workload(string Id, string Description, string Path);

    private sealed record TimedRun(
        double MillisecondsPerInvocation,
        double AllocatedBytesPerInvocation,
        SearchIoReadResult Result);
}
