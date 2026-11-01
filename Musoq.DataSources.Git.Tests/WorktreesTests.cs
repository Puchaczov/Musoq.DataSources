using System.Text;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class WorktreesTests
{
    [TestMethod]
    public void ListsLockedDetachedAndStaleRegistrationsWithoutOpeningTheirPaths()
    {
        using var fixture = OfflineGitFixture.Create();
        var locked = Path.Combine(fixture.Root, "locked żółw");
        var detached = Path.Combine(fixture.Root, "detached");
        var stale = Path.Combine(fixture.Root, "stale");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "-qb", "feature", locked);
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "lock", "--reason", "portable\nsecond line", locked);
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "-q", "--detach", detached);
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "-q", "--detach", stale);
        Directory.Delete(stale, true);
        var rows = new WorktreesRowsSource(locked, RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks.SelectMany(c => c).ToArray();
        Assert.AreEqual(4, rows.Length);
        var lockedRow = rows.Single(w => w.IsLocked);
        Assert.AreEqual("portable\nsecond line", lockedRow.LockReason);
        Assert.AreEqual("refs/heads/feature", lockedRow.Branch);
        Assert.AreEqual(2, rows.Count(w => w.IsDetached));
        Assert.IsTrue(rows.Single(w => w.IsPrunable).Path.EndsWith("stale"));
        var escaped = locked.Replace("\\", "\\\\");
        var sql = WorktreeCompatibilityTests.Query($"select w.Path, s.FilePath from git.worktrees('{escaped}') w cross apply git.status(w.Path) s where w.IsPrunable = false and w.IsBare = false");
        Assert.AreEqual(0, sql.Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Path from git.worktrees('{escaped}') where IsLocked = true").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Path from git.worktrees('{fixture.RemotePath.Replace("\\", "\\\\")}') where IsBare = true").Count);
    }

    [TestMethod]
    public void NulProtocolPreservesWhitespaceUnknownAttributesAndUnbornHead()
    {
        var rows = Parse("worktree /path/with\nnewline\0HEAD 0000000000000000000000000000000000000000\0branch refs/heads/new\0locked\0future value\0\0worktree /bare\0bare\0\0");
        Assert.AreEqual(2, rows.Length);
        Assert.AreEqual("/path/with\nnewline", rows[0].Path);
        Assert.IsNull(rows[0].HeadSha);
        Assert.IsTrue(rows[0].IsLocked);
        Assert.IsNull(rows[0].LockReason);
        Assert.IsTrue(rows[1].IsBare);
        Assert.ThrowsException<InvalidDataException>(() => Parse("worktree /truncated\0HEAD abc\0"));
        Assert.ThrowsException<InvalidDataException>(() => Parse("HEAD abc\0\0"));
        Assert.ThrowsException<InvalidDataException>(() => Parse("worktree /one\0worktree /two\0\0"));
    }

    [TestMethod]
    public void TakeZeroAndCancelledEnumerationStartNoProcess()
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: new SourceExecutionPlan { Identity = SourceIdentity.Empty, AcceptedTake = 0 });
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        Assert.AreEqual(0, new WorktreesRowsSource("missing", context).Chunks.SelectMany(c => c).Count());
        Assert.AreEqual(0L, GitCliProcessMetrics.Snapshot().StartedProcesses);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => new GitWorktreeReader()
            .Read("missing", GitHistoryBackendOptions.Default, cancelled.Token).ToArray());
    }

    private static WorktreeEntity[] Parse(string protocol)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(protocol));
        using var reader = new GitNulDelimitedUtf8Reader(stream);
        return GitWorktreeReader.Parse(reader, CancellationToken.None).ToArray();
    }
}
