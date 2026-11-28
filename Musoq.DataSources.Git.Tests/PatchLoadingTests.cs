using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class PatchLoadingTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public void PatchChanges_StreamPathsBeforeLoadingPatchDetails(bool useCli)
    {
        var linked = string.Empty;
        using var fixture = useCli ? ModernWorktreeTests.Create(out linked) : OfflineGitFixture.Create();
        var path = useCli ? linked : fixture.SeedPath;
        File.WriteAllText(Path.Combine(path, "old.txt"), "rename this file\n");
        File.WriteAllBytes(Path.Combine(path, "old.bin"), [0, 1, 2, 3]);
        OfflineGitFixture.RunGit(path, "add", "--", "old.txt", "old.bin");
        OfflineGitFixture.RunGit(path, "commit", "-qm", "before changes");
        OfflineGitFixture.RunGit(path, "mv", "old.txt", "new.txt");
        File.WriteAllBytes(Path.Combine(path, "old.bin"), [0, 4, 5, 6]);
        File.WriteAllText(Path.Combine(path, "added.txt"), "one\ntwo\n");
        OfflineGitFixture.RunGit(path, "add", "--", "old.bin", "added.txt");
        OfflineGitFixture.RunGit(path, "commit", "-qm", "after changes");

        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(path), new Dictionary<string, string>(),
            CancellationToken.None);
        Assert.AreEqual(useCli, context.RequiresCli);
        var first = context.CommitFrom("HEAD~1")!.Sha!;
        var second = context.CommitFrom("HEAD")!.Sha!;
        var expected = OpenPatch();
        var expectedText = expected.Content;
        var expectedEntries = expected.Changes.ToArray();
        var candidate = OpenPatch();
        PatchEntryChangesEntity[] actual;
        using (GitCliProcessMetrics.BeginMeasurement())
        {
            actual = candidate.Changes.ToArray();
            Assert.AreEqual(useCli ? 3L : 0L, GitCliProcessMetrics.Snapshot().StartedProcesses);
        }

        Assert.IsTrue(actual.Length >= 3);
        CollectionAssert.AreEqual(expectedEntries.Select(e => (e.Path, e.OldMode, e.Mode)).ToArray(),
            actual.Select(e => (e.Path, e.OldMode, e.Mode)).ToArray());
        using (GitCliProcessMetrics.BeginMeasurement())
        {
            CollectionAssert.AreEqual(expectedEntries.Select(Describe).ToArray(), actual.Select(Describe).ToArray());
            Assert.AreEqual(expectedText, candidate.Content);
            Assert.AreEqual(useCli ? 2L : 0L, GitCliProcessMetrics.Snapshot().StartedProcesses);
        }

        var sql = $"select c.Path from git.repository('{path.Escape()}') r " +
                  "cross apply r.PatchBetween(r.CommitFrom('HEAD~1'), r.CommitFrom('HEAD')) p cross apply p.Changes c";
        using var result = WorktreeCompatibilityTests.Query(sql);
        CollectionAssert.AreEqual(expectedEntries.Select(e => e.Path).ToArray(),
            result.Select(row => (string)row.ReadValue(0)).ToArray());

        PatchEntity OpenPatch() => useCli ? new PatchEntity(context, first, second) : new PatchEntity(path, first, second);

        static (int Added, int Deleted, bool Binary, string Content) Describe(PatchEntryChangesEntity entry) =>
            (entry.LinesAdded, entry.LinesDeleted, entry.IsBinaryComparison, entry.Content);
    }

    [TestMethod]
    public void ModernStatisticsUseOneProcessAndFullContentRemainsAvailableInEitherOrder()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        foreach (var contentFirst in new[] { false, true })
        {
            var patch = new PatchEntity(context, context.CommitFrom("HEAD~1")!.Sha!, context.CommitFrom("HEAD")!.Sha!);
            using var measurement = GitCliProcessMetrics.BeginMeasurement();
            if (contentFirst) StringAssert.Contains(patch.Content, "+inserted");
            var processes = GitCliProcessMetrics.Snapshot().StartedProcesses;
            Assert.AreEqual(2, patch.LinesAdded);
            Assert.AreEqual(1, patch.LinesDeleted);
            Assert.AreEqual(contentFirst ? processes : 1L, GitCliProcessMetrics.Snapshot().StartedProcesses);
            StringAssert.Contains(patch.Content, "+inserted");
            Assert.AreEqual("tracked.txt", patch.Changes.Single().Path);
            Assert.AreEqual(2, patch.LinesAdded);
        }
    }

    [TestMethod]
    public void ModernTextAndBytesShareOneBlobProcessInEitherOrder()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        foreach (var textFirst in new[] { false, true })
        {
            var difference = context.Differences("HEAD~1", "HEAD").Single();
            using var measurement = GitCliProcessMetrics.BeginMeasurement();
            if (textFirst) _ = difference.NewContent; else _ = difference.NewContentBytes;
            CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes(difference.NewContent!), difference.NewContentBytes);
            Assert.AreEqual(1L, GitCliProcessMetrics.Snapshot().StartedProcesses);
        }
    }

    [TestMethod]
    public void NativeByteThenTextReusePreservesBomAndInvalidUtf8Decoding()
    {
        using var fixture = OfflineGitFixture.Create();
        File.WriteAllBytes(Path.Combine(fixture.SeedPath, "encoding.txt"), [0xef, 0xbb, 0xbf, 0x61, 0xff, 0x0a]);
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", ".");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "encoding");
        using var repository = new Repository(fixture.SeedPath);
        var blob = (Blob)repository.Head.Tip["encoding.txt"].Target;
        var library = new GitLibrary();
        var entity = new RepositoryEntity(repository);
        var difference = library.DifferenceBetween(entity, library.CommitFrom(entity, "HEAD~1"), library.CommitFrom(entity, "HEAD")).Single();
        _ = difference.NewContentBytes;
        Assert.AreEqual(blob.GetContentText(), difference.NewContent);
    }
}
