using System.Text.Json;
using System.Text.Json.Serialization;

namespace Musoq.DataSources.SeparatedValues.Benchmark.Performance;

internal static class BenchmarkReportReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyDictionary<string, BenchmarkMetric> Read(string path)
    {
        return ReadReport(path).Metrics;
    }

    public static BenchmarkReportData ReadReport(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Benchmark report path cannot be empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Benchmark report was not found.", path);

        using var stream = File.OpenRead(path);
        var report = JsonSerializer.Deserialize<BenchmarkReport>(stream, SerializerOptions)
                     ?? throw new InvalidDataException($"Benchmark report '{path}' is empty.");

        if (report.Benchmarks is not { Count: > 0 })
            throw new InvalidDataException($"Benchmark report '{path}' contains no benchmarks.");

        var metrics = new Dictionary<string, BenchmarkMetric>(StringComparer.Ordinal);
        var jobFingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var benchmark in report.Benchmarks)
        {
            var benchmarkName = string.IsNullOrWhiteSpace(benchmark.FullName)
                ? benchmark.Method
                : benchmark.FullName;
            if (string.IsNullOrWhiteSpace(benchmarkName))
                throw new InvalidDataException($"Benchmark report '{path}' contains a benchmark without a method name.");

            if (benchmark.Statistics is null ||
                !double.IsFinite(benchmark.Statistics.Mean) ||
                benchmark.Statistics.Mean <= 0)
            {
                throw new InvalidDataException(
                    $"Benchmark '{benchmarkName}' in '{path}' has no valid timing statistics. " +
                    "The report is partial and cannot be used for a performance gate.");
            }

            var allocatedBytes = benchmark.Memory?.BytesAllocatedPerOperation;
            if (!allocatedBytes.HasValue ||
                !double.IsFinite(allocatedBytes.Value) ||
                allocatedBytes.Value < 0)
            {
                throw new InvalidDataException(
                    $"Benchmark '{benchmarkName}' in '{path}' has no valid allocation statistics. " +
                    "Run it with MemoryDiagnoser enabled.");
            }

            if (!metrics.TryAdd(
                    benchmarkName,
                    new BenchmarkMetric(benchmark.Statistics.Mean, allocatedBytes.Value)))
            {
                throw new InvalidDataException(
                    $"Benchmark report '{path}' contains duplicate method '{benchmarkName}'.");
            }

            var benchmarkJob = GetJobFingerprint(benchmark.DisplayInfo, benchmarkName, path);
            jobFingerprints.Add(benchmarkJob);
        }

        var environment = report.HostEnvironmentInfo
                          ?? throw new InvalidDataException(
                              $"Benchmark report '{path}' has no host environment metadata.");
        return new BenchmarkReportData(
            metrics,
            new BenchmarkEnvironmentFingerprint(
                environment.BenchmarkDotNetVersion ?? string.Empty,
                environment.OsVersion ?? string.Empty,
                environment.ProcessorName ?? string.Empty,
                environment.PhysicalCoreCount,
                environment.LogicalCoreCount,
                environment.RuntimeVersion ?? string.Empty,
                environment.Architecture ?? string.Empty,
                environment.Configuration ?? string.Empty,
                environment.DotNetCliVersion ?? string.Empty),
            jobFingerprints.Order(StringComparer.Ordinal).ToArray());
    }

    private static string GetJobFingerprint(string? displayInfo, string benchmarkName, string path)
    {
        if (string.IsNullOrWhiteSpace(displayInfo))
            throw new InvalidDataException($"Benchmark '{benchmarkName}' in '{path}' has no display metadata.");

        var colon = displayInfo.IndexOf(": ", StringComparison.Ordinal);
        var parameters = displayInfo.LastIndexOf(" [", StringComparison.Ordinal);
        if (colon < 0 || parameters <= colon + 2)
            throw new InvalidDataException(
                $"Benchmark '{benchmarkName}' in '{path}' has unrecognized job metadata '{displayInfo}'.");
        return displayInfo[(colon + 2)..parameters];
    }

    private sealed class BenchmarkReport
    {
        [JsonPropertyName("HostEnvironmentInfo")]
        public BenchmarkHostEnvironment? HostEnvironmentInfo { get; init; }

        [JsonPropertyName("Benchmarks")]
        public List<BenchmarkRecord>? Benchmarks { get; init; }
    }

    private sealed class BenchmarkRecord
    {
        [JsonPropertyName("DisplayInfo")]
        public string? DisplayInfo { get; init; }

        [JsonPropertyName("Method")]
        public string? Method { get; init; }

        [JsonPropertyName("FullName")]
        public string? FullName { get; init; }

        [JsonPropertyName("Statistics")]
        public BenchmarkStatistics? Statistics { get; init; }

        [JsonPropertyName("Memory")]
        public BenchmarkMemory? Memory { get; init; }
    }

    private sealed class BenchmarkStatistics
    {
        [JsonPropertyName("Mean")]
        public double Mean { get; init; }
    }

    private sealed class BenchmarkMemory
    {
        [JsonPropertyName("BytesAllocatedPerOperation")]
        public double? BytesAllocatedPerOperation { get; init; }
    }

    private sealed class BenchmarkHostEnvironment
    {
        public string? BenchmarkDotNetVersion { get; init; }
        public string? OsVersion { get; init; }
        public string? ProcessorName { get; init; }
        public int PhysicalCoreCount { get; init; }
        public int LogicalCoreCount { get; init; }
        public string? RuntimeVersion { get; init; }
        public string? Architecture { get; init; }
        public string? Configuration { get; init; }
        public string? DotNetCliVersion { get; init; }
    }
}

internal sealed record BenchmarkReportData(
    IReadOnlyDictionary<string, BenchmarkMetric> Metrics,
    BenchmarkEnvironmentFingerprint Environment,
    IReadOnlyList<string> JobFingerprints);

internal sealed record BenchmarkEnvironmentFingerprint(
    string BenchmarkDotNetVersion,
    string OsVersion,
    string ProcessorName,
    int PhysicalCoreCount,
    int LogicalCoreCount,
    string RuntimeVersion,
    string Architecture,
    string Configuration,
    string DotNetCliVersion);
