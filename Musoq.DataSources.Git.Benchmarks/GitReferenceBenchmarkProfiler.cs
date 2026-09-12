using System.Diagnostics;
using System.Text.Json;
using Musoq.DataSources.Git;

namespace Musoq.DataSources.Git.Benchmarks;

internal static class GitReferenceBenchmarkProfiler
{
    public static int Run(GitReferenceCorpusProfile profile)
    {
        var results = new List<ReferenceProfileResult>();
        foreach (var packed in new[] { false, true })
        {
            var corpus = GitReferenceBenchmarkCorpusFactory.Ensure(profile, packed);
            var gitVersion = ReadGitVersion(corpus.RepositoryPath);

            foreach (var backend in new[] { "auto", "git-cli", "libgit2" })
            {
                var local = new GitReferenceSourceBenchmarks
                {
                    Profile = profile,
                    Packed = packed,
                    Backend = backend
                };
                local.Setup();
                Add(results, corpus, gitVersion, packed, backend, "tags.identity", "identity", local.DirectTags,
                    () => local.LastMetrics);
                Add(results, corpus, gitVersion, packed, backend, "tags.zero-column", "zero-column", local.DirectTagsCountZeroProjection,
                    () => local.LastMetrics);
                Add(results, corpus, gitVersion, packed, backend, "tags.exact", "identity", local.DirectTagsExactPredicate,
                    () => local.LastMetrics);
                Add(results, corpus, gitVersion, packed, backend, "tags.skip-take", "identity", local.DirectTagsSkipTake,
                    () => local.LastMetrics);
                Add(results, corpus, gitVersion, packed, backend, "stashes.identity", "identity", local.DirectStashes,
                    () => local.LastMetrics);
                Add(results, corpus, gitVersion, packed, backend, "stashes.zero-column", "zero-column", local.DirectStashesCountZeroProjection,
                    () => local.LastMetrics);
                Add(results, corpus, gitVersion, packed, backend, "stashes.selector", "identity", local.DirectStashExactSelector,
                    () => local.LastMetrics);
            }

            var remote = new GitRemoteTagSourceBenchmarks
            {
                Profile = profile,
                Packed = packed
            };
            remote.Setup();
            Add(results, corpus, gitVersion, packed, "git-cli", "remote-tags.lean", "lean", remote.DirectRemoteTagsCountZeroProjection,
                () => remote.LastMetrics);
            Add(results, corpus, gitVersion, packed, "git-cli", "remote-tags.peel", "peel-aware", remote.DirectRemoteTags,
                () => remote.LastMetrics);
            Add(results, corpus, gitVersion, packed, "git-cli", "remote-tags.exact", "lean", remote.DirectRemoteTagsExactPredicate,
                () => remote.LastMetrics);
            Add(results, corpus, gitVersion, packed, "git-cli", "remote-tags.cursor", "lean", remote.DirectRemoteTagsCursorPredicate,
                () => remote.LastMetrics);
            Add(results, corpus, gitVersion, packed, "git-cli", "remote-tags.skip-take", "lean", remote.DirectRemoteTagsSkipTake,
                () => remote.LastMetrics);
        }

        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
        return 0;
    }

    private static void Add(
        ICollection<ReferenceProfileResult> results,
        GitReferenceBenchmarkCorpus corpus,
        string gitVersion,
        bool packed,
        string backend,
        string scenario,
        string projectionMode,
        Func<long> operation,
        Func<IReadOnlyDictionary<string, long>> sourceMetrics)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var generation0Before = GC.CollectionCount(0);
        var generation1Before = GC.CollectionCount(1);
        var generation2Before = GC.CollectionCount(2);

        var first = Execute(operation);
        var warm = Execute(operation);
        var metrics = new Dictionary<string, long>(sourceMetrics(), StringComparer.Ordinal);
        var processCount = first.ProcessMetrics.StartedProcesses + warm.ProcessMetrics.StartedProcesses;
        var completedCount = first.ProcessMetrics.CompletedProcesses + warm.ProcessMetrics.CompletedProcesses;
        var stoppedCount = first.ProcessMetrics.StoppedProcesses + warm.ProcessMetrics.StoppedProcesses;

        results.Add(new ReferenceProfileResult(
            scenario,
            corpus.Profile.ToString(),
            packed,
            backend,
            projectionMode,
            corpus.Fingerprint,
            corpus.Root,
            gitVersion,
            first.ElapsedMilliseconds,
            warm.ElapsedMilliseconds,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore,
            GC.CollectionCount(0) - generation0Before,
            GC.CollectionCount(1) - generation1Before,
            GC.CollectionCount(2) - generation2Before,
            Math.Max(first.ParentPeakWorkingSet, warm.ParentPeakWorkingSet),
            Math.Max(first.ProcessMetrics.PeakWorkingSet, warm.ProcessMetrics.PeakWorkingSet),
            processCount,
            completedCount,
            stoppedCount,
            processCount == completedCount + stoppedCount,
            first.Checksum,
            warm.Checksum,
            first.Checksum == warm.Checksum,
            Metric(metrics, "RowsExamined"),
            Metric(metrics, "RowsEmitted"),
            Metric(metrics, "RowsFiltered"),
            Metric(metrics, "RowsSkipped"),
            Metric(metrics, "RowsSkippedByCursor"),
            Metric(metrics, "MaximumBufferedRows"),
            metrics));
    }

    private static ProfileExecution Execute(Func<long> operation)
    {
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        var stopwatch = Stopwatch.StartNew();
        var checksum = operation();
        stopwatch.Stop();
        return new ProfileExecution(
            stopwatch.Elapsed.TotalMilliseconds,
            checksum,
            Process.GetCurrentProcess().PeakWorkingSet64,
            GitCliProcessMetrics.Snapshot());
    }

    private static long Metric(IReadOnlyDictionary<string, long> metrics, string suffix) =>
        metrics.FirstOrDefault(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal)).Value;

    private static string ReadGitVersion(string repositoryPath)
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
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start local Git.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output.Trim();
    }

    private sealed record ProfileExecution(
        double ElapsedMilliseconds,
        long Checksum,
        long ParentPeakWorkingSet,
        GitCliProcessMetricsSnapshot ProcessMetrics);

    private sealed record ReferenceProfileResult(
        string Scenario,
        string Profile,
        bool Packed,
        string Backend,
        string ProjectionMode,
        string CorpusFingerprint,
        string CorpusRoot,
        string GitVersion,
        double FirstRunMilliseconds,
        double WarmRunMilliseconds,
        long ManagedAllocatedBytes,
        int Generation0Collections,
        int Generation1Collections,
        int Generation2Collections,
        long ParentPeakWorkingSetBytes,
        long ChildPeakWorkingSetBytes,
        long ProcessCount,
        long CompletedProcessCount,
        long StoppedProcessCount,
        bool ProcessCleanup,
        long FirstChecksum,
        long WarmChecksum,
        bool ChecksumsMatch,
        long RowsExamined,
        long RowsEmitted,
        long RowsFiltered,
        long RowsSkipped,
        long RowsSkippedByCursor,
        long MaximumBufferedRows,
        IReadOnlyDictionary<string, long> SourceMetrics);
}
