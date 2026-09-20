#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public sealed record SearchRegexBenchmarkTrial(
    int Number,
    double DurationMilliseconds,
    long AllocatedBytes,
    int MatchCount,
    int CaptureCount,
    string ResultHash,
    bool Complete,
    string TerminalState);

public sealed record SearchRegexBenchmarkPhase(
    string Id,
    int WarmupsExcluded,
    IReadOnlyList<SearchRegexBenchmarkTrial> Trials,
    double MedianMilliseconds,
    double MedianAllocatedBytes,
    bool Passed);

public sealed record SearchRegexBenchmarkWorkload(
    string Id,
    string Pattern,
    int InputCharacters,
    int ExpectedMatches,
    int ExpectedCaptures,
    string ExpectedResultHash,
    IReadOnlyList<SearchRegexBenchmarkPhase> Phases,
    bool CorrectnessPassed);

public sealed record SearchRegexBenchmarkReport(
    string ScopeId,
    string Dialect,
    int TrialsPerPhase,
    string CachePolicy,
    string TimingBoundary,
    string SafetyThreshold,
    IReadOnlyList<SearchRegexBenchmarkWorkload> Workloads,
    bool Passed);

public static class SearchRegexAdversarialBenchmark
{
    private const int MeasuredTrials = 7;
    private const int DenseCapturePairs = 20_000;
    private const double MaximumMedianMilliseconds = 1_000;

    public static SearchRegexBenchmarkReport Measure()
    {
        var workloads = new[]
        {
            CreatePathologicalWorkload(),
            CreateDenseCaptureWorkload()
        };
        var reports = workloads
            .Select(MeasureWorkload)
            .ToArray();

        return new SearchRegexBenchmarkReport(
            ScopeId: "W07-S05",
            Dialect: SearchRegexBackend.Dialect,
            TrialsPerPhase: MeasuredTrials,
            CachePolicy: "cold-compilation resets the bounded cache before each measured invocation; warm-cache compiles once before each measured phase",
            TimingBoundary: "Stopwatch surrounds safe-profile regex compilation when cold, matching, capture enumeration and deterministic result hashing; fixture construction and JSON serialization are outside the timed boundary",
            SafetyThreshold: $"Every complete phase median must be below {MaximumMedianMilliseconds:0} milliseconds; this is a host-bound safety observation, not a superiority target.",
            Workloads: reports,
            Passed: reports.All(static report => report.CorrectnessPassed && report.Phases.All(static phase => phase.Passed)));
    }

    private static SearchRegexBenchmarkWorkload MeasureWorkload(RegexWorkload workload)
    {
        var phases = new[]
        {
            MeasurePhase(workload, "cold-compilation", resetBeforeInvocation: true, warmupsExcluded: 0),
            MeasurePhase(workload, "warm-cache", resetBeforeInvocation: false, warmupsExcluded: 1)
        };
        var correctnessPassed = phases
            .SelectMany(static phase => phase.Trials)
            .All(trial => trial.Complete &&
                          trial.TerminalState == "complete" &&
                          trial.MatchCount == workload.ExpectedMatches &&
                          trial.CaptureCount == workload.ExpectedCaptures &&
                          trial.ResultHash == workload.ExpectedResultHash);

        return new SearchRegexBenchmarkWorkload(
            workload.Id,
            workload.Pattern,
            workload.Input.Length,
            workload.ExpectedMatches,
            workload.ExpectedCaptures,
            workload.ExpectedResultHash,
            phases,
            correctnessPassed);
    }

    private static SearchRegexBenchmarkPhase MeasurePhase(
        RegexWorkload workload,
        string id,
        bool resetBeforeInvocation,
        int warmupsExcluded)
    {
        SearchRegexBackend.ResetForTests();
        if (!resetBeforeInvocation)
        {
            var warmup = Execute(workload, SearchRegexBackend.Compile(workload.Pattern));
            EnsureExpected(workload, warmup);
        }

        var trials = new List<SearchRegexBenchmarkTrial>(MeasuredTrials);
        for (var number = 1; number <= MeasuredTrials; number++)
        {
            if (resetBeforeInvocation)
                SearchRegexBackend.ResetForTests();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var result = Execute(
                workload,
                SearchRegexBackend.Compile(workload.Pattern));
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var complete = result.TerminalState == "complete";
            if (complete)
                EnsureExpected(workload, result);

            trials.Add(new SearchRegexBenchmarkTrial(
                number,
                duration,
                allocated,
                result.MatchCount,
                result.CaptureCount,
                result.ResultHash,
                complete,
                result.TerminalState));
        }

        var median = Median(trials.Select(static trial => trial.DurationMilliseconds));
        return new SearchRegexBenchmarkPhase(
            id,
            warmupsExcluded,
            trials,
            median,
            Median(trials.Select(static trial => (double)trial.AllocatedBytes)),
            trials.All(static trial => trial.Complete &&
                                       trial.TerminalState == "complete" &&
                                       trial.DurationMilliseconds > 0 &&
                                       trial.DurationMilliseconds < MaximumMedianMilliseconds));
    }

    private static RegexWorkload CreatePathologicalWorkload()
    {
        const string pattern = "^(a|aa)+$";
        var input = new string('a', 100_000) + "!";
        return new RegexWorkload(
            "pathological-nested-alternation",
            pattern,
            input,
            ExpectedMatches: 0,
            ExpectedCaptures: 0,
            ExpectedResultHash: Hash(string.Empty));
    }

    private static RegexWorkload CreateDenseCaptureWorkload()
    {
        const string pattern = "(?<letter>[A-Z])(?<digit>[0-9])";
        var input = string.Concat(Enumerable.Repeat("A1", DenseCapturePairs));
        var expected = Enumerable
            .Range(0, DenseCapturePairs)
            .Select(index => $"{index * 2}:2:A:1")
            .ToArray();
        return new RegexWorkload(
            "dense-capture-pairs",
            pattern,
            input,
            ExpectedMatches: DenseCapturePairs,
            ExpectedCaptures: DenseCapturePairs * 2,
            ExpectedResultHash: Hash(string.Join('\n', expected)));
    }

    private static RegexExecutionResult Execute(RegexWorkload workload, Regex regex)
    {
        var signatures = new List<string>();
        var matchCount = 0;
        var captureCount = 0;
        foreach (Match match in regex.Matches(workload.Input))
        {
            matchCount++;
            var captures = new List<string>();
            for (var groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
            {
                captures.Add(match.Groups[groupIndex].Value);
                captureCount++;
            }

            signatures.Add($"{match.Index}:{match.Length}:{string.Join(':', captures)}");
        }

        return new RegexExecutionResult(
            matchCount,
            captureCount,
            Hash(string.Join('\n', signatures)),
            "complete");
    }

    private static void EnsureExpected(
        RegexWorkload workload,
        RegexExecutionResult result)
    {
        if (result.MatchCount != workload.ExpectedMatches ||
            result.CaptureCount != workload.ExpectedCaptures ||
            result.ResultHash != workload.ExpectedResultHash)
        {
            throw new InvalidOperationException(
                $"{workload.Id} mismatch: expected {workload.ExpectedMatches} matches/{workload.ExpectedCaptures} captures/{workload.ExpectedResultHash}, got {result.MatchCount} matches/{result.CaptureCount} captures/{result.ResultHash}.");
        }
    }

    private static string Hash(string value)
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

    private sealed record RegexWorkload(
        string Id,
        string Pattern,
        string Input,
        int ExpectedMatches,
        int ExpectedCaptures,
        string ExpectedResultHash);

    private sealed record RegexExecutionResult(
        int MatchCount,
        int CaptureCount,
        string ResultHash,
        string TerminalState);
}
