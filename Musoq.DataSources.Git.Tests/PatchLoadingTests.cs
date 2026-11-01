using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class PatchLoadingTests
{
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
