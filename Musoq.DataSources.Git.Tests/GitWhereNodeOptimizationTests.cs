using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitWhereNodeOptimizationTests
{
    static GitWhereNodeOptimizationTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private static string Repository2ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository2.zip");

    private static string Repository3ZipPath => Path.Combine(StartDirectory, "Repositories", "Repository3.zip");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(GitWhereNodeOptimizationTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    [TestMethod]
    public async Task WhenCommitsFilteredByAuthor_ShouldReturnMatchingCommits()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                c.Sha,
                c.Author
            from #git.commits('{RepositoryPath}') c
            where c.Author = 'anonymous'"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(5, result.Count, "All 5 commits in Repository2 are by 'anonymous'");
        Assert.IsTrue(result.All(r => (string)r[1] == "anonymous"),
            "Every returned commit should have Author = 'anonymous'");
    }

    [TestMethod]
    public async Task WhenCommitsFilteredByNonExistentAuthor_ShouldReturnNoCommits()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                c.Sha,
                c.Author
            from #git.commits('{RepositoryPath}') c
            where c.Author = 'nobody'"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "No commits should be returned for a non-existent author");
    }

    [TestMethod]
    public async Task WhenCommitsFilteredBySha_ShouldReturnSingleCommit()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        const string targetSha = "789f584ce162424f61b33e020e2138aad47e60ba";

        var query = @"
            select
                c.Sha,
                c.MessageShort
            from #git.commits('{RepositoryPath}') c
            where c.Sha = '789f584ce162424f61b33e020e2138aad47e60ba'"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Exactly one commit should match the specific SHA");
        Assert.AreEqual(targetSha, (string)result[0][0], "The returned SHA should match the queried SHA");
        Assert.AreEqual("initial commit", (string)result[0][1], "The commit short message should match");
    }

    [TestMethod]
    public async Task WhenTagsFilteredByIsAnnotatedTrue_ShouldReturnAnnotatedTagsOnly()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository3ZipPath);

        var query = @"
            select
                t.FriendlyName,
                t.IsAnnotated
            from #git.tags('{RepositoryPath}') t
            where t.IsAnnotated = true"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Only one annotated tag (v0.0) should be returned");
        Assert.AreEqual("v0.0", (string)result[0][0]);
        Assert.IsTrue((bool)result[0][1]);
    }

    [TestMethod]
    public async Task WhenTagsFilteredByIsAnnotatedFalse_ShouldReturnNonAnnotatedTagsOnly()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository3ZipPath);

        var query = @"
            select
                t.FriendlyName,
                t.IsAnnotated
            from #git.tags('{RepositoryPath}') t
            where t.IsAnnotated = false"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Only one non-annotated tag (v0.1) should be returned");
        Assert.AreEqual("v0.1", (string)result[0][0]);
        Assert.IsFalse((bool)result[0][1]);
    }

    [TestMethod]
    public async Task WhenTagsFilteredByFriendlyName_ShouldReturnMatchingTag()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository3ZipPath);

        var query = @"
            select
                t.FriendlyName,
                t.CanonicalName
            from #git.tags('{RepositoryPath}') t
            where t.FriendlyName = 'v0.1'"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Exactly one tag should match FriendlyName = 'v0.1'");
        Assert.AreEqual("v0.1", (string)result[0][0]);
        Assert.AreEqual("refs/tags/v0.1", (string)result[0][1]);
    }

    [TestMethod]
    public async Task WhenBranchesFilteredByFriendlyName_ShouldReturnMatchingBranch()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                b.FriendlyName,
                b.IsCurrentRepositoryHead
            from #git.branches('{RepositoryPath}') b
            where b.FriendlyName = 'master'"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count, "Exactly one branch named 'master' should be returned");
        Assert.AreEqual("master", (string)result[0][0]);
        Assert.IsTrue((bool)result[0][1], "'master' should be the current repository HEAD");
    }

    [TestMethod]
    public async Task WhenBranchesFilteredByIsRemoteFalse_ShouldReturnLocalBranchesOnly()
    {
        using var unpackedRepositoryPath = await UnpackGitRepositoryAsync(Repository2ZipPath);

        var query = @"
            select
                b.FriendlyName,
                b.IsRemote
            from #git.branches('{RepositoryPath}') b
            where b.IsRemote = false"
            .Replace("{RepositoryPath}", unpackedRepositoryPath.Path.Escape());

        var vm = CreateAndRunVirtualMachine(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count, "Both branches in Repository2 are local");
        Assert.IsTrue(result.All(r => !(bool)r[1]), "All returned branches should have IsRemote = false");
        Assert.IsTrue(result.Any(r => (string)r[0] == "master"), "master branch should be in results");
        Assert.IsTrue(result.Any(r => (string)r[0] == "feature/feature_a"),
            "feature/feature_a branch should be in results");
    }

    private Task<UnpackedRepository> UnpackGitRepositoryAsync(string zippedRepositoryPath,
        [CallerMemberName] string? testName = null)
    {
        if (testName is null)
            throw new ArgumentNullException(nameof(testName));

        if (!File.Exists(zippedRepositoryPath))
            throw new InvalidOperationException("File does not exist.");

        var repositoryPath = Path.Combine(Path.GetTempPath(), "mqgt", testName);

        if (Directory.Exists(repositoryPath))
            Directory.Delete(repositoryPath, true);

        ZipFile.ExtractToDirectory(zippedRepositoryPath, repositoryPath);

        if (!Directory.Exists(repositoryPath))
            throw new InvalidOperationException("Directory was not created.");

        var fileName = Path.GetFileNameWithoutExtension(zippedRepositoryPath);
        var fullPath = Path.Combine(repositoryPath, fileName);
        return Task.FromResult(new UnpackedRepository(fullPath));
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new GitSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private class UnpackedRepository : IDisposable
    {
        private static readonly ConcurrentDictionary<string, int> IsCounter = new();

        public UnpackedRepository(string path)
        {
            Path = path;
            IsCounter.AddOrUpdate(path, 1, (_, value) => value + 1);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!IsCounter.TryGetValue(Path, out var value)) return;

            if (value == 1)
                IsCounter.TryRemove(Path, out _);
            else
                IsCounter.AddOrUpdate(Path, value - 1, (_, _) => value - 1);
        }

        public static implicit operator string(UnpackedRepository unpackedRepository)
        {
            return unpackedRepository.Path;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}