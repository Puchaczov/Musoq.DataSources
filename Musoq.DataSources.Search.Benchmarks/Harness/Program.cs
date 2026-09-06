#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Musoq.DataSources.Search.Benchmarks.Comparisons;
using Musoq.DataSources.Search.Benchmarks.Measurements;

namespace Musoq.DataSources.Search.Benchmarks.Harness;

internal static class Program
{
    private const int MeasuredTrials = 7;
    private const int Seed = 0x5EA4C004;

    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "measure-decoding" => MeasureDecoding(),
                "measure-regex" => MeasureRegex(),
                "measure-literal" => MeasureLiteral(),
                "measure-io-backends" => MeasureIoBackends(),
                "measure-match-batches" => MeasureMatchBatches(),
                "measure-parallel-cost" => MeasureParallelCost(),
                "measure-delivery-latency" => MeasureDeliveryLatency(),
                "measure-resource-stability" => MeasureResourceStability(),
                "verify-byte-pipelines" => VerifyBytePipelines(),
                "measure-byte-pipelines" => MeasureBytePipelines(),
                _ => Usage()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    private static int MeasureDecoding()
    {
        var cohorts = new List<object>();
        foreach (var cohort in SearchDecodingCorpus.Cohorts)
        {
            using var corpus = SearchDecodingCorpus.Create(cohort);
            var expected = corpus.ExpectedMatches
                .Select(SearchSpikeResultFormatter.Signature)
                .ToArray();

            var warmup = ManagedSearchRunner.Run(corpus.Root, corpus.PatternSet);
            AssertEquivalent($"{cohort.Id} warmup", expected, warmup.Matches);

            var trials = new List<object>(MeasuredTrials);
            var durations = new List<double>(MeasuredTrials);
            for (var trial = 1; trial <= MeasuredTrials; trial++)
            {
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var workingSetBefore = GetWorkingSet();
                var start = Stopwatch.GetTimestamp();
                var result = ManagedSearchRunner.Run(corpus.Root, corpus.PatternSet);
                var elapsed = Stopwatch.GetElapsedTime(start);
                AssertEquivalent($"{cohort.Id} trial {trial}", expected, result.Matches);
                var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                var workingSetBytes = Math.Max(workingSetBefore, GetWorkingSet());
                durations.Add(elapsed.TotalMilliseconds);
                trials.Add(new
                {
                    trial,
                    durationMilliseconds = elapsed.TotalMilliseconds,
                    allocatedBytes,
                    workingSetBytes,
                    filesVisited = result.FilesVisited,
                    bytesRead = result.BytesRead,
                    outputBytes = result.OutputBytes,
                    outputHash = result.OutputHash,
                    complete = result.ExitCode is null,
                    stageTimings = result.StageTimings
                });
            }

            cohorts.Add(new
            {
                id = cohort.Id,
                encoding = cohort.EncodingName,
                contentProfile = cohort.ContentProfile,
                fixtureDigest = corpus.FixtureDigest,
                eligibleBytes = corpus.EligibleBytes,
                expectedOccurrences = expected.Length,
                warmup = new
                {
                    complete = warmup.ExitCode is null,
                    resultHash = warmup.OutputHash,
                    resultCount = warmup.Matches.Count
                },
                trials,
                medianMilliseconds = Median(durations)
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                command = "measure-decoding",
                seed = Seed,
                trialPolicy = new
                {
                    measuredTrials = MeasuredTrials,
                    warmupsExcluded = 1,
                    order = "single managed candidate; no external baseline"
                },
                cachePolicy = "filesystem cache is not flushed or asserted on this unprivileged Windows host; state is unknown",
                timingBoundary = "Total Stopwatch surrounds the complete managed in-process runner, including traversal, file open/read, decoding, matching and result normalization/hash.",
                interpretation = "Diagnostic decoding observations only; no cross-backend or superiority claim is made.",
                environment = DescribeEnvironment(),
                cohorts
            },
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureRegex()
    {
        var report = SearchRegexAdversarialBenchmark.Measure();
        Console.WriteLine(JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions { WriteIndented = true }));
        return report.Passed ? 0 : 2;
    }

    private static int MeasureLiteral()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            LiteralAlgorithmMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureIoBackends()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SearchIoBackendMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureMatchBatches()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SearchMatchBatchMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureParallelCost()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SearchParallelCostMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureDeliveryLatency()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SearchDeliveryLatencyMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureResourceStability()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SearchResourceStabilityMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int VerifyBytePipelines()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            new
            {
                command = "verify-byte-pipelines",
                scopeId = "W13-S05",
                verification = SearchBytePipelineMeasurement.Verify()
            },
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int MeasureBytePipelines()
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SearchBytePipelineMeasurement.Run(),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static object DescribeEnvironment()
    {
        return new
        {
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount,
            dotnet = Environment.Version.ToString()
        };
    }

    private static long GetWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        return process.WorkingSet64;
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        if (ordered.Length == 0)
            return 0;

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static void AssertEquivalent(
        string name,
        IReadOnlyList<string> expected,
        IReadOnlyList<SearchSpikeMatch> actual)
    {
        var actualSignatures = actual
            .Select(SearchSpikeResultFormatter.Signature)
            .ToArray();
        if (!expected.SequenceEqual(actualSignatures, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{name} mismatch: expected {expected.Count} rows, got {actualSignatures.Length}.");
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: dotnet run --project Musoq.DataSources.Search.Benchmarks -- [measure-decoding|measure-regex|measure-literal|measure-io-backends|measure-match-batches|measure-parallel-cost|measure-delivery-latency|measure-resource-stability|verify-byte-pipelines|measure-byte-pipelines]");
        return 1;
    }
}
