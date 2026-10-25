using System.Diagnostics;
using System.Text;
using LibGit2Sharp;
using Musoq.DataSources.Common;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
[DoNotParallelize]
public class EarlyPatternTests
{
    [TestMethod]
    public void MeasurePatternSelectivity()
    {
        var message = new string('x', 4096) + "😀";
        var output = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 100).Select(i => "\x1e" + i.ToString("D4") + "\0" + message + "\0author\0")));
        foreach (var (pattern, expected) in new[] { ("^0000$", 1), ("^00[0-4]", 50), ("^00", 100) })
        foreach (var enabled in new[] { false, true })
        {
            var predicate = new SourcePredicateRegexMatch(new("Sha"), pattern, TimeSpan.FromMilliseconds(31));
            int Read()
            {
                var count = 0;
                var native = enabled ? new NativePatternExecution([predicate]) : null;
                new GitCliCommitReader(new CapturedFactory(output)).Read("fixture", new GitProjection(true, ["Sha", "Message", "Author"], nativePatterns: native), null,
                    _ => throw new AssertFailedException(), default, row => { if (enabled || ProviderRegex.Match(predicate, row.Sha) == true) count++; return true; });
                return count;
            }
            Assert.AreEqual(expected, Read());
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) Assert.AreEqual(expected, Read());
            timer.Stop();
            Console.WriteLine($"native={enabled}; admitted={expected}/100; iterations=20; elapsedMs={timer.Elapsed.TotalMilliseconds}; allocatedBytes={GC.GetAllocatedBytesForCurrentThread() - allocated}");
        }
    }

    private sealed class CapturedFactory(byte[] output) : IGitCliProcessFactory
    {
        public IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken) => new FakeProcess(output);
    }

    [TestMethod]
    public void CliCommits_PredicatePrefixAvoidsUnrelatedDecodingInOneCommand()
    {
        foreach (var (pattern, expected) in new[] { ("^0000$", 1), ("^00[0-4]", 50), ("^00", 100) })
        {
            var native = new NativePatternExecution([new SourcePredicateRegexMatch(new("Sha"), pattern, TimeSpan.FromMilliseconds(31))]);
            var factory = new CommitFactory();
            var reader = new GitCliCommitReader(factory);
            var rows = new List<GitCommitRecord>();
            reader.Read("fixture", new GitProjection(true, ["Sha", "Message", "Author"], nativePatterns: native), null, _ => throw new AssertFailedException("CLI must not open a managed repository."), default,
                row => { rows.Add(row); return true; });
            Assert.AreEqual(expected, rows.Count);
            Assert.AreEqual(1, factory.Requests);
            Assert.IsTrue(factory.Process!.Disposed);
            Assert.IsTrue(rows.All(row => row.Message == "message 😀" && row.Author == "author"));
            Assert.AreEqual(100L, native.Candidates);
            Assert.AreEqual(100L + 2 * expected, native.DecodedStrings);
            Assert.AreEqual(100L - expected, native.Rejected);
        }
    }

    [TestMethod]
    public void CliCommits_ReordersAuthorAheadOfMessage_AndConsumesRejectedRecords()
    {
        var native = new NativePatternExecution([new SourcePredicateRegexMatch(new("Author"), "^none$", TimeSpan.FromMilliseconds(31))]);
        var factory = new CommitFactory();
        new GitCliCommitReader(factory).Read("fixture", new GitProjection(true, ["Sha", "Message", "Author"], nativePatterns: native), null,
            _ => throw new AssertFailedException(), default, _ => throw new AssertFailedException("Rejected record reached materialization."));
        Assert.IsTrue(factory.Format!.IndexOf("%an", StringComparison.Ordinal) < factory.Format.IndexOf("%B", StringComparison.Ordinal));
        Assert.AreEqual(100L, native.Rejected);
        Assert.AreEqual(200L, native.DecodedStrings);
        Assert.IsTrue(factory.Process!.Disposed);
    }

    [TestMethod]
    public void NativeCommitAndBranchReaders_MatchDisabledNativeStage()
    {
        using var fixture = OfflineGitFixture.Create();
        using (var repo = new Repository(fixture.SeedPath)) repo.Branches.Add("keep", repo.Head.Tip);
        foreach (var reader in new IGitCommitReader[] { new LibGit2CommitReader(), new GitCliCommitReader() })
        {
            var baseline = new List<GitCommitRecord>();
            var projection = new GitProjection(true, ["Sha", "Message", "Author", "AuthorEmail"]);
            reader.Read(fixture.SeedPath, projection, null, path => new Repository(path), default, row => { baseline.Add(row); return true; });
            var native = new NativePatternExecution([new SourcePredicateRegexMatch(new("Sha"), "^" + baseline[0].Sha + "$", TimeSpan.FromMilliseconds(31))]);
            var actual = new List<GitCommitRecord>();
            reader.Read(fixture.SeedPath, new GitProjection(true, projection.Columns, nativePatterns: native), null, path => new Repository(path), default, row => { actual.Add(row); return true; });
            CollectionAssert.AreEqual(new[] { baseline[0] }, actual);
            if (reader is LibGit2CommitReader) Assert.AreEqual(3L, native.RichFieldLoads);
        }
        foreach (var reader in new IGitBranchReader[] { new LibGit2BranchReader(), new GitCliBranchReader() })
        {
            var native = new NativePatternExecution([new SourcePredicateRegexMatch(new("FriendlyName"), "^keep$", TimeSpan.FromMilliseconds(31))]);
            var rows = new List<GitBranchRecord>();
            reader.Read(fixture.SeedPath, new GitProjection(true, ["FriendlyName", "CanonicalName"], nativePatterns: native), path => new Repository(path), default,
                row => { rows.Add(row); return true; });
            Assert.AreEqual("keep", rows.Single().FriendlyName);
            Assert.IsTrue(native.Rejected > 0);
        }
    }

    [TestMethod]
    public void FileHistory_PathRejectionPreservesIntrinsicWindowAndAvoidsRichMetadata()
    {
        using var fixture = OfflineGitFixture.Create();
        using (var repository = new Repository(fixture.SeedPath))
        {
            var signature = new Signature("Author", "author@example.test", DateTimeOffset.Now);
            foreach (var name in new[] { "keep.txt", "reject.txt" })
            {
                File.WriteAllText(Path.Combine(fixture.SeedPath, name), name);
                Commands.Stage(repository, name);
                repository.Commit(name, signature, signature);
            }
        }
        foreach (var backend in new[] { "git-cli", "libgit2" })
        {
            var predicate = new SourcePredicateRegexMatch(new("FilePath"), "^keep", TimeSpan.FromMilliseconds(31));
            var plan = new GitSchema().TryPlanSource("filehistory", new() { Identity = new("git", "s", "s", "filehistory"), Predicate = predicate,
                RequiredColumns = [new("Author"), new("AuthorEmail"), new("CommittedWhen"), new("FilePath")] });
            var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan,
                sourceRuntimeSettings: new Dictionary<string, string> { ["GIT_HISTORY_BACKEND"] = backend });
            NativePatternExecution? execution = null;
            var source = PatternSourcePlan.Read(context, inner =>
            {
                execution = NativePatternExecution.Get(inner);
                return new FileHistoryRowsSource(fixture.SeedPath, "*", 0, 1, path => new Repository(path), inner);
            }, ["FilePath"]);
            Assert.AreEqual(0, source.Chunks.Sum(chunk => chunk.Count));
            Assert.AreEqual(1L, execution!.Candidates);
            Assert.AreEqual(1L, execution.Rejected);
            Assert.AreEqual(0L, execution.RichFieldLoads);
        }
    }

    [TestMethod]
    public void TagReaders_FilterAnnotatedAndLightweightRecordsBeforeRichWrappers()
    {
        using var fixture = OfflineGitFixture.Create();
        fixture.CreateLightweightTag("light");
        fixture.CreateAnnotatedTag("keep", "message");
        foreach (var reader in new IGitTagReader[] { new LibGit2TagReader(), new GitCliTagReader() })
        foreach (var column in new[] { "FriendlyName", "Message", "TargetSha" })
        foreach (var negated in new[] { false, true })
        {
            var native = new NativePatternExecution([new SourcePredicateRegexMatch(new(column), column == "FriendlyName" ? "keep" : column == "Message" ? "message" : ".", TimeSpan.FromMilliseconds(31), isNegated: negated)]);
            var projection = new GitProjection(true, ["FriendlyName", "Message", "TargetSha", "Annotation"], nativePatterns: native);
            var actual = reader.ReadStreaming(fixture.SeedPath, GitReferenceBackendOptions.Default, projection, GitTagReadQuery.Empty, path => new Repository(path), default).ToArray();
            Assert.AreEqual(column == "TargetSha" ? negated ? 0 : 2 : 1, actual.Length);
            Assert.AreEqual(2L, native.Candidates);
            if (column == "Message" && negated) Assert.AreEqual("light", actual.Single().FriendlyName);
        }
    }

    [TestMethod]
    public void SkipToken_ConsumesLargeUtf8FieldsAndPreservesFraming()
    {
        var bytes = Encoding.UTF8.GetBytes("first\0" + new string('ż', 40000) + "\0last\0");
        using var stream = new MemoryStream(bytes);
        using var reader = new GitNulDelimitedUtf8Reader(stream);
        Assert.AreEqual("first", reader.ReadToken());
        Assert.IsTrue(reader.SkipToken());
        Assert.AreEqual("last", reader.ReadToken());
        Assert.IsFalse(reader.SkipToken());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => reader.SkipToken(cancelled.Token));
    }

    private sealed class CommitFactory : IGitCliProcessFactory
    {
        internal int Requests;
        internal string? Format;
        internal FakeProcess? Process;
        public IGitCliProcess Start(GitCliProcessRequest request, CancellationToken cancellationToken)
        {
            Requests++;
            Format = request.OperationArguments.Single(argument => argument.StartsWith("--format=", StringComparison.Ordinal));
            var authorFirst = Format.IndexOf("%an", StringComparison.Ordinal) < Format.IndexOf("%B", StringComparison.Ordinal);
            var output = string.Concat(Enumerable.Range(0, 100).Select(i => "\x1e" + i.ToString("D4") + "\0" +
                (authorFirst ? "author\0message 😀\0" : "message 😀\0author\0")));
            return Process = new FakeProcess(Encoding.UTF8.GetBytes(output));
        }
    }
    private sealed class FakeProcess(byte[] output) : IGitCliProcess
    {
        public Stream StandardOutput { get; } = new MemoryStream(output);
        internal bool Disposed;
        public void Complete() { }
        public void Stop() { }
        public void Dispose() { Disposed = true; StandardOutput.Dispose(); }
    }
}
