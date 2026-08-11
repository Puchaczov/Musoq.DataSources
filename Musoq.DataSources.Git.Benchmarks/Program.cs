using System.Diagnostics;
using System.Text.Json;
using BenchmarkDotNet.Running;

namespace Musoq.DataSources.Git.Benchmarks;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["smoke"])
            return Verify(GitBenchmarkProfile.Smoke, includeWithoutCommitGraph: false);

        if (args is ["verify"])
            return Verify(GitBenchmarkProfile.Verify, includeWithoutCommitGraph: true);

        if (args is ["scale"])
            return Verify(GitBenchmarkProfile.Reference, includeWithoutCommitGraph: true);

        if (TryParseProfileCommand(args, "profile-source", out var sourceProfile, out var sourceBackend))
        {
            var benchmark = CreateBenchmark(sourceProfile, sourceBackend);
            return Profile(
                $"git-filehistory-production-{sourceBackend}",
                sourceProfile,
                sourceBackend,
                benchmark.ProductionChecksum,
                () => benchmark.LastCombinedPeakWorkingSet);
        }

        if (TryParseProfileCommand(args, "profile-legacy", out var legacyProfile, out _))
        {
            var benchmark = CreateBenchmark(legacyProfile);
            return Profile(
                "git-filehistory-frozen-legacy",
                legacyProfile,
                "frozen-legacy",
                benchmark.FrozenLegacyChecksum,
                () => Process.GetCurrentProcess().PeakWorkingSet64);
        }

        if (TryDescribeExternalRepository(args, out var repositoryPath))
        {
            Console.WriteLine($"repository={repositoryPath}");
            Console.WriteLine($"git={RunGitVersion(repositoryPath)}");
            return 0;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }

    private static int Verify(GitBenchmarkProfile profile, bool includeWithoutCommitGraph)
    {
        var graph = GitBenchmarkCorpusFactory.Ensure(profile, withCommitGraph: true);
        Console.WriteLine(GitBenchmarkCorpusFactory.Describe(graph));
        VerifyProductionChecksum(graph);

        if (includeWithoutCommitGraph)
        {
            var noGraph = GitBenchmarkCorpusFactory.Ensure(profile, withCommitGraph: false);
            Console.WriteLine(GitBenchmarkCorpusFactory.Describe(noGraph));
            VerifyProductionChecksum(noGraph);
        }

        return 0;
    }

    private static void VerifyProductionChecksum(GitBenchmarkCorpus corpus)
    {
        var benchmark = new GitFileHistoryBenchmarks
        {
            Profile = corpus.Profile,
            WithCommitGraph = corpus.HasCommitGraph
        };
        benchmark.Setup();
        var production = benchmark.ProductionChecksum();
        Console.WriteLine($"profile={corpus.Profile}; productionChecksum={production}");

        if (production == 0)
            throw new InvalidDataException("A benchmark checksum was unexpectedly zero.");
    }

    private static int Profile(
        string name,
        GitBenchmarkProfile profile,
        string backend,
        Func<long> operation,
        Func<long> combinedPeakWorkingSet)
    {
        // Keep the macro figures self-describing and redirectable as JSON. BenchmarkDotNet remains the microbenchmark
        // authority; this runner deliberately captures child-process memory and cold/warm lifecycle data it cannot.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var corpus = GitBenchmarkCorpusFactory.Ensure(profile);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var generation0Before = GC.CollectionCount(0);
        var generation1Before = GC.CollectionCount(1);
        var generation2Before = GC.CollectionCount(2);
        long peak = 0;

        var firstRun = Stopwatch.StartNew();
        var checksum = operation();
        firstRun.Stop();
        peak = Math.Max(peak, combinedPeakWorkingSet());

        var warmDurations = new List<TimeSpan>();
        var warmElapsed = Stopwatch.StartNew();
        do
        {
            var iteration = Stopwatch.StartNew();
            checksum = unchecked(checksum * 31 + operation());
            iteration.Stop();
            warmDurations.Add(iteration.Elapsed);
            peak = Math.Max(peak, combinedPeakWorkingSet());
        } while (warmElapsed.Elapsed < TimeSpan.FromSeconds(5));

        warmDurations.Sort();
        var result = new MacroProfileResult(
            name,
            profile.ToString(),
            backend,
            GitBenchmarkCorpusFactory.Describe(corpus),
            firstRun.Elapsed.TotalMilliseconds,
            warmDurations[warmDurations.Count / 2].TotalMilliseconds,
            warmDurations.Count,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore,
            GC.CollectionCount(0) - generation0Before,
            GC.CollectionCount(1) - generation1Before,
            GC.CollectionCount(2) - generation2Before,
            peak,
            checksum,
            Environment.Version.ToString(),
            Environment.OSVersion.VersionString,
            Environment.ProcessorCount);

        Console.WriteLine(JsonSerializer.Serialize(result));
        return 0;
    }

    private sealed record MacroProfileResult(
        string Name,
        string Profile,
        string Backend,
        string Corpus,
        double FirstRunMilliseconds,
        double WarmMedianMilliseconds,
        int WarmIterations,
        long AllocatedBytes,
        int Generation0Collections,
        int Generation1Collections,
        int Generation2Collections,
        long CombinedPeakWorkingSetBytes,
        long Checksum,
        string DotNetVersion,
        string OperatingSystem,
        int ProcessorCount);

    private static GitFileHistoryBenchmarks CreateBenchmark(GitBenchmarkProfile profile, string historyBackend = "auto")
    {
        var benchmark = new GitFileHistoryBenchmarks { Profile = profile, HistoryBackend = historyBackend };
        benchmark.SetupForMacro();
        return benchmark;
    }

    private static bool TryParseProfileCommand(
        string[] args,
        string command,
        out GitBenchmarkProfile profile,
        out string historyBackend)
    {
        profile = GitBenchmarkProfile.Smoke;
        historyBackend = "auto";
        if (args.Length == 1 && string.Equals(args[0], command, StringComparison.Ordinal))
            return true;
        if (args.Length is < 2 or > 3 || !string.Equals(args[0], command, StringComparison.Ordinal))
            return false;

        if (!Enum.TryParse(args[1], ignoreCase: true, out profile))
            throw new ArgumentException($"Unknown benchmark profile '{args[1]}'. Use smoke, verify, or reference.");

        if (args.Length == 3)
        {
            historyBackend = args[2].ToLowerInvariant();
            if (historyBackend is not ("auto" or "git-cli" or "libgit2"))
                throw new ArgumentException($"Unknown history backend '{args[2]}'. Use auto, git-cli, or libgit2.");
        }
        return true;
    }

    private static bool TryDescribeExternalRepository(string[] args, out string repositoryPath)
    {
        repositoryPath = string.Empty;
        if (args.Length != 2 || !string.Equals(args[0], "--repository", StringComparison.Ordinal))
            return false;

        repositoryPath = Path.GetFullPath(args[1]);
        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository path '{repositoryPath}' does not exist.");
        return true;
    }

    private static string RunGitVersion(string repositoryPath)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--version");
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output.Trim();
    }
}
