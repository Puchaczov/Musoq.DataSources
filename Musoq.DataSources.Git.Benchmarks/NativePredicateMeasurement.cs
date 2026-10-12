using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LibGit2Sharp;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Converter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Schema;

namespace Musoq.DataSources.Git.Benchmarks;

internal static class NativePredicateMeasurement
{
    internal static int Run(string output)
    {
        if (File.Exists(output)) throw new InvalidOperationException("Evidence already exists.");
        var corpus = GitBenchmarkCorpusFactory.Ensure(GitBenchmarkProfile.Smoke);
        using var repository = new Repository(corpus.RepositoryPath);
        var cutoff = repository.Commits.Select(static row => row.Committer.When).Order().ElementAt(200);
        var path = corpus.RepositoryPath.Replace('\\','/');
        var workloads = new[] {
            ("AuthorIn", $"select Sha from git.commits('{path}') where Author in ('Benchmark','nobody')", repository.Commits.Select(static row => row.Sha).ToArray()),
            ("DateAndAuthor", $"select Sha from git.commits('{path}') where Author = 'Benchmark' and CommittedWhen >= ToDateTimeOffset('{cutoff.ToString("O",CultureInfo.InvariantCulture)}')", repository.Commits.Where(row => row.Committer.When >= cutoff).Select(static row => row.Sha).ToArray()),
            ("BranchBoolean", $"select FriendlyName from git.branches('{path}') where IsRemote = false and FriendlyName <> 'nonexistent'", repository.Branches.Where(static row => !row.IsRemote).Select(static row => row.FriendlyName).ToArray()) };
        var results = new List<Measurement>();
        foreach (var (name, query, expected) in workloads)
        {
            var before = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
            using var compiled = InstanceCreatorHelpers.CompileForExecution(query, "GitMeasurement" + Guid.NewGuid().ToString("N"), new Provider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            var compilationMs = timer.Elapsed.TotalMilliseconds; var compilationBytes = GC.GetTotalAllocatedBytes(true)-before;
            using (var checkedTable = compiled.Run())
            {
                var actual = checkedTable.Select(static row => row.GetValue<string>(0)).Order(StringComparer.Ordinal).ToArray();
                if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal))) throw new InvalidOperationException("Independent Git expected rows differ.");
            }
            long Consume()
            {
                using var table = compiled.Run(); long count = 0, checksum = 0;
                foreach (var row in table) { count++; checksum += row.GetValue<string>(0).Length; }
                if (count != expected.Length) throw new InvalidOperationException("Git consumed count differs.");
                return checksum;
            }
            for (var warmup = 0; warmup < 100; warmup++) _ = Consume();
            var times = new List<double>(); var allocations = new List<long>();
            for (var iteration = 0; iteration < 20; iteration++)
            {
                before = GC.GetTotalAllocatedBytes(true); timer.Restart(); for (var invocation = 0; invocation < 50; invocation++) _ = Consume(); timer.Stop();
                times.Add(timer.Elapsed.TotalMilliseconds / 50); allocations.Add((GC.GetTotalAllocatedBytes(true)-before) / 50);
            }
            var sortedTimes = times.Order().ToArray(); var sortedBytes = allocations.Order().ToArray();
            results.Add(new(name, sortedTimes[10], sortedBytes[10], compilationMs, compilationBytes, times));
            Console.WriteLine($"{name}: {sortedTimes[10]:F4} ms; {sortedBytes[10]} bytes");
            File.WriteAllText(output + "." + name + ".cs", InstanceCreator.CompileForInspection(query, "GitInspection" + Guid.NewGuid().ToString("N"), new Provider(), new LoggerResolver()).GeneratedCSharpCode);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    private sealed class LoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;
        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }
    private sealed record Measurement(string Shape, double MedianMs, long MedianBytes, double CompilationMs, long CompilationBytes, List<double> SamplesMs);
    private sealed class Provider : ISchemaProvider { public ISchema GetSchema(string name) => new GitSchema(); }
}
