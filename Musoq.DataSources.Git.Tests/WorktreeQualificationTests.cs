using System.Security.Cryptography;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class WorktreeQualificationTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowsLongPathsInsideWorktreesKeepStatus(bool relative)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "config", "core.longpaths", "true");
        var linked = Path.Combine(fixture.Root, "long-files");
        var arguments = new List<string> { "worktree", "add", "-q", "--detach" };
        if (relative) arguments.Add("--relative-paths");
        arguments.Add(linked);
        OfflineGitFixture.RunGit(fixture.SeedPath, arguments.ToArray());
        var contentDirectory = linked;
        for (var index = 0; index < 6; index++) contentDirectory = Path.Combine(contentDirectory, "long-path-" + new string('x', 35));
        Directory.CreateDirectory(contentDirectory);
        File.WriteAllText(Path.Combine(contentDirectory, "long.txt"), "untracked");
        var path = linked.Replace("\\", "\\\\");
        var rows = WorktreeCompatibilityTests.Query($"select FilePath from git.status('{path}')");
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(Path.GetRelativePath(linked, Path.Combine(contentDirectory, "long.txt")).Replace('\\', '/'), rows[0].ReadValue(0));
    }

    [TestMethod]
    public void NestedTagsWorktreeConfigurationAndQueriesPreserveRepositoryState()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        OfflineGitFixture.RunGit(linked, "tag", "-a", "nested", "annotated", "-m", "outer annotation");
        OfflineGitFixture.RunGit(linked, "config", "extensions.worktreeConfig", "true");
        OfflineGitFixture.RunGit(linked, "config", "--worktree", "worktree.marker", "linked");
        OfflineGitFixture.RunGit(fixture.SeedPath, "config", "--worktree", "worktree.marker", "main");
        var before = Fingerprint(fixture);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        var repository = context.Repository();
        var nested = repository.Tags.Single(t => t.FriendlyName == "nested");
        Assert.IsNotNull(nested.Commit);
        Assert.AreEqual(nested.Commit.Sha, nested.TargetSha);
        StringAssert.Contains(nested.Message, "outer annotation");
        Assert.AreEqual("linked", repository.Configuration.Single(c => c.Key == "worktree.marker").Value);
        Assert.IsFalse(repository.Configuration.Any(c => c.ConfigurationLevel == "App"));
        _ = repository.Commits.ToArray(); _ = repository.Branches.ToArray(); _ = context.Status().ToArray();
        _ = new GitLibrary().PatchBetween(repository, context.CommitFrom("HEAD~1")!, context.CommitFrom("HEAD")!).Single().Content;
        _ = context.Blame("tracked.txt", "HEAD").SelectMany(h => h.Lines).ToArray();
        Assert.AreEqual(before, Fingerprint(fixture));
    }

    [TestMethod]
    public void RelativeGitfilesBareDotGitAndSubmoduleStyleSeparateMetadataAreAccepted()
    {
        using var fixture = OfflineGitFixture.Create();
        var bare = Path.Combine(fixture.Root, ".git");
        OfflineGitFixture.RunGit(fixture.Root, "init", "--bare", "-q", bare);
        Assert.IsNull(GitRepositoryLocation.Resolve(bare).WorktreeRoot);
        Assert.IsTrue(new WorktreesRowsSource(bare, RuntimeV2TestContexts.CreateExecutionContext()).Chunks.SelectMany(c => c).Single().IsBare);
        var separate = Path.Combine(fixture.Root, "separate");
        var metadata = Path.Combine(fixture.Root, "module-metadata");
        OfflineGitFixture.RunGit(fixture.Root, "init", "-q", "--separate-git-dir", metadata, separate);
        Assert.IsTrue(File.Exists(Path.Combine(separate, ".git")));
        File.SetAttributes(Path.Combine(separate, ".git"), FileAttributes.Normal);
        File.WriteAllText(Path.Combine(separate, ".git"), "gitdir:\t../module-metadata\n");
        Assert.AreEqual(Path.GetFullPath(metadata), GitRepositoryLocation.Resolve(separate).GitDirectory);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Path from git.repository('{separate.Replace("\\", "\\\\")}')").Count);
    }

    [TestMethod]
    public void SmallWindowsCancelAndDisposeTheOnlyEnumerationProcess()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        var rows = new WorktreesRowsSource(linked, RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: new Musoq.Schema.Optimization.SourceExecutionPlan
                { Identity = Musoq.Schema.Optimization.SourceIdentity.Empty, AcceptedTake = 1 })).Chunks.SelectMany(c => c).ToArray();
        Assert.AreEqual(1, rows.Length);
        var metrics = GitCliProcessMetrics.Snapshot();
        Assert.AreEqual(1L, metrics.StartedProcesses);
        Assert.AreEqual(1L, metrics.CompletedProcesses + metrics.StoppedProcesses);
        using var cancellation = new CancellationTokenSource();
        using var enumerator = new GitWorktreeReader().Read(linked, GitHistoryBackendOptions.Default, cancellation.Token).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => enumerator.MoveNext());
    }

    private static string Fingerprint(OfflineGitFixture fixture)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(fixture.Root, file)));
            hash.AppendData(File.ReadAllBytes(file));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
