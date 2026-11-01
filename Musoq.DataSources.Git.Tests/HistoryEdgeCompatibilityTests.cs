using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class HistoryEdgeCompatibilityTests
{
    [DataTestMethod]
    [DataRow(27)]
    [DataRow(91)]
    public void ModernNestedTimeOrderMatchesNativeAcrossManyTiedMergeCommits(int seed)
    {
        using var fixture = OfflineGitFixture.Create();
        var random = new Random(seed);
        string[] expected;
        using (var repository = new Repository(fixture.SeedPath))
        {
            var tree = repository.Head.Tip.Tree;
            var nodes = new List<Commit>();
            var tips = new HashSet<Commit>();
            for (var index = 0; index < 120; index++)
            {
                var parents = index == 0 ? [] : new[] { nodes[random.Next(index)], nodes[random.Next(index)] }.DistinctBy(c => c.Sha).ToArray();
                var signature = new Signature("Graph", "graph@example.invalid", DateTimeOffset.FromUnixTimeSeconds(1900000000 + random.Next(20)));
                var commit = repository.ObjectDatabase.CreateCommit(signature, signature, "node " + index, tree, parents, false);
                foreach (var parent in parents) tips.Remove(parent);
                tips.Add(commit); nodes.Add(commit);
            }
            var mergeSignature = new Signature("Graph", "graph@example.invalid", DateTimeOffset.FromUnixTimeSeconds(1900000001));
            var merge = repository.ObjectDatabase.CreateCommit(mergeSignature, mergeSignature, "all tips", tree, tips, false);
            repository.Refs.UpdateTarget(repository.Head.Reference, merge.Id);
            using var fresh = new Repository(fixture.SeedPath);
            expected = fresh.Commits.Select(c => c.Sha).ToArray();
        }
        var linked = Path.Combine(fixture.Root, "modern graph");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "--relative-paths", "--detach", "-q", linked);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        CollectionAssert.AreEqual(expected, context.Commits().Select(c => c.Sha!).ToArray());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CliAndModernCommitOrdersMatchNativeOnSkewedMergeGraph(bool tied)
    {
        using var fixture = OfflineGitFixture.Create();
        string[] nativeDirect, nativeNested;
        var names = new Dictionary<string, string>();
        using (var repository = new Repository(fixture.SeedPath))
        {
            var tree = repository.Head.Tip.Tree;
            Commit Make(string name, int time, params Commit[] parents)
            {
                var signature = new Signature("Graph", "graph@example.invalid", DateTimeOffset.FromUnixTimeSeconds(1800000000 + time));
                var commit = repository.ObjectDatabase.CreateCommit(signature, signature, name, tree, parents, false);
                names[commit.Sha] = name;
                return commit;
            }
            var root = Make("root", 0);
            var left = Make("left", 20, root);
            var leftTip = Make("left tip", 55, left);
            var right = Make("right", 40, root);
            var rightTip = Make("right tip", tied ? 55 : 50, right);
            var merge = Make("merge", 10, leftTip, rightTip);
            var tip = Make("tip", 60, merge);
            repository.Refs.UpdateTarget(repository.Head.Reference, tip.Id);
            using var fresh = new Repository(fixture.SeedPath);
            nativeDirect = fresh.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time }).Select(c => c.Sha).ToArray();
            nativeNested = fresh.Commits.Select(c => c.Sha).ToArray();
        }
        var cli = new List<string>();
        new GitCliCommitReader().Read(fixture.SeedPath, new GitProjection(false, ["Sha"]), null,
            static p => new Repository(p), CancellationToken.None, row => { cli.Add(row.Sha); return true; });
        CollectionAssert.AreEqual(nativeDirect, cli);
        var linked = Path.Combine(fixture.Root, "modern graph");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "--relative-paths", "--detach", "-q", linked);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        CollectionAssert.AreEqual(nativeDirect, context.Rows<CommitEntity>("git.commits").Select(c => c.Sha!).ToArray());
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        var actualNested = context.Commits().Select(c => c.Sha!).ToArray();
        Assert.AreEqual(2L, GitCliProcessMetrics.Snapshot().StartedProcesses);
        CollectionAssert.AreEqual(nativeNested, actualNested, "native=" + string.Join(",", nativeNested.Select(s => names[s])) +
            "; cli=" + string.Join(",", actualNested.Select(s => names[s])));
    }

    [TestMethod]
    public void BackendSettingsKeepTheirScopesAndRequiredCliErrorsOfferExecutableGuidance()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var settings = new Dictionary<string, string> { [GitHistoryBackendOptions.BackendSettingName] = "invalid-unused-history-value" };
        Assert.AreEqual(4, new CommitsRowsSource(linked, static p => new Repository(p),
            RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: settings)).Chunks.SelectMany(c => c).Count());
        Assert.AreEqual(2, new WorktreesRowsSource(linked,
            RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: settings)).Chunks.SelectMany(c => c).Count());
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked),
            new Dictionary<string, string> { [GitReferenceBackendOptions.BackendSettingName] = "libgit2" }, CancellationToken.None);
        StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => context.Repository().Tags.ToArray()).Message, "relativeWorktrees");
        settings[GitHistoryBackendOptions.ExecutableSettingName] = "missing-git-for-required-worktree-discovery";
        var exception = Assert.ThrowsException<GitCliUnavailableException>(() => new WorktreesRowsSource(linked,
            RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: settings)).Chunks.SelectMany(c => c).ToArray());
        StringAssert.Contains(exception.Message, "GIT_EXECUTABLE");
        Assert.IsFalse(exception.Message.Contains("=libgit2", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ModernMainAndLinkedMetadataDirectoriesPreserveCheckoutWithTrailingSeparator()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        foreach (var (root, expected) in new[] { (fixture.SeedPath, 0), (linked, 1) })
        {
            var location = GitRepositoryLocation.Resolve(root);
            var metadata = (location.GitDirectory + Path.DirectorySeparatorChar).Replace("\\", "\\\\");
            Assert.AreEqual(expected, WorktreeCompatibilityTests.Query($"select FilePath from git.status('{metadata}')").Count);
            Assert.AreEqual(location.WorktreeRoot, GitRepositoryLocation.Resolve(location.GitDirectory + Path.DirectorySeparatorChar).WorktreeRoot);
            if (OperatingSystem.IsWindows())
            {
                var upper = (location.GitDirectory.ToUpperInvariant() + Path.DirectorySeparatorChar).Replace("\\", "\\\\");
                Assert.AreEqual(expected, WorktreeCompatibilityTests.Query($"select FilePath from git.status('{upper}')").Count);
            }
        }
    }

    [TestMethod]
    public void UnbornModernRepositoriesReturnEmptyHistoriesWithoutMaskingMissingObjects()
    {
        using var fixture = OfflineGitFixture.Create();
        var root = Path.Combine(fixture.Root, "unborn");
        Directory.CreateDirectory(root);
        OfflineGitFixture.RunGit(root, "init", "-q", "--initial-branch=main");
        OfflineGitFixture.RunGit(root, "config", "core.repositoryformatversion", "1");
        OfflineGitFixture.RunGit(root, "config", "extensions.relativeWorktrees", "true");
        var escaped = root.Replace("\\", "\\\\");
        Assert.AreEqual(0, WorktreeCompatibilityTests.Query($"select Sha from git.commits('{escaped}')").Count);
        Assert.AreEqual(0, WorktreeCompatibilityTests.Query($"select c.Sha from git.repository('{escaped}') r cross apply r.Commits c").Count);
        Assert.AreEqual(0, WorktreeCompatibilityTests.Query($"select CommitSha from git.filehistory('{escaped}', '*.txt')").Count);
        File.WriteAllText(Path.Combine(root, ".git", "refs", "heads", "main"), new string('1', 40) + "\n");
        Directory.CreateDirectory(Path.Combine(root, ".git", "worktrees"));
        Assert.ThrowsException<InvalidOperationException>(() => new GitRepositoryContext(GitRepositoryLocation.Resolve(root),
            new Dictionary<string, string>(), CancellationToken.None).Commits(topological: true).ToArray());
    }

    [TestMethod]
    public void LargeStandardInputCompletesAndCancelledWritersReleaseProcesses()
    {
        using var fixture = OfflineGitFixture.Create();
        string sha;
        using (var repository = new Repository(fixture.SeedPath)) sha = repository.Head.Tip.Sha;
        var request = new GitCliProcessRequest(fixture.SeedPath, "git", string.Empty,
            ["log", "--no-walk=sorted", "--stdin", "--format=%H"],
            StandardInput: string.Concat(Enumerable.Repeat(sha + "\n", 20000)));
        using (var process = GitCliProcess.Start(request, CancellationToken.None))
        {
            using var reader = new StreamReader(process.StandardOutput);
            Assert.AreEqual(sha, reader.ReadToEnd().Trim());
            process.Complete();
        }
        using var cancelled = new CancellationTokenSource();
        using var measurement = GitCliProcessMetrics.BeginMeasurement();
        using (var process = GitCliProcess.Start(request, cancelled.Token))
        {
            cancelled.Cancel();
            Assert.ThrowsException<OperationCanceledException>(process.Complete);
        }
        Assert.AreEqual(1L, GitCliProcessMetrics.Snapshot().StoppedProcesses);
    }

    [TestMethod]
    public void EmptyStashesAndGitfileInputsWorkAcrossCheckoutSources()
    {
        using var fixture = OfflineGitFixture.Create();
        File.WriteAllText(Path.Combine(fixture.SeedPath, "lines.txt"), "one\ntwo\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", ".");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "lines");
        var linked = Path.Combine(fixture.Root, "linked");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "-qb", "feature", linked);
        var pointer = Path.Combine(linked, ".git").Replace("\\", "\\\\");
        Assert.AreEqual(0, WorktreeCompatibilityTests.Query($"select Sha from git.stashes('{pointer}')").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select Path from git.repository('{pointer}')").Count);
        Assert.AreEqual(2, WorktreeCompatibilityTests.Query($"select l.Content from git.blame('{pointer}', 'lines.txt') h cross apply h.Lines l").Count);
        var absolute = Path.Combine(linked, "lines.txt").Replace("\\", "\\\\");
        var metadata = GitRepositoryLocation.Resolve(linked).GitDirectory.Replace("\\", "\\\\");
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select CommitSha from git.filehistory('{metadata}', '{absolute}')").Count);
    }

    [TestMethod]
    [DoNotParallelize]
    public void InheritedRepositoryEnvironmentDoesNotRedirectCliWorktreeReads()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var overrides = new Dictionary<string, string>
        {
            ["GIT_DIR"] = fixture.RemotePath, ["GIT_COMMON_DIR"] = fixture.RemotePath,
            ["GIT_WORK_TREE"] = fixture.Root, ["GIT_INDEX_FILE"] = Path.Combine(fixture.Root, "foreign-index")
        };
        var original = overrides.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var entry in overrides) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
            Assert.AreEqual("feature change", context.Commits(topological: true).First().MessageShort);
            Assert.AreEqual("untracked.txt", context.Status().Single().FilePath);
            Assert.AreEqual(2, new WorktreesRowsSource(linked, RuntimeV2TestContexts.CreateExecutionContext()).Chunks.SelectMany(c => c).Count());
        }
        finally
        {
            foreach (var entry in original) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }

    [TestMethod]
    public void ModernDirectShaLookupIncludesUnreachableCommitsAndSelfKeepsRichMetadata()
    {
        using var fixture = OfflineGitFixture.Create();
        string sha;
        using (var repository = new Repository(fixture.SeedPath))
        {
            var signature = new Signature("Lookup", "lookup@example.invalid", DateTimeOffset.FromUnixTimeSeconds(1800000000));
            sha = repository.ObjectDatabase.CreateCommit(signature, signature, "unreachable", repository.Head.Tip.Tree, [], false).Sha;
        }
        var linked = Path.Combine(fixture.Root, "modern");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "--relative-paths", "--detach", "-q", linked);
        var path = linked.Replace("\\", "\\\\");
        Assert.AreEqual("unreachable", WorktreeCompatibilityTests.Query($"select MessageShort from git.commits('{path}') where Sha = '{sha}'")[0].ReadValue(0));
        Assert.AreEqual("Musoq Offline Test", WorktreeCompatibilityTests.Query($"select c.Self.Author from git.commits('{path}') c take 1")[0].ReadValue(0));
    }

    [TestMethod]
    public void ModernGoneUpstreamMatchesNativeTrackingSnapshot()
    {
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "upstream");
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "tracked");
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "--set-upstream-to=upstream", "tracked");
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "-D", "upstream");
        bool tracking;
        string? upstream;
        int? ahead, behind;
        using (var repository = new Repository(fixture.SeedPath))
        {
            var branch = repository.Branches["tracked"];
            tracking = branch.IsTracking; upstream = branch.UpstreamBranchCanonicalName;
            ahead = branch.TrackingDetails.AheadBy; behind = branch.TrackingDetails.BehindBy;
        }
        var linked = Path.Combine(fixture.Root, "modern");
        OfflineGitFixture.RunGit(fixture.SeedPath, "worktree", "add", "--relative-paths", "--detach", "-q", linked);
        var context = new GitRepositoryContext(GitRepositoryLocation.Resolve(linked), new Dictionary<string, string>(), CancellationToken.None);
        var actual = context.Branches().Single(b => b.FriendlyName == "tracked");
        Assert.AreEqual(tracking, actual.IsTracking);
        Assert.AreEqual(upstream, actual.UpstreamBranchCanonicalName);
        Assert.AreEqual(ahead, actual.BranchTrackingDetails.AheadBy);
        Assert.AreEqual(behind, actual.BranchTrackingDetails.BehindBy);
    }

    [TestMethod]
    public void CliBranchCandidateCountsAgainstUpstreamInsteadOfHead()
    {
        using var fixture = OfflineGitFixture.Create();
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "upstream");
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "tracked");
        OfflineGitFixture.RunGit(fixture.SeedPath, "branch", "--set-upstream-to=upstream", "tracked");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "--allow-empty", "-qm", "head diverges");
        var rows = new List<GitBranchRecord>();
        new GitCliBranchReader().Read(fixture.SeedPath, GitProjection.NotAccepted, static p => new Repository(p),
            CancellationToken.None, row => { rows.Add(row); return true; });
        var tracked = rows.Single(r => r.FriendlyName == "tracked");
        Assert.AreEqual(0, tracked.AheadBy);
        Assert.AreEqual(0, tracked.BehindBy);
        Assert.IsNull(rows.Single(r => r.FriendlyName == "main").AheadBy);
    }
}
