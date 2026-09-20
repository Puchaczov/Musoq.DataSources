using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class StashPushdownTests
{
    [TestMethod]
    public void CliAndLibGit2StashReadersHaveIdentityAndParentParity()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateStash("first stash");
        fixture.CreateStash("untracked stash", includeUntrackedFiles: true);

        var projection = new GitProjection(true, [
            nameof(StashEntity.Selector),
            nameof(StashEntity.Sha),
            nameof(StashEntity.Message),
            nameof(StashEntity.Index),
            nameof(StashEntity.WorkTree),
            nameof(StashEntity.UntrackedFiles)]);
        var cli = Read(GitOperationReaders.CliStashes, fixture.SeedPath, projection);
        var libGit2 = Read(GitOperationReaders.Stashes, fixture.SeedPath, projection);

        Assert.HasCount(2, cli);
        Assert.HasCount(2, libGit2);
        CollectionAssert.AreEqual(
            libGit2.Select(stash => (stash.Selector, stash.Sha, stash.Message, stash.IndexSha, stash.WorkTreeSha, stash.UntrackedFilesSha)).ToArray(),
            cli.Select(stash => (stash.Selector, stash.Sha, stash.Message, stash.IndexSha, stash.WorkTreeSha, stash.UntrackedFilesSha)).ToArray());
    }

    [TestMethod]
    public void CliStashReaderPushesDownSelectorAndShaEquality()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateStash("first stash");
        fixture.CreateStash("second stash");

        var all = Read(GitOperationReaders.CliStashes, fixture.SeedPath, GitProjection.NotAccepted);
        var selector = Read(
            GitOperationReaders.CliStashes,
            fixture.SeedPath,
            GitProjection.NotAccepted,
            new GitStashReadQuery("stash@{1}", null, null, null));
        var sha = Read(
            GitOperationReaders.CliStashes,
            fixture.SeedPath,
            GitProjection.NotAccepted,
            new GitStashReadQuery(null, all[1].Sha, null, null));

        Assert.HasCount(2, all);
        Assert.HasCount(1, selector);
        Assert.AreEqual("stash@{1}", selector[0].Selector);
        Assert.HasCount(1, sha);
        Assert.AreEqual(all[1].Sha, sha[0].Sha);
    }

    [TestMethod]
    public void FiniteSelectorSetStopsAtTheHighestRequiredReflogPosition()
    {
        var factory = new CapturingStashProcessFactory(
            "stash@{0}\0aaaaaaaa\0stash@{1}\0bbbbbbbb\0stash@{2}\0cccccccc\0" +
            string.Concat(Enumerable.Repeat("stash@{3}\0dddddddd\0", 1_000)));
        var records = new List<GitStashRecord>();
        new GitCliStashReader(factory).Read(
            "repo",
            GitReferenceBackendOptions.Default,
            new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
            new GitStashReadQuery(null, null, ["stash@{0}", "stash@{2}"], null),
            static _ => throw new InvalidOperationException(),
            CancellationToken.None,
            record =>
            {
                records.Add(record);
                return true;
            });

        CollectionAssert.AreEqual(new[] { "stash@{0}", "stash@{2}" }, records.Select(record => record.Selector).ToArray());
        Assert.IsTrue(factory.Process!.BytesConsumedAtDispose < factory.Process.TotalBytes);
    }

    [TestMethod]
    public void StashDiagnosticsDistinguishDirectSelectorLookupFromShaStreaming()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateStash("diagnostic stash");
        var all = Read(GitOperationReaders.CliStashes, fixture.SeedPath, GitProjection.NotAccepted);

        var selectorSink = new CapturingDiagnosticsSink();
        var selectorRows = ReadSource(
            fixture.SeedPath,
            nameof(StashEntity.Selector),
            "stash@{0}",
            selectorSink);
        Assert.HasCount(1, selectorRows);
        Assert.AreEqual(1, selectorSink.Metrics["Git.Stashes.DirectLookup"]);

        var shaSink = new CapturingDiagnosticsSink();
        var shaRows = ReadSource(fixture.SeedPath, nameof(StashEntity.Sha), all[0].Sha, shaSink);
        Assert.HasCount(1, shaRows);
        Assert.AreEqual(0, shaSink.Metrics["Git.Stashes.DirectLookup"]);
    }

    private static List<GitStashRecord> Read(
        IGitStashReader reader,
        string path,
        GitProjection projection,
        GitStashReadQuery? query = null)
    {
        var records = new List<GitStashRecord>();
        reader.Read(
            path,
            GitReferenceBackendOptions.Default,
            projection,
            query ?? GitStashReadQuery.Empty,
            static repositoryPath => new Repository(repositoryPath),
            CancellationToken.None,
            record =>
            {
                records.Add(record);
                return true;
            });
        return records;
    }

    private static List<StashEntity> ReadSource(
        string path,
        string column,
        string value,
        CapturingDiagnosticsSink sink)
    {
        var predicate = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(column)),
            new SourcePredicateLiteral(value));
        var plan = GitSourcePlanner.Plan("stashes", new SourcePlanRequest
        {
            Identity = new SourceIdentity("git", "git", "git", "stashes"),
            RequiredColumns = [new SourceColumnRef(column)],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = []
        });
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: plan.ExecutionPlan,
            sourceDiagnostics: new SourceDiagnostics(sink));
        return new StashesRowsSource(path, static repositoryPath => new Repository(repositoryPath), context)
            .Chunks.SelectMany(static chunk => chunk).ToList();
    }

    private sealed class CapturingStashProcessFactory : IGitCliProcessFactory
    {
        private readonly byte[] _output;

        public CapturingStashProcessFactory(string output)
        {
            _output = System.Text.Encoding.UTF8.GetBytes(output);
        }

        public CapturingStashProcess? Process { get; private set; }

        public IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken)
        {
            Process = new CapturingStashProcess(_output);
            return Process;
        }
    }

    private sealed class CapturingDiagnosticsSink : ISourceDiagnosticsSink
    {
        public Dictionary<string, long> Metrics { get; } = new(StringComparer.Ordinal);

        public IDisposable Measure(string name, SourceDiagnosticOperation operation) =>
            new CallbackDisposable();

        public void AddRowsProduced(long count)
        {
        }

        public void AddBytesRead(long bytes)
        {
        }

        public void AddMetric(string name, long value) => Metrics[name] = Metrics.GetValueOrDefault(name) + value;
    }

    private sealed class CallbackDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class CapturingStashProcess : IGitCliProcess
    {
        private readonly MemoryStream _output;

        public CapturingStashProcess(byte[] output)
        {
            _output = new MemoryStream(output, writable: false);
            TotalBytes = output.Length;
        }

        public Stream StandardOutput => _output;

        public int BytesConsumedAtDispose { get; private set; }

        public int TotalBytes { get; }

        public void Complete()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            BytesConsumedAtDispose = checked((int)_output.Position);
            _output.Dispose();
        }
    }
}
