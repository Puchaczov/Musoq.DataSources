using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Benchmarks;

internal static class GitReferenceBenchmarkVerifier
{
    public static void VerifyStreamingInvariants(GitReferenceBenchmarkCorpus corpus)
    {
        VerifyCapturedAdvertisement(corpus);

        var local = new GitReferenceSourceBenchmarks
        {
            Profile = corpus.Profile,
            Packed = corpus.Packed,
            Backend = "git-cli"
        };
        local.Setup();
        VerifyTakeZero(local.DirectTagsTakeZero, "Git.Tags");
        VerifyEarlyStop(local.DirectTagsSkipTake, () => local.LastMetrics, "Git.Tags");

        var remote = new GitRemoteTagSourceBenchmarks
        {
            Profile = corpus.Profile,
            Packed = corpus.Packed
        };
        remote.Setup();
        VerifyTakeZero(remote.DirectRemoteTagsTakeZero, "Git.RemoteTags");
        VerifyEarlyStop(remote.DirectRemoteTagsSkipTake, () => remote.LastMetrics, "Git.RemoteTags");
        VerifyCancellation(corpus);
    }

    private static void VerifyCapturedAdvertisement(GitReferenceBenchmarkCorpus corpus)
    {
        var count = 0;
        var annotated = 0;
        foreach (var tag in GitRemoteTagProtocolParser.Parse(
                     File.ReadLines(corpus.RemoteAdvertisementPath),
                     corpus.ClientPath,
                     "origin",
                     corpus.RemotePath,
                     needsPeel: true,
                     GitRemoteTagReadQuery.Empty,
                     CancellationToken.None))
        {
            count++;
            if (tag.IsAnnotated)
                annotated++;
        }

        if (count != corpus.TagCount || annotated != corpus.AnnotatedTagCount)
            throw new InvalidDataException(
                $"Captured remote advertisement mismatch: tags={count}, annotated={annotated}, " +
                $"expected tags={corpus.TagCount}, annotated={corpus.AnnotatedTagCount}.");
    }

    private static void VerifyTakeZero(Func<long> operation, string sourceName)
    {
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        _ = operation();
        var process = GitCliProcessMetrics.Snapshot();
        if (process.StartedProcesses != 0)
            throw new InvalidDataException($"{sourceName} TAKE 0 started {process.StartedProcesses} Git processes.");
    }

    private static void VerifyEarlyStop(
        Func<long> operation,
        Func<IReadOnlyDictionary<string, long>> sourceMetrics,
        string sourceName)
    {
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        _ = operation();
        var process = GitCliProcessMetrics.Snapshot();
        EnsureProcessCleanup(process, sourceName);
        var metrics = sourceMetrics();
        if (Metric(metrics, "EarlyStop") != 1)
            throw new InvalidDataException($"{sourceName} did not report early termination for its bounded window.");
        if (Metric(metrics, "MaximumBufferedRows") > 128)
            throw new InvalidDataException($"{sourceName} exceeded its bounded row buffer.");
    }

    private static void VerifyCancellation(GitReferenceBenchmarkCorpus corpus)
    {
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            foreach (var _ in GitOperationReaders.CliTags.ReadStreaming(
                         corpus.RepositoryPath,
                         GitReferenceBackendOptions.Default,
                         new GitProjection(true, [nameof(TagEntity.FriendlyName)]),
                         GitTagReadQuery.Empty,
                         static path => new Repository(path),
                         cancellation.Token))
            {
            }

            throw new InvalidDataException("Cancelled local tag enumeration completed without cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            GitOperationReaders.RemoteTags.Read(
                corpus.ClientPath,
                "origin",
                GitReferenceBackendOptions.Default,
                new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName)]),
                GitRemoteTagReadQuery.Empty,
                static path => new Repository(path),
                cancellation.Token,
                static _ => true);
            throw new InvalidDataException("Cancelled remote tag enumeration completed without cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        var process = GitCliProcessMetrics.Snapshot();
        if (process.StartedProcesses != 0)
            throw new InvalidDataException($"Cancellation started {process.StartedProcesses} Git processes.");
    }

    private static void EnsureProcessCleanup(GitCliProcessMetricsSnapshot process, string sourceName)
    {
        if (process.StartedProcesses != process.CompletedProcesses + process.StoppedProcesses)
            throw new InvalidDataException(
                $"{sourceName} leaked a Git process: started={process.StartedProcesses}, " +
                $"completed={process.CompletedProcesses}, stopped={process.StoppedProcesses}.");
    }

    private static long Metric(IReadOnlyDictionary<string, long> metrics, string suffix) =>
        metrics.FirstOrDefault(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal)).Value;
}
