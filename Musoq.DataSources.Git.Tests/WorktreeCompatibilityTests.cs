using LibGit2Sharp;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class WorktreeCompatibilityTests
{
    [TestMethod]
    public void LinkedWorktreeKeepsHeadIndexStatusAndNestedIdentity()
    {
        using var fixture = OfflineGitFixture.Create();
        File.WriteAllText(Path.Combine(fixture.SeedPath, "tracked.txt"), "main\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", "tracked.txt");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "main content");
        var linked = Path.Combine(fixture.Root, "linked żółw");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "-qb", "feature", linked);
        File.WriteAllText(Path.Combine(linked, "tracked.txt"), "feature\n");
        OfflineGitFixture.RunGit(linked, "add", "tracked.txt");
        OfflineGitFixture.RunGit(linked, "commit", "-qm", "feature content");
        File.WriteAllText(Path.Combine(linked, "only-linked.txt"), "new");
        var escaped = linked.Replace("\\", "\\\\");
        var head = Query($"select Head.CanonicalName, WorkingDirectory, Path from git.repository('{escaped}')");
        Assert.AreEqual("refs/heads/feature", head[0].ReadValue(0));
        Assert.AreEqual(Path.GetFullPath(linked), ((string)head[0].ReadValue(1)).TrimEnd('\\', '/'));
        Assert.IsTrue(((string)head[0].ReadValue(2)).Contains("worktrees"));
        Assert.AreEqual(1, Query($"select FilePath from git.status('{escaped}')").Count);
        Assert.AreEqual(0, Query($"select FilePath from git.status('{fixture.SeedPath.Replace("\\", "\\\\")}')").Count);
        Assert.AreEqual("feature content\n", Query($"select Message from git.commits('{escaped}') take 1")[0].ReadValue(0));
        using var repository = new Repository(linked);
        var metadata = repository.Info.Path.Replace("\\", "\\\\");
        Assert.AreEqual(1, Query($"select CommitSha from git.filehistory('{metadata}', 'tracked.txt', 1)").Count);
        var location = GitRepositoryLocation.Resolve(repository.Info.Path);
        Assert.AreEqual(Path.GetFullPath(linked), location.WorktreeRoot);
    }

    [TestMethod]
    public void BrokenPointerAndNonRepositoryFailWithoutOpeningParent()
    {
        using var fixture = OfflineGitFixture.Create();
        var path = Path.Combine(fixture.Root, "broken");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, ".git"), "gitdir: ../missing\n");
        Assert.ThrowsException<InvalidOperationException>(() => GitRepositoryLocation.Resolve(path));
        Assert.ThrowsException<InvalidOperationException>(() => GitRepositoryLocation.Resolve(fixture.Root));
        var bare = GitRepositoryLocation.Resolve(fixture.RemotePath);
        Assert.IsNull(bare.WorktreeRoot);
    }

    internal static Musoq.Evaluator.Tables.Table Query(string sql) => InstanceCreatorHelpers.CompileForExecution(
        sql, Guid.NewGuid().ToString(), new GitSchemaProvider(),
        EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables()).Run();
}
