#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Benchmarks.Harness;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public static class SearchMatchBatchMeasurement
{
    private const int MeasuredTrials = BenchmarkHarnessContract.MinimumMeasuredTrials;
    private const int ExpectedMatches = 50_000;

    public static object Run(int seed = 0x5EA4C004)
    {
        var content = string.Concat(Enumerable.Repeat("TODO ", ExpectedMatches));
        var random = new Random(seed);
        var warmupPerMatch = Execute(content, batched: false);
        var warmupBatched = Execute(content, batched: true);
        EnsureEquivalent("warmup", warmupPerMatch, warmupBatched);

        var trials = new List<MeasurementTrial>(MeasuredTrials);
        for (var number = 1; number <= MeasuredTrials; number++)
        {
            var perMatchFirst = random.Next(2) == 0;
            MeasurementRun perMatch;
            MeasurementRun batched;
            if (perMatchFirst)
            {
                perMatch = Execute(content, batched: false);
                batched = Execute(content, batched: true);
            }
            else
            {
                batched = Execute(content, batched: true);
                perMatch = Execute(content, batched: false);
            }
            EnsureEquivalent($"trial {number}", perMatch, batched);
            trials.Add(new MeasurementTrial(
                number,
                perMatchFirst ? "per-match,batched" : "batched,per-match",
                perMatch,
                batched));
        }

        return new
        {
            command = "measure-match-batches",
            scopeId = "W11-S04",
            seed,
            resultUnit = "occurrence",
            workload = new
            {
                id = "dense-literal-hits",
                literal = "TODO",
                expectedMatches = ExpectedMatches,
                inputCharacters = content.Length,
                inputSha256 = HashText(content)
            },
            trialsPerCell = MeasuredTrials,
            warmupsExcluded = 1,
            baseline = new
            {
                id = "managed-per-match-sink",
                description = "The scanner batch boundary uses the compatibility path, invoking the sink's single-match operation for every span."
            },
            candidate = new
            {
                id = "managed-batched-sink",
                description = "The scanner batch boundary invokes one sink operation per matcher output batch; the sink copies spans synchronously."
            },
            timingBoundary = "File-like StringReader setup, pooled-buffer scan, literal matching, sink materialization, cancellation checks and deterministic occurrence hashing; fixture construction and JSON serialization excluded.",
            warmup = new
            {
                baseline = Describe(warmupPerMatch),
                candidate = Describe(warmupBatched)
            },
            trials = trials.Select(static trial => new
            {
                trial = trial.Number,
                firstInvocation = trial.FirstInvocation,
                baseline = Describe(trial.PerMatch),
                candidate = Describe(trial.Batched)
            }).ToArray(),
            summary = new
            {
                baselineMedianMilliseconds = Median(trials.Select(static trial => trial.PerMatch.DurationMilliseconds)),
                candidateMedianMilliseconds = Median(trials.Select(static trial => trial.Batched.DurationMilliseconds)),
                candidateToBaselineMedianRatio = Median(trials.Select(static trial => trial.Batched.DurationMilliseconds)) /
                    Median(trials.Select(static trial => trial.PerMatch.DurationMilliseconds)),
                baselineMedianManagedCalls = Median(trials.Select(static trial => (double)trial.PerMatch.SingleMatchCalls)),
                candidateMedianBatchCalls = Median(trials.Select(static trial => (double)trial.Batched.BatchCalls)),
                baselineMedianAllocatedBytes = Median(trials.Select(static trial => (double)trial.PerMatch.AllocatedBytes)),
                candidateMedianAllocatedBytes = Median(trials.Select(static trial => (double)trial.Batched.AllocatedBytes))
            },
            decision = new
            {
                status = "observation",
                conclusion = "Batch delivery preserves occurrence output and reduces the managed sink operation from one call per hit to one operation per populated matcher batch. This is a managed seam measurement; no native backend is claimed or introduced."
            }
        };
    }

    private static MeasurementRun Execute(string content, bool batched)
    {
        var sink = batched
            ? (BatchMeasurementSink)new BatchedMeasurementSink()
            : new PerMatchMeasurementSink();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();

        using var buffer = SearchCharBuffer.Rent();
        SearchTextScanner.ScanFile(
            new LiteralMatcher("TODO"),
            "dense-batch.txt",
            buffer,
            [],
            CancellationToken.None,
            _ => new StringReader(content),
            sink);

        var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var resultHash = HashMatches(sink.Matches);
        if (sink.Matches.Count != ExpectedMatches)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedMatches} matches, got {sink.Matches.Count}.");
        }

        return new MeasurementRun(
            duration,
            allocatedBytes,
            sink.Matches.Count,
            sink.SingleMatchCalls,
            sink.BatchCalls,
            resultHash);
    }

    private static void EnsureEquivalent(
        string label,
        MeasurementRun baseline,
        MeasurementRun candidate)
    {
        if (baseline.MatchCount != candidate.MatchCount ||
            !string.Equals(baseline.ResultHash, candidate.ResultHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} output mismatch: baseline {baseline.MatchCount}/{baseline.ResultHash}, candidate {candidate.MatchCount}/{candidate.ResultHash}.");
        }
    }

    private static object Describe(MeasurementRun run)
    {
        return new
        {
            durationMilliseconds = run.DurationMilliseconds,
            allocatedBytes = run.AllocatedBytes,
            matchCount = run.MatchCount,
            singleMatchCalls = run.SingleMatchCalls,
            batchCalls = run.BatchCalls,
            resultHash = run.ResultHash,
            complete = true,
            terminalState = "complete"
        };
    }

    private static string HashMatches(IReadOnlyList<MatchSpan> matches)
    {
        var builder = new StringBuilder(matches.Count * 16);
        foreach (var match in matches)
        {
            builder.Append(match.Start)
                .Append(':')
                .Append(match.Length)
                .Append(':')
                .Append(match.LineNumber)
                .Append(':')
                .Append(match.Utf16Column)
                .Append('\n');
        }

        return HashText(builder.ToString());
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
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

    private abstract class BatchMeasurementSink : ISearchTextScanSink
    {
        public List<MatchSpan> Matches { get; } = [];

        public int SingleMatchCalls { get; protected set; }

        public int BatchCalls { get; protected set; }

        public bool NeedsLineText => false;

        public bool NeedsMatchText => false;

        public bool NeedsCaptures => false;

        public bool NeedsLineCompletion => false;

        public long RowsEmitted => Matches.Count;

        public abstract bool AcceptMatch(MatchSpan span);

        public abstract bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default);

        public bool HasMatchesOnLine(long lineNumber) => false;

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
        }
    }

    private sealed class PerMatchMeasurementSink : BatchMeasurementSink
    {
        public override bool AcceptMatch(MatchSpan span)
        {
            SingleMatchCalls++;
            Matches.Add(span);
            return false;
        }

        public override bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(spans);
            BatchCalls++;
            for (var index = 0; index < spans.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (AcceptMatch(spans[index]))
                    return true;
            }

            return false;
        }
    }

    private sealed class BatchedMeasurementSink : BatchMeasurementSink
    {
        public override bool AcceptMatch(MatchSpan span)
        {
            SingleMatchCalls++;
            Matches.Add(span);
            return false;
        }

        public override bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(spans);
            cancellationToken.ThrowIfCancellationRequested();
            BatchCalls++;
            for (var index = 0; index < spans.Count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                Matches.Add(spans[index]);
            }

            return false;
        }
    }

    private sealed record MeasurementRun(
        double DurationMilliseconds,
        long AllocatedBytes,
        int MatchCount,
        int SingleMatchCalls,
        int BatchCalls,
        string ResultHash);

    private sealed record MeasurementTrial(
        int Number,
        string FirstInvocation,
        MeasurementRun PerMatch,
        MeasurementRun Batched);
}
