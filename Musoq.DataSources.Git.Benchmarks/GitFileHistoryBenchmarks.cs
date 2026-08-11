using System.Diagnostics;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public sealed class GitFileHistoryBenchmarks
{
    // This is the blocking compiled-query shape from the large-repository qualification gate.
    private const int Take = 5_000;
    private GitBenchmarkCorpus _corpus = null!;
    private SourceExecutionContext _context = null!;

    public long LastCombinedPeakWorkingSet { get; private set; }

    [Params(GitBenchmarkProfile.Smoke)]
    public GitBenchmarkProfile Profile { get; set; }

    [Params(true)]
    public bool WithCommitGraph { get; set; }

    [Params("auto", "git-cli", "libgit2")]
    public string HistoryBackend { get; set; } = "auto";

    [GlobalSetup]
    public void Setup() => Initialize(validateProductionChecksum: true);

    // The isolated macro runner must include the first real source enumeration in its first-run measurement.
    // BenchmarkDotNet setup still validates the fixture before it measures a benchmark method.
    internal void SetupForMacro() => Initialize(validateProductionChecksum: false);

    private void Initialize(bool validateProductionChecksum)
    {
        _corpus = GitBenchmarkCorpusFactory.Ensure(Profile, WithCommitGraph);
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: FileHistoryEntity.Columns,
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [GitHistoryBackendOptions.BackendSettingName] = HistoryBackend
            });

        if (validateProductionChecksum && ProductionChecksum() == 0)
            throw new InvalidOperationException("The benchmark fixture unexpectedly returned no file-history rows.");
    }

    [Benchmark(Baseline = true)]
    public long FrozenLegacyChecksum()
    {
        long checksum = 17;
        foreach (var row in FrozenLegacyFileHistoryReader.Read(_corpus.RepositoryPath, "*.cs", 0, Take))
            checksum = Fold(checksum, row.CommitSha, row.FilePath, row.ChangeType, row.OldPath);
        return checksum;
    }

    [Benchmark]
    public long ProductionChecksum()
    {
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        var schema = new GitSchema();
        var source = schema.GetRowSource<FileHistoryEntity>(
            "filehistory",
            _context,
            _corpus.RepositoryPath,
            "*.cs",
            Take);
        long checksum = 17;
        var rows = new List<FileHistoryEntity>(Take);

        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
        {
            checksum = Fold(checksum, row.CommitSha, row.FilePath, row.ChangeType, row.OldPath);
            rows.Add(row);
        }

        GitBenchmarkOracle.AssertExpectedRows(_corpus, rows, Math.Min(Take, GitBenchmarkOracle.ExpectedCsRowCount(_corpus)));

        // A sum of independently observed peaks is deliberately conservative; it cannot under-report the working-set
        // budget when the child overlaps with the plugin producer.
        LastCombinedPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 + GitCliProcessMetrics.PeakWorkingSet;
        return checksum;
    }

    internal static long Fold(long checksum, params string?[] values)
    {
        foreach (var value in values)
            checksum = unchecked(checksum * 31 + StableOrdinalHash(value));
        return checksum;
    }

    private static int StableOrdinalHash(string? value)
    {
        if (value is null)
            return 0;

        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return unchecked((int)hash);
    }
}
