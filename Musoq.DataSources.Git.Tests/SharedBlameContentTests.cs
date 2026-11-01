using System.Collections.Concurrent;
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
        var file = Path.Combine(fixture.SeedPath, "lines.txt");
        File.WriteAllText(file, "first preserved line\nsecond preserved line\nthird preserved line\nfourth preserved line\n");
        var originalCommit = CommitAt("lines", 1800000000);
        // Appending keeps the original block intact and gives this sharing test two unambiguous hunks.
        File.AppendAllText(file, "appended line\n");
        var appendedCommit = CommitAt("append a second hunk", 1800003600);
        var sink = new Metrics();
        var source = new BlameRowsSource(fixture.SeedPath, "lines.txt", "HEAD", static p => new Repository(p),
            RuntimeV2TestContexts.CreateExecutionContext(sourceDiagnostics: new SourceDiagnostics(sink)));
        var hunks = source.Chunks.SelectMany(c => c).ToArray();
        CollectionAssert.AreEqual(new[] { originalCommit, appendedCommit }, hunks.Select(h => h.CommitSha).ToArray());
        Assert.AreEqual(0L, sink.Get("Git.Blame.BlobReads"));
        var expected = new[] { "first preserved line", "second preserved line", "third preserved line", "fourth preserved line", "appended line" };
        for (var repeat = 0; repeat < 2; repeat++)
            CollectionAssert.AreEqual(expected, hunks.SelectMany(h => h.Lines).Select(l => l.Content).ToArray());
        Assert.AreEqual(1L, sink.Get("Git.Blame.BlobReads"));

        string CommitAt(string message, long timestamp)
        {
            using var repository = new Repository(fixture.SeedPath);
            Commands.Stage(repository, "lines.txt");
            var signature = new Signature("Blame Fixture", "blame@example.invalid", DateTimeOffset.FromUnixTimeSeconds(timestamp));
            return repository.Commit(message, signature, signature).Sha;
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
        private readonly ConcurrentDictionary<string, long> _values = new();
        public long Get(string key) => _values.GetValueOrDefault(key);
        public IDisposable Measure(string name, SourceDiagnosticOperation operation) => new EmptyScope();
        public void AddRowsProduced(long count) { }
        public void AddBytesRead(long count) { }
        public void AddMetric(string name, long value) => _values.AddOrUpdate(name, value, (_, current) => current + value);
        private sealed class EmptyScope : IDisposable { public void Dispose() { } }
    }
}
