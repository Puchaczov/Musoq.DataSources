using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class ModernLibraryTests
{
    [TestMethod]
    public void ModernWorktreesSupportEveryLibraryOperationAndLazyDifferenceContent()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        var repository = context.Repository();
        var library = new GitLibrary();
        var old = library.CommitFrom(repository, "HEAD~1");
        var current = library.CommitFrom(repository, "HEAD");
        var feature = library.BranchFrom(repository, "refs/heads/feature");
        var main = library.BranchFrom(repository, "refs/heads/main");
        var difference = library.DifferenceBetween(repository, old, current).Single();
        Assert.AreEqual("Modified", difference.ChangeKind);
        Assert.AreEqual("first\nsecond\nthird\n", difference.OldContent);
        Assert.AreEqual("inserted\nfirst\nsecond changed\nthird\n", difference.NewContent);
        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes(difference.NewContent!), difference.NewContentBytes);
        Assert.AreEqual(1, library.DifferenceBetween(repository, main, feature).Count());
        Assert.AreEqual(1, library.DifferenceBetweenCurrentAndBranch(repository, main).Count());
        Assert.AreEqual(1, library.DifferenceBetweenCommitAndBranch(repository, old, feature).Count());
        Assert.AreEqual(1, library.DifferenceBetweenBranchAndCommit(repository, feature, old).Count());
        Assert.AreEqual(1, library.SearchForBranches(repository, "^feature$").Count());
        Assert.AreEqual("refs/heads/main", feature.ParentBranch!.CanonicalName);
        Assert.AreEqual(old.Sha, library.FindMergeBase(repository, feature)!.MergeBaseCommit.Sha);
        Assert.AreEqual(1, library.GetBranchSpecificCommits(repository, feature).Count());
        var patch = library.PatchBetween(repository, old, current).Single();
        Assert.AreEqual(2, patch.LinesAdded);
        Assert.AreEqual(1, patch.LinesDeleted);
        StringAssert.Contains(patch.Content, "+inserted");
        Assert.AreEqual("tracked.txt", patch.Changes.Single().Path);
        Assert.IsFalse(patch.Changes.Single().IsBinaryComparison);
        var path = linked.Replace("\\", "\\\\");
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select d.Path from git.repository('{path}') r cross apply r.DifferenceBetween(r.CommitFrom('HEAD~1'), r.CommitFrom('HEAD')) d").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select p.LinesAdded from git.repository('{path}') r cross apply r.PatchBetween(r.CommitFrom('HEAD~1'), r.CommitFrom('HEAD')) p").Count);
    }

    [TestMethod]
    public void CliRawDifferencesMatchNativeForRenamesDeletesBinaryAndUnicode()
    {
        using var fixture = OfflineGitFixture.Create();
        File.WriteAllText(Path.Combine(fixture.SeedPath, "original.txt"), string.Join('\n', Enumerable.Range(1, 20)));
        File.WriteAllText(Path.Combine(fixture.SeedPath, "deleted.txt"), "delete");
        File.WriteAllBytes(Path.Combine(fixture.SeedPath, "binary.bin"), [0, 1, 2]);
        File.WriteAllText(Path.Combine(fixture.SeedPath, "copy-source.txt"), string.Join('\n', Enumerable.Range(1, 30)));
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", ".");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "before");
        OfflineGitFixture.RunGit(fixture.SeedPath, "mv", "original.txt", "renamed.txt");
        File.Copy(Path.Combine(fixture.SeedPath, "copy-source.txt"), Path.Combine(fixture.SeedPath, "copy.txt"));
        File.AppendAllText(Path.Combine(fixture.SeedPath, "copy-source.txt"), "\nmodified source\n");
        File.Delete(Path.Combine(fixture.SeedPath, "deleted.txt"));
        File.WriteAllBytes(Path.Combine(fixture.SeedPath, "binary.bin"), [0, 1, 3]);
        File.WriteAllText(Path.Combine(fixture.SeedPath, "new żółw.txt"), "added");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", "-A");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "after");
        using var native = new Repository(fixture.SeedPath);
        var library = new GitLibrary();
        var entity = new RepositoryEntity(native);
        var expected = library.DifferenceBetween(entity, library.CommitFrom(entity, "HEAD~1"), library.CommitFrom(entity, "HEAD"))
            .Select(Describe).Order(StringComparer.Ordinal).ToArray();
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(fixture.SeedPath), new Dictionary<string, string>(), CancellationToken.None);
        var actual = context.Differences("HEAD~1", "HEAD").Select(Describe).Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expected, actual);
    }

    private static string Describe(DifferenceEntity d) => string.Join("|", d.Path, d.ChangeKind, d.OldPath,
        d.OldMode, d.NewMode, d.OldSha, d.NewSha, d.Exists);
}
