using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class NestedHistoryStreamingTests
{
    [TestMethod]
    public void BranchSpecificHistoryIsFreshAndCanBeAbandoned()
    {
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "checkout", "-qb", "feature");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "feature one");
        using var native = new Repository(fixture.SeedPath);
        var repository = new RepositoryEntity(native);
        var library = new GitLibrary();
        var branch = library.BranchFrom(repository, "refs/heads/feature");
        var commits = library.GetBranchSpecificCommits(repository, branch);
        Assert.AreEqual(1, commits.Count());
        using (var enumerator = commits.GetEnumerator()) Assert.IsTrue(enumerator.MoveNext());
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "feature two");
        Assert.AreEqual(2, commits.Count());
    }

    [TestMethod]
    public void ExplicitModernExtensionWithoutRegistrationsUsesValidatedCliFallback()
    {
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "config", "core.repositoryformatversion", "1");
        OfflineGitFixture.RunGit(fixture.SeedPath, "config", "extensions.relativeWorktrees", "true");
        var path = fixture.SeedPath.Replace("\\", "\\\\");
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select c.Sha from git.repository('{path}') r cross apply r.Commits c").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Sha from git.commits('{path}')").Count);
    }

    [TestMethod]
    public void ParentProjectionFallsBackWhenGitIsUnavailableAndHonorsNativeSelection()
    {
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "child");
        foreach (var backend in new[] { "auto", "libgit2" })
        {
            var settings = new Dictionary<string, string> { [GitHistoryBackendOptions.BackendSettingName] = backend,
                [GitHistoryBackendOptions.ExecutableSettingName] = "missing-git-for-parent-fallback" };
            var plan = new GitSchema().TryPlanSource("commits", new Musoq.Schema.Optimization.SourcePlanRequest
            {
                Identity = Musoq.Schema.Optimization.SourceIdentity.Empty,
                RequiredColumns = [new Musoq.Schema.Optimization.SourceColumnRef("Parents")],
                SourceRuntimeSettings = settings
            }, fixture.SeedPath).ExecutionPlan;
            var source = new CommitsRowsSource(fixture.SeedPath, static p => new Repository(p),
                RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: settings, executionPlan: plan));
            Assert.AreEqual(1, source.Chunks.SelectMany(c => c).SelectMany(c => c.Parents).Count());
        }
    }

    [TestMethod]
    public void NativeNestedCollectionsAreFreshAndCanBeAbandoned()
    {
        using var fixture = OfflineGitFixture.Create();
        using var native = new Repository(fixture.SeedPath);
        var repository = new RepositoryEntity(native);
        var commits = repository.Commits;
        Assert.AreEqual(1, commits.Count());
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "next");
        Assert.AreEqual(2, commits.Count());
        using (var enumerator = commits.GetEnumerator()) Assert.IsTrue(enumerator.MoveNext());
        var main = repository.Branches.Single(b => b.FriendlyName == "main");
        Assert.AreEqual(2, main.Commits.Count());
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "third");
        Assert.AreEqual(3, main.Commits.Count());
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "fresh");
        Assert.AreEqual(2, repository.Branches.Count(b => !b.IsRemote));
    }

    [TestMethod]
    public void ParentIdentityRetainsLazyRichPropertiesAfterProducerEnds()
    {
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "child");
        var rows = WorktreeCompatibilityTests.Query($"select p.Sha, p.Message, p.Author from git.commits('{fixture.SeedPath.Replace("\\", "\\\\")}') c cross apply c.Parents p");
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("seed\n", rows[0].ReadValue(1));
        Assert.AreEqual("Musoq Offline Test", rows[0].ReadValue(2));
    }
}
