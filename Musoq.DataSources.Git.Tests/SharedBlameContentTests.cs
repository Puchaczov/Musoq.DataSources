using LibGit2Sharp;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class SharedBlameContentTests
{
    [TestMethod]
    public void NativeHunksShareOneDeferredBlobReadAndSurviveProducerDisposal()
    {
        using var fixture = OfflineGitFixture.Create();
        File.WriteAllText(Path.Combine(fixture.SeedPath, "lines.txt"), "first original line\nsecond preserved line\nthird original line\nfourth preserved line\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", ".");
        CommitAt("lines", 1800000000);
        File.WriteAllText(Path.Combine(fixture.SeedPath, "lines.txt"), "first changed line\nsecond preserved line\nthird changed line\nfourth preserved line\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", ".");
        CommitAt("several hunks", 1800003600);
        var sink = new Metrics();
        var source = new BlameRowsSource(fixture.SeedPath, "lines.txt", "HEAD", static p => new Repository(p),
            RuntimeV2TestContexts.CreateExecutionContext(sourceDiagnostics: new SourceDiagnostics(sink)));
        var hunks = source.Chunks.SelectMany(c => c).ToArray();
        Assert.IsTrue(hunks.Length > 1);
        Assert.AreEqual(0L, sink.Get("Git.Blame.BlobReads"));
        var expected = new[] { "first changed line", "second preserved line", "third changed line", "fourth preserved line" };
        for (var repeat = 0; repeat < 2; repeat++)
            CollectionAssert.AreEqual(expected, hunks.SelectMany(h => h.Lines).Select(l => l.Content).ToArray());
        Assert.AreEqual(1L, sink.Get("Git.Blame.BlobReads"));

        void CommitAt(string message, long timestamp)
        {
            using var repository = new Repository(fixture.SeedPath);
            var signature = new Signature("Blame Fixture", "blame@example.invalid", DateTimeOffset.FromUnixTimeSeconds(timestamp));
            repository.Commit(message, signature, signature);
        }
    }

    [TestMethod]
    public void ModernHunksShareOneContentProcessAcrossRepeatedExpansion()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var sink = new Metrics();
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(),
            CancellationToken.None, new SourceDiagnostics(sink));
        var hunks = context.Blame("tracked.txt", "HEAD").ToArray();
        Assert.IsTrue(hunks.Length > 1);
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        for (var repeat = 0; repeat < 2; repeat++)
            CollectionAssert.AreEqual(new[] { "inserted", "first", "second changed", "third" },
                hunks.SelectMany(h => h.Lines).Select(l => l.Content).ToArray());
        Assert.AreEqual(1L, GitCliProcessMetrics.Snapshot().StartedProcesses);
        Assert.AreEqual(1L, sink.Get("Git.Blame.BlobReads"));
    }

    [TestMethod]
    public void NativeAndModernBlameReadRenamedFileAtRequestedRevision()
    {
        foreach (var modern in new[] { false, true })
        {
            using var fixture = modern ? ModernWorktreeTests.Create(out var ignored) : OfflineGitFixture.Create();
            var root = fixture.SeedPath;
            File.WriteAllText(Path.Combine(root, "old.txt"), "one\ntwo\nthree\n");
            OfflineGitFixture.RunGit(root, "add", "old.txt");
            OfflineGitFixture.RunGit(root, "commit", "-qm", "before rename");
            OfflineGitFixture.RunGit(root, "mv", "old.txt", "renamed.txt");
            OfflineGitFixture.RunGit(root, "commit", "-qm", "rename");
            var path = root.Replace("\\", "\\\\");
            foreach (var (file, revision) in new[] { ("renamed.txt", "HEAD"), ("old.txt", "HEAD~1") })
                CollectionAssert.AreEqual(new[] { "one", "two", "three" },
                    WorktreeCompatibilityTests.Query($"select l.Content from git.blame('{path}', '{file}', '{revision}') h cross apply h.Lines l order by l.LineNumber")
                        .Select(r => (string)r.ReadValue(0)).ToArray());
        }
    }

    [TestMethod]
    public void SharedContentPreservesLineEndingsAndFinalLineWithoutTerminator()
    {
        var content = new GitBlameFileContent(() => "one\r\ntwo\rthree\nfour", CancellationToken.None, SourceDiagnostics.None);
        CollectionAssert.AreEqual(new[] { "one", "two", "three", "four" },
            content.Lines(0, 4).Select(l => l.Content).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 3 }, content.Lines(1, 2).Select(l => l.LineNumber).ToArray());
        Assert.AreEqual(0, content.Lines(10, 1).Count());
    }

    private sealed class Metrics : ISourceDiagnosticsSink
    {
        private readonly Dictionary<string, long> _values = new();
        public long Get(string key) => _values.GetValueOrDefault(key);
        public IDisposable Measure(string name, SourceDiagnosticOperation operation) => new EmptyScope();
        public void AddRowsProduced(long count) { }
        public void AddBytesRead(long count) { }
        public void AddMetric(string name, long value) => _values[name] = Get(name) + value;
        private sealed class EmptyScope : IDisposable { public void Dispose() { } }
    }
}
