using System.Diagnostics;
using System.Globalization;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class FileHistoryRowsSourceTests
{
    [TestMethod]
    public void FileHistory_StreamsHistoricalChangesAndPreservesWindows()
    {
        using var repository = GitFixture.Create();
        repository.Commit("Root.cs", "class Root { }", "root");
        repository.Commit("src/a.cs", "class A { }", "root");
        repository.Commit("docs/readme.md", "root", "add docs");
        repository.Commit("src/żółw.cs", "class Turtle { }", "unicode");
        repository.Move("src/a.cs", "src/b.cs", "rename source");
        repository.Commit("docs/readme.md", null, "delete docs");
        repository.Commit("src/b.cs", "class B { }\n", "modify source");
        repository.CopyWithSourceEdit("src/b.cs", "src/c.cs", "class B { }\n// updated\n", "class B { }\n", "copy source");
        var mergeSha = repository.CreateMerge();

        var all = Read(repository.Path, "*", 0, int.MaxValue);

        Assert.IsTrue(all.Any(row => row.ChangeType == "Added" && row.FilePath == "src/a.cs"));
        Assert.IsTrue(all.Any(row => row.ChangeType == "Added" && row.FilePath == "Root.cs"));
        Assert.IsTrue(all.Any(row => row.ChangeType == "Added" && row.FilePath == "src/żółw.cs"));
        Assert.IsTrue(all.Any(row => row.ChangeType == "Deleted" && row.FilePath == "docs/readme.md"));
        Assert.IsTrue(all.Any(row => row.ChangeType == "Renamed" && row.FilePath == "src/b.cs" && row.OldPath == "src/a.cs"));
        Assert.IsTrue(all.Any(row => row.ChangeType == "Copied" && row.FilePath == "src/c.cs" && row.OldPath == "src/b.cs"));
        Assert.IsFalse(all.Any(row => row.CommitSha == mergeSha));
        Assert.IsTrue(all.All(row => row.FilePath is not null));

        var renamed = Read(repository.Path, "src/b.cs", 0, int.MaxValue);
        Assert.IsTrue(renamed.Any(row => row.FilePath == "src/a.cs" || row.OldPath == "src/a.cs"));

        var one = Read(repository.Path, "*", 1, 1);
        Assert.HasCount(1, one);
        Assert.AreEqual(all[1].CommitSha, one[0].CommitSha);
        Assert.AreEqual(all[1].FilePath, one[0].FilePath);

        var oldest = Read(repository.Path, "*", 0, -1);
        Assert.HasCount(1, oldest);
        Assert.AreEqual(all[^1].CommitSha, oldest[0].CommitSha);

        var compatibilityRows = Read(repository.Path, "*", 0, 1, backend: "libgit2");
        Assert.HasCount(1, compatibilityRows);
    }

    [TestMethod]
    public void FileHistory_RejectsNegativeSkipAndDoesNotStartGitForZeroTake()
    {
        using var repository = GitFixture.Create();
        repository.Commit("root.cs", "class Root { }", "root");
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                ["GIT_HISTORY_BACKEND"] = "git-cli",
                ["GIT_EXECUTABLE"] = "does-not-exist-musoq-git"
            });

        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new FileHistoryRowsSource(repository.Path, "*", -1, 1, path => new Repository(path), context));

        var zeroTake = new FileHistoryRowsSource(repository.Path, "*", 0, 0, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk)
            .ToArray();
        Assert.IsEmpty(zeroTake);
    }

    [TestMethod]
    public void DirectSources_EmitDetachedRowsAndReleaseRepositoryHandles()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("root.cs", "class Root { }", "root");
        File.WriteAllText(Path.Combine(fixture.Path, "untracked.cs"), "class Untracked { }");
        var context = RuntimeV2TestContexts.CreateExecutionContext();

        var commits = new CommitsRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();
        var branches = new BranchesRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();
        var repositories = new RepositoryRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();
        var status = new StatusRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.HasCount(1, commits);
        Assert.AreEqual("root", commits[0].MessageShort);
        Assert.IsEmpty(commits[0].Parents);
        Assert.IsTrue(branches[0].Tip?.Sha == commits[0].Sha);
        Assert.AreEqual(fixture.Path + Path.DirectorySeparatorChar, repositories[0].WorkingDirectory);
        Assert.IsTrue(status.Any(row => row.FilePath == "untracked.cs"));

        var movedPath = fixture.Path + "-moved";
        Directory.Move(fixture.Path, movedPath);
        Assert.IsTrue(Directory.Exists(movedPath));
        foreach (var file in Directory.EnumerateFiles(movedPath, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(movedPath, recursive: true);
    }

    [TestMethod]
    public void NestedSnapshots_AreCachedAfterTheirShortLivedRepositoryScopesClose()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("first.cs", "class First { }", "first");
        fixture.Commit("second.cs", "class Second { }", "second");
        fixture.RunGit("tag", "-a", "cache-tag", "-m", "cache annotation");
        var context = RuntimeV2TestContexts.CreateExecutionContext();
        var repository = new RepositoryRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).Single();
        var commits = repository.Commits.ToArray();
        var latest = commits.Single(commit => commit.MessageShort == "second");
        var parents = latest.Parents.ToArray();
        var head = repository.Head!;
        var tip = head.Tip!;
        var branchCommits = head.Commits.ToArray();
        var tag = repository.Tags.Single(value => value.FriendlyName == "cache-tag");
        var taggedCommit = tag.Commit!;
        var library = new GitLibrary();
        var patch = library.PatchBetween(repository, parents.Single(), latest).Single();
        var patchContent = patch.Content;
        var difference = library.DifferenceBetween(repository, parents.Single(), latest).Single();
        var differenceContent = difference.NewContent;

        Assert.AreSame(repository.Commits, repository.Commits);
        Assert.AreSame(latest.Parents, latest.Parents);
        Assert.AreSame(head.Tip, head.Tip);
        Assert.AreSame(head.Commits, head.Commits);
        Assert.AreSame(tag.Commit, tag.Commit);
        Assert.AreEqual(patchContent, patch.Content);
        Assert.AreEqual(differenceContent, difference.NewContent);

        var movedPath = fixture.Path + "-nested-moved";
        Directory.Move(fixture.Path, movedPath);
        Assert.AreEqual(2, commits.Length);
        Assert.HasCount(1, parents);
        Assert.AreEqual(latest.Sha, tip.Sha);
        Assert.IsTrue(branchCommits.Any(commit => commit.Sha == latest.Sha));
        Assert.AreEqual(latest.Sha, taggedCommit.Sha);
        Assert.AreEqual(patchContent, patch.Content);
        Assert.AreEqual(differenceContent, difference.NewContent);
        foreach (var file in Directory.EnumerateFiles(movedPath, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(movedPath, recursive: true);
    }

    [TestMethod]
    public void ParentBranch_ReportsRepositoryFailuresInsteadOfGuessingADefaultBranch()
    {
        var branch = new BranchEntity(
            Path.Combine(Path.GetTempPath(), "musoq-missing-" + Guid.NewGuid().ToString("N")),
            "missing",
            "refs/heads/missing",
            false,
            false,
            false,
            null,
            null,
            null,
            "0123456789012345678901234567890123456789",
            null,
            null);

        var exception = Assert.ThrowsException<InvalidOperationException>(() => _ = branch.ParentBranch);

        StringAssert.Contains(exception.Message, "could not determine the parent branch");
        Assert.IsInstanceOfType(exception.InnerException, typeof(LibGit2SharpException));
    }

    [TestMethod]
    public void CommitsSource_OnlyMaterializesProjectedMetadataAndHonorsAcceptedWindow()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("first.cs", "class First { }", "first");
        fixture.Commit("second.cs", "class Second { }", "second");
        var plan = new SourceExecutionPlan
        {
            Identity = new SourceIdentity("git", "git", "git", "commits"),
            AcceptedSkip = 1,
            AcceptedTake = 1,
            Properties = new Dictionary<string, object?>
            {
                [GitSourcePlanner.ProjectionPropertyName] = new GitProjection(true, [nameof(CommitEntity.MessageShort)])
            }
        };
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan);

        var rows = new CommitsRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.HasCount(1, rows);
        Assert.AreEqual("first", rows[0].MessageShort);
        Assert.IsNull(rows[0].Message);
        Assert.IsNull(rows[0].Author);
        Assert.IsNotNull(rows[0].Sha, "The compact commit identity is retained for nested/library APIs.");
    }

    [TestMethod]
    public void CommitsSource_AppliesInAndNullPredicatesBeforeCreatingProjectedRows()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("first.cs", "class First { }", "first");
        fixture.Commit("second.cs", "class Second { }", "second");
        using var repository = new Repository(fixture.Path);
        var secondSha = repository.Commits.Single(commit => commit.MessageShort == "second").Sha;
        var predicate = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            new SourcePredicateIn(
                new SourcePredicateColumn(new SourceColumnRef(nameof(CommitEntity.Sha))),
                [new SourcePredicateLiteral(secondSha)]),
            new SourcePredicateNullCheck(
                new SourcePredicateColumn(new SourceColumnRef(nameof(CommitEntity.Author))),
                IsNegated: true));
        var plan = new GitSchema().TryPlanSource("commits", new SourcePlanRequest
        {
            Identity = new SourceIdentity("git", "git", "git", "commits"),
            RequiredColumns = [new SourceColumnRef(nameof(CommitEntity.MessageShort))],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = []
        }, fixture.Path).ExecutionPlan;
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan);

        var rows = new CommitsRowsSource(fixture.Path, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.HasCount(1, rows);
        Assert.AreEqual(secondSha, rows[0].Sha);
        Assert.AreEqual("second", rows[0].MessageShort);
        Assert.AreEqual("Musoq Test", rows[0].Author, "The predicate dependency is retained in the physical snapshot.");
        Assert.IsNull(rows[0].Message, "An unprojected expensive field was not materialized.");
    }

    [TestMethod]
    public void ReferenceAndStatusSources_PhysicallyPruneUnprojectedNestedAndStateFields()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("projection.cs", "class Projection { }", "projection");
        fixture.RunGit("tag", "-a", "projection-tag", "-m", "annotation");
        File.WriteAllText(Path.Combine(fixture.Path, "untracked.cs"), "class New { }");

        var branch = new BranchesRowsSource(fixture.Path, path => new Repository(path),
                RuntimeV2TestContexts.CreateExecutionContext(executionPlan: ProjectionPlan("branches", nameof(BranchEntity.FriendlyName))))
            .Chunks.SelectMany(static chunk => chunk).Single();
        var tag = new TagsRowsSource(fixture.Path, path => new Repository(path),
                RuntimeV2TestContexts.CreateExecutionContext(executionPlan: ProjectionPlan("tags", nameof(TagEntity.FriendlyName))))
            .Chunks.SelectMany(static chunk => chunk).Single();
        var status = new StatusRowsSource(fixture.Path, path => new Repository(path),
                RuntimeV2TestContexts.CreateExecutionContext(executionPlan: ProjectionPlan("status", nameof(StatusEntity.FilePath))))
            .Chunks.SelectMany(static chunk => chunk).Single();

        Assert.AreEqual("master", branch.FriendlyName);
        Assert.IsNotNull(branch.Tip, "Nested identity is preserved because runtime-v2 can omit the intermediate dependency.");
        Assert.AreEqual("projection-tag", tag.FriendlyName);
        Assert.IsNotNull(tag.Annotation, "Tag annotation capability is retained for an unreported nested projection.");
        Assert.IsNotNull(tag.Commit, "Tag target identity is retained for Commit.Sha.");
        Assert.IsNull(tag.Message, "The public annotation message is not materialized outside the accepted projection.");
        Assert.AreEqual("untracked.cs", status.FilePath);
        Assert.AreEqual(string.Empty, status.State);
        Assert.AreEqual(string.Empty, status.IndexStatus);
        Assert.AreEqual(string.Empty, status.WorkDirStatus);
    }

    [TestMethod]
    public void CommitReaders_HaveParityForProjectionShapedRows()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("first.cs", "class First { }", "first");
        fixture.Commit("second.cs", "class Second { }", "second");
        var projection = new GitProjection(
            true,
            [nameof(CommitEntity.Sha), nameof(CommitEntity.MessageShort), nameof(CommitEntity.Author)]);
        var cli = new List<GitCommitRecord>();
        var libGit2 = new List<GitCommitRecord>();

        GitOperationReaders.CliCommits.Read(
            fixture.Path,
            projection,
            directSha: null,
            path => new Repository(path),
            CancellationToken.None,
            record =>
            {
                cli.Add(record);
                return true;
            });
        GitOperationReaders.LibGit2Commits.Read(
            fixture.Path,
            projection,
            directSha: null,
            path => new Repository(path),
            CancellationToken.None,
            record =>
            {
                libGit2.Add(record);
                return true;
            });

        CollectionAssert.AreEqual(
            libGit2.Select(record => (record.Sha, record.MessageShort, record.Author)).ToArray(),
            cli.Select(record => (record.Sha, record.MessageShort, record.Author)).ToArray());
        Assert.IsTrue(cli.All(record => record.Message is null && record.AuthorEmail is null),
            "Neither backend should materialize fields outside the accepted projection.");
    }

    [TestMethod]
    public void BranchReaders_HaveParityForLocalReferenceMetadata()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("branch.cs", "class Branch { }", "branch");
        var cli = new List<GitBranchRecord>();
        var libGit2 = new List<GitBranchRecord>();

        GitOperationReaders.CliBranches.Read(
            fixture.Path,
            GitProjection.NotAccepted,
            path => new Repository(path),
            CancellationToken.None,
            record =>
            {
                cli.Add(record);
                return true;
            });
        GitOperationReaders.Branches.Read(
            fixture.Path,
            GitProjection.NotAccepted,
            path => new Repository(path),
            CancellationToken.None,
            record =>
            {
                libGit2.Add(record);
                return true;
            });

        CollectionAssert.AreEqual(
            libGit2.Select(record => (record.FriendlyName, record.CanonicalName, record.IsRemote, record.IsCurrentRepositoryHead, record.TipSha)).ToArray(),
            cli.Select(record => (record.FriendlyName, record.CanonicalName, record.IsRemote, record.IsCurrentRepositoryHead, record.TipSha)).ToArray());
    }

    [TestMethod]
    public void TagReaders_HaveParityForLightweightAndAnnotatedTags()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("tag.cs", "class Tag { }", "tag target");
        fixture.RunGit("tag", "lightweight");
        fixture.RunGit("tag", "-a", "annotated", "-m", "tag message");
        var cli = new List<GitTagRecord>();
        var libGit2 = new List<GitTagRecord>();

        GitOperationReaders.CliTags.Read(fixture.Path, GitProjection.NotAccepted, path => new Repository(path), CancellationToken.None, record =>
        {
            cli.Add(record);
            return true;
        });
        GitOperationReaders.Tags.Read(fixture.Path, GitProjection.NotAccepted, path => new Repository(path), CancellationToken.None, record =>
        {
            libGit2.Add(record);
            return true;
        });

        CollectionAssert.AreEquivalent(
            libGit2.Select(record => (record.FriendlyName, record.CanonicalName, record.IsAnnotated, record.CommitSha)).ToArray(),
            cli.Select(record => (record.FriendlyName, record.CanonicalName, record.IsAnnotated, record.CommitSha)).ToArray());
        Assert.AreEqual("tag message\n", cli.Single(record => record.FriendlyName == "annotated").Message);
    }

    [TestMethod]
    public void RemoteReaders_HaveParityForLocalConfiguration()
    {
        using var fixture = GitFixture.Create();
        fixture.RunGit("remote", "add", "origin", "https://example.invalid/fetch.git");
        fixture.RunGit("remote", "set-url", "--push", "origin", "ssh://example.invalid/push.git");
        var cli = new List<GitRemoteRecord>();
        var libGit2 = new List<GitRemoteRecord>();

        GitOperationReaders.CliRemotes.Read(fixture.Path, GitProjection.NotAccepted, path => new Repository(path), CancellationToken.None, record =>
        {
            cli.Add(record);
            return true;
        });
        GitOperationReaders.Remotes.Read(fixture.Path, GitProjection.NotAccepted, path => new Repository(path), CancellationToken.None, record =>
        {
            libGit2.Add(record);
            return true;
        });

        CollectionAssert.AreEqual(libGit2.ToArray(), cli.ToArray());
    }

    [TestMethod]
    public void StatusReaders_HaveParityForUntrackedAndModifiedFiles()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("tracked.cs", "class Original { }", "initial");
        fixture.RunGit("mv", "tracked.cs", "renamed.cs");
        File.WriteAllText(Path.Combine(fixture.Path, "renamed.cs"), "class Changed { }");
        File.WriteAllText(Path.Combine(fixture.Path, "untracked.cs"), "class New { }");
        var cli = new List<GitStatusRecord>();
        var libGit2 = new List<GitStatusRecord>();

        GitOperationReaders.CliStatus.Read(fixture.Path, path => new Repository(path), CancellationToken.None, record =>
        {
            cli.Add(record);
            return true;
        });
        GitOperationReaders.Status.Read(fixture.Path, path => new Repository(path), CancellationToken.None, record =>
        {
            libGit2.Add(record);
            return true;
        });

        CollectionAssert.AreEquivalent(libGit2.ToArray(), cli.ToArray());
    }

    [TestMethod]
    public void FileHistorySource_AppliesOuterWindowAfterConstructorWindow()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("first.cs", "class First { }", "first");
        fixture.Commit("second.cs", "class Second { }", "second");
        fixture.Commit("third.cs", "class Third { }", "third");
        var intrinsic = Read(fixture.Path, "*.cs", skip: 1, take: 2);
        var plan = new SourceExecutionPlan
        {
            Identity = new SourceIdentity("git", "git", "git", "filehistory"),
            AcceptedSkip = 1,
            AcceptedTake = 1
        };
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: plan,
            sourceRuntimeSettings: new Dictionary<string, string> { ["GIT_HISTORY_BACKEND"] = "git-cli" });

        var rows = new FileHistoryRowsSource(fixture.Path, "*.cs", skip: 1, take: 2, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.HasCount(1, rows);
        Assert.AreEqual(intrinsic[1].CommitSha, rows[0].CommitSha);
        Assert.AreEqual(intrinsic[1].FilePath, rows[0].FilePath);
    }

    [TestMethod]
    public void FileHistorySource_ShapesCommitHeaderToAcceptedProjection()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("history.cs", "class History { }", "history");
        var plan = new SourceExecutionPlan
        {
            Identity = new SourceIdentity("git", "git", "git", "filehistory"),
            AcceptedColumns = [new SourceColumnRef(nameof(FileHistoryEntity.FilePath))],
            Properties = new Dictionary<string, object?>
            {
                [GitSourcePlanner.ProjectionPropertyName] = new GitProjection(true, [nameof(FileHistoryEntity.FilePath)])
            }
        };
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: plan,
            sourceRuntimeSettings: new Dictionary<string, string> { ["GIT_HISTORY_BACKEND"] = "git-cli" });

        var rows = new FileHistoryRowsSource(fixture.Path, "*.cs", 0, 1, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.HasCount(1, rows);
        Assert.AreEqual("history.cs", rows[0].FilePath);
        Assert.IsNull(rows[0].CommitSha);
        Assert.IsNull(rows[0].Author);
        Assert.IsNull(rows[0].AuthorEmail);
        Assert.AreEqual(default, rows[0].CommittedWhen);
    }

    [TestMethod]
    public void FileHistorySource_ParsesOnlyRequestedCommitHeaderFields()
    {
        using var fixture = GitFixture.Create();
        fixture.Commit("author.cs", "class Author { }", "author");
        var plan = new SourceExecutionPlan
        {
            Identity = new SourceIdentity("git", "git", "git", "filehistory"),
            AcceptedColumns =
            [
                new SourceColumnRef(nameof(FileHistoryEntity.FilePath)),
                new SourceColumnRef(nameof(FileHistoryEntity.Author))
            ],
            Properties = new Dictionary<string, object?>
            {
                [GitSourcePlanner.ProjectionPropertyName] = new GitProjection(
                    true,
                    [nameof(FileHistoryEntity.FilePath), nameof(FileHistoryEntity.Author)])
            }
        };
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: plan,
            sourceRuntimeSettings: new Dictionary<string, string> { ["GIT_HISTORY_BACKEND"] = "git-cli" });

        var rows = new FileHistoryRowsSource(fixture.Path, "*.cs", 0, 1, path => new Repository(path), context)
            .Chunks.SelectMany(static chunk => chunk).ToArray();

        Assert.HasCount(1, rows);
        Assert.AreEqual("author.cs", rows[0].FilePath);
        Assert.AreEqual("Musoq Test", rows[0].Author);
        Assert.IsNull(rows[0].CommitSha);
        Assert.IsNull(rows[0].AuthorEmail);
        Assert.AreEqual(default, rows[0].CommittedWhen);
    }

    private static FileHistoryEntity[] Read(string repositoryPath, string pattern, int skip, int take, string backend = "git-cli")
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                ["GIT_HISTORY_BACKEND"] = backend
            });
        var source = new FileHistoryRowsSource(repositoryPath, pattern, skip, take, path => new Repository(path), context);
        return source.Chunks.SelectMany(static chunk => chunk).ToArray();
    }

    private static SourceExecutionPlan ProjectionPlan(string source, string column) => new()
    {
        Identity = new SourceIdentity("git", "git", "git", source),
        Properties = new Dictionary<string, object?>
        {
            [GitSourcePlanner.ProjectionPropertyName] = new GitProjection(true, [column])
        }
    };

    private sealed class GitFixture : IDisposable
    {
        private readonly string _path;

        private GitFixture(string path)
        {
            _path = path;
            Run("init", "-q");
            Run("config", "user.name", "Musoq Test");
            Run("config", "user.email", "musoq-test@example.invalid");
        }

        public string Path => _path;

        public static GitFixture Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "musoq-git-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new GitFixture(path);
        }

        public void Commit(string path, string? content, string message)
        {
            var fullPath = System.IO.Path.Combine(_path, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (content is null)
            {
                File.Delete(fullPath);
            }
            else
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, content);
            }

            Run("add", "-A");
            Run("commit", "-q", "-m", message);
        }

        public void Move(string oldPath, string newPath, string message)
        {
            Run("mv", oldPath, newPath);
            Run("commit", "-q", "-m", message);
        }

        public void CopyWithSourceEdit(string source, string copy, string sourceContent, string copyContent, string message)
        {
            var sourcePath = System.IO.Path.Combine(_path, source.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var copyPath = System.IO.Path.Combine(_path, copy.Replace('/', System.IO.Path.DirectorySeparatorChar));
            File.WriteAllText(sourcePath, sourceContent);
            File.WriteAllText(copyPath, copyContent);
            Run("add", "-A");
            Run("commit", "-q", "-m", message);
        }

        public void RunGit(params string[] arguments) => Run(arguments);

        public string CreateMerge()
        {
            Run("checkout", "-q", "-b", "feature");
            Commit("feature.cs", "class Feature { }", "feature");
            Run("checkout", "-q", "master");
            Commit("master.cs", "class Master { }", "master");
            Run("merge", "--no-ff", "-q", "feature", "-m", "merge feature");
            return Run("rev-parse", "HEAD").Trim();
        }

        public void Dispose()
        {
            if (!Directory.Exists(_path))
                return;

            foreach (var file in Directory.EnumerateFiles(_path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_path, recursive: true);
        }

        private string Run(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = _path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Git test fixture.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }
    }
}
