#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Benchmarks.Harness;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public static class LiteralAlgorithmMeasurement
{
    private const int MeasuredTrials = BenchmarkHarnessContract.MinimumMeasuredTrials;
    private const int IterationsPerTrial = 4;

    public static object Run(int seed = 0x11502001)
    {
        var random = new Random(seed);
        var workloads = CreateWorkloads();
        var measurements = new List<object>(workloads.Count);

        foreach (var workload in workloads)
        {
            var expected = RunReference(workload);
            var warmupBaseline = RunLegacy(workload);
            var warmupCandidate = RunCandidate(workload);
            AssertEquivalent(workload, "warmup baseline", expected, warmupBaseline);
            AssertEquivalent(workload, "warmup candidate", expected, warmupCandidate);

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
                    baseline = Measure(workload, RunLegacy);
                    candidate = Measure(workload, RunCandidate);
                }
                else
                {
                    candidate = Measure(workload, RunCandidate);
                    baseline = Measure(workload, RunLegacy);
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
                        ? "baseline,candidate"
                        : "candidate,baseline",
                    baselineMilliseconds = baseline.MillisecondsPerInvocation,
                    candidateMilliseconds = candidate.MillisecondsPerInvocation,
                    baselineAllocatedBytes = baseline.AllocatedBytesPerInvocation,
                    candidateAllocatedBytes = candidate.AllocatedBytesPerInvocation,
                    baselineResultCount = baseline.Result.Count,
                    candidateResultCount = candidate.Result.Count,
                    baselineResultHash = baseline.Result.Hash,
                    candidateResultHash = candidate.Result.Hash
                });
            }

            var baselineMedian = Median(baselineDurations);
            var candidateMedian = Median(candidateDurations);
            var baselineAllocatedBytesMedian = Median(baselineAllocations);
            var candidateAllocatedBytesMedian = Median(candidateAllocations);
            measurements.Add(new
            {
                id = workload.Id,
                literal = workload.Literal,
                eligibleBytes = Encoding.UTF8.GetByteCount(workload.Content),
                eligibleInputManifestHash = ComputeManifestHash(workload),
                expectedResultCount = expected.Count,
                expectedResultHash = expected.Hash,
                trials,
                baselineMedianMilliseconds = baselineMedian,
                candidateMedianMilliseconds = candidateMedian,
                candidateToBaselineMedianRatio = candidateMedian / baselineMedian,
                baselineAllocatedBytesMedian,
                candidateAllocatedBytesMedian,
                candidateToBaselineAllocatedBytesRatio = candidateAllocatedBytesMedian /
                    baselineAllocatedBytesMedian
            });
        }

        return new
        {
            command = "measure-literal",
            scopeId = "W11-S02",
            seed,
            trialPolicy = new
            {
                measuredTrials = MeasuredTrials,
                iterationsPerTrial = IterationsPerTrial,
                warmupsExcluded = 1,
                order = "randomized baseline/candidate invocation order per trial"
            },
            baseline = new
            {
                id = "pre-W11-S02-sensitive-loop",
                algorithm = "SearchValues first-character filter plus SequenceEqual verification, copied from the prior sensitive literal branch",
                status = "before"
            },
            candidate = new
            {
                id = "W11-S02-library-indexof",
                algorithm = "ReadOnlySpan<char>.IndexOf(char) for single-character literals; existing stateful scalar matcher for longer literals and block tails",
                status = "after"
            },
            featurePolicy = new
            {
                customSimd = false,
                scalarFallback = "The BCL span search primitive supplies the architecture-aware implementation and its scalar fallback.",
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                vectorHardwareAccelerated = System.Numerics.Vector.IsHardwareAccelerated
            },
            timingBoundary = "Stopwatch and GC.GetAllocatedBytesForCurrentThread surround matcher construction, the complete in-memory scan and result count/position-hash normalization per invocation. Traversal, decoding and external output serialization are intentionally excluded from this algorithm comparison.",
            workloads = measurements,
            interpretation = "The before/after comparison is a kernel observation, not a claim of universal end-to-end speedup. Every trial compares count and position hash with an independent String.IndexOf oracle."
        };
    }

    private static TimedRun Measure(
        Workload workload,
        Func<Workload, LiteralAlgorithmResult> algorithm)
    {
        var start = Stopwatch.GetTimestamp();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        LiteralAlgorithmResult? result = null;
        for (var iteration = 0; iteration < IterationsPerTrial; iteration++)
            result = algorithm(workload);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        return new(
            Stopwatch.GetElapsedTime(start).TotalMilliseconds / IterationsPerTrial,
            (double)allocatedBytes / IterationsPerTrial,
            result ?? throw new InvalidOperationException("The literal measurement did not run."));
    }

    private static LiteralAlgorithmResult RunCandidate(Workload workload)
    {
        return RunMatcher(
            workload,
            new LiteralMatcher(
                workload.Literal,
                SearchCaseMode.Sensitive,
                wholeWord: false));
    }

    private static LiteralAlgorithmResult RunLegacy(Workload workload)
    {
        return RunMatcher(
            workload,
            new LiteralMatcher(
                workload.Literal,
                SearchCaseMode.Sensitive,
                wholeWord: false,
                useSingleCharacterIndexOf: false));
    }

    private static LiteralAlgorithmResult RunMatcher(
        Workload workload,
        LiteralMatcher matcher)
    {
        var spans = new List<MatchSpan>();
        var lineNumber = 1L;
        var utf16Column = 0L;
        matcher.ConsumeBlock(
            workload.Content.AsSpan(),
            ref lineNumber,
            ref utf16Column,
            spans);
        matcher.Complete(spans);
        return new(spans.Count, HashPositions(spans.Select(static span => span.Start)));
    }

    private static LiteralAlgorithmResult RunReference(Workload workload)
    {
        var positions = new List<long>();
        var index = 0;
        while (index < workload.Content.Length)
        {
            var found = workload.Content.IndexOf(
                workload.Literal,
                index,
                StringComparison.Ordinal);
            if (found < 0)
                break;

            positions.Add(found);
            index = checked(found + workload.Literal.Length);
        }

        return new(positions.Count, HashPositions(positions));
    }

    private static IReadOnlyList<Workload> CreateWorkloads()
    {
        return
        [
            new(
                "single-character-sparse",
                "T",
                CreateContent(240_000, "T", markerEvery: 97)),
            new(
                "small-pattern-sparse",
                "TODO",
                CreateContent(240_000, "TODO", markerEvery: 97)),
            new(
                "small-pattern-dense",
                "TODO",
                string.Concat(Enumerable.Repeat("TODO ordinary payload\n", 240_000))),
            new(
                "long-pattern-absent",
                new string('Z', 31),
                CreateContent(240_000, "TODO", markerEvery: 97))
        ];
    }

    private static string CreateContent(
        int lineCount,
        string marker,
        int markerEvery)
    {
        var builder = new StringBuilder(lineCount * 48);
        for (var line = 0; line < lineCount; line++)
        {
            builder.Append("ordinary payload ")
                .Append(line.ToString("D5"));
            if (line % markerEvery == 0)
                builder.Append(' ').Append(marker);
            builder.Append(" 😀\n");
        }

        return builder.ToString();
    }

    private static string ComputeManifestHash(Workload workload)
    {
        var manifest = Encoding.UTF8.GetBytes(
            string.Join('\0', workload.Id, workload.Literal, workload.Content));
        return Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant();
    }

    private static string HashPositions(IEnumerable<long> positions)
    {
        var value = string.Join(',', positions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static void AssertEquivalent(
        Workload workload,
        string label,
        LiteralAlgorithmResult expected,
        LiteralAlgorithmResult actual)
    {
        if (expected.Count != actual.Count ||
            !string.Equals(expected.Hash, actual.Hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{workload.Id} {label} mismatch: expected {expected.Count}/{expected.Hash}, got {actual.Count}/{actual.Hash}.");
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

    private sealed record Workload(string Id, string Literal, string Content);

    private sealed record LiteralAlgorithmResult(int Count, string Hash);

    private sealed record TimedRun(
        double MillisecondsPerInvocation,
        double AllocatedBytesPerInvocation,
        LiteralAlgorithmResult Result);
}
