using LibGit2Sharp;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class ModernWorktreeTests
{
    [TestMethod]
    public void ModernFormatSupportsSourcesNestedReadsPatternsAndTrackingAgainstUpstream()
    {
        using var fixture = Create(out var linked);
        var path = linked.Replace("\\", "\\\\");
        var location = GitRepositoryLocation.Resolve(linked);
        var context = new GitRepositoryContext(location, new Dictionary<string, string>(), CancellationToken.None);
        Assert.IsTrue(context.RequiresCli);
        var repository = context.Repository();
        Assert.AreEqual("refs/heads/feature", repository.Head!.CanonicalName);
        Assert.AreEqual("feature", repository.Head.FriendlyName);
        Assert.IsNotNull(repository.Head.Tip!.Sha);
        Assert.IsTrue(repository.Commits.Any());
        Assert.IsTrue(repository.Commits.First().Parents.Any());
        Assert.AreEqual(1, repository.Tags.Count());
        Assert.IsNotNull(repository.Tags.Single().Commit);
        Assert.IsNotNull(repository.Tags.Single().Annotation);
        Assert.AreEqual(1, repository.Stashes.Count());
        Assert.IsNotNull(repository.Stashes.Single().Index);
        Assert.IsNotNull(repository.Stashes.Single().WorkTree);
        Assert.IsTrue(repository.Configuration.Any(c => c.Key.Equals("extensions.relativeworktrees", StringComparison.OrdinalIgnoreCase)));
        var branch = repository.Branches.Single(b => b.CanonicalName == "refs/heads/feature");
        Assert.AreEqual(1, branch.BranchTrackingDetails.AheadBy);
        Assert.AreEqual(0, branch.BranchTrackingDetails.BehindBy);
        Assert.IsNotNull(branch.TrackedBranch);
        Assert.IsTrue(branch.Commits.Any());
        foreach (var source in new[] { "repository", "commits", "branches", "status", "tags", "stashes", "remotes", "worktrees" })
            Assert.IsTrue(WorktreeCompatibilityTests.Query($"select * from git.{source}('{path}')").Count > 0, source);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Sha from git.commits('{path}') where MessageShort = 'feature change'").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Sha from git.commits('{path}') where MessageShort like 'feature%'").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select CommitSha from git.filehistory('{path}', 'tracked.txt', 1)").Count);
    }

    [TestMethod]
    public void ExplicitUnsupportedNativeBackendReportsCapabilityError()
    {
        using var fixture = Create(out var linked);
        var source = new FileHistoryRowsSource(linked, "tracked.txt", 0, 1, static p => new Repository(p),
            RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: new Dictionary<string, string>
                { [GitHistoryBackendOptions.BackendSettingName] = "libgit2" }));
        var exception = Assert.ThrowsException<InvalidOperationException>(() => source.Chunks.SelectMany(c => c).ToArray());
        StringAssert.Contains(exception.Message, "relativeWorktrees");
    }

    internal static OfflineGitFixture Create(out string linked)
    {
        var fixture = OfflineGitFixture.Create();
        File.WriteAllText(Path.Combine(fixture.SeedPath, "tracked.txt"), "first\nsecond\nthird\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", "tracked.txt");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "tracked base");
        fixture.CreateStash("shared stash");
        OfflineGitFixture.RunGit(fixture.SeedPath, "tag", "-a", "annotated", "-m", "annotation message");
        linked = Path.Combine(fixture.Root, "relative żółw");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "--relative-paths", "-qb", "feature", linked);
        OfflineGitFixture.RunGit(linked, "branch", "--set-upstream-to=main", "feature");
        File.WriteAllText(Path.Combine(linked, "tracked.txt"), "inserted\nfirst\nsecond changed\nthird\n");
        OfflineGitFixture.RunGit(linked, "add", "tracked.txt");
        OfflineGitFixture.RunGit(linked, "commit", "-qm", "feature change");
        File.WriteAllText(Path.Combine(linked, "untracked.txt"), "worktree-specific");
        return fixture;
    }
}
